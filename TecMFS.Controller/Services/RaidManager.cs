using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using System.Text;
using TecMFS.Common.DTOs;
using TecMFS.Common.Interfaces;
using TecMFS.Common.Models;

namespace TecMFS.Controller.Services
{
    public class RaidManager : IRaidManager
    {
        private readonly ILogger<RaidManager> _logger;
        private readonly IHttpClientService _httpClientService;

        private readonly string metadataPath = Path.Combine(Directory.GetCurrentDirectory(), "Metadata");
        private readonly int blockSize = 512 * 1024; // 512 KB por bloque
        private readonly int nodeCount = 4;

        public RaidManager(ILogger<RaidManager> logger, IHttpClientService httpClientService)
        {
            _logger = logger;
            _httpClientService = httpClientService;

            if (!Directory.Exists(metadataPath))
                Directory.CreateDirectory(metadataPath);
        }

        public async Task<string> SaveFile(IFormFile file)
        {
            string fileName = Path.GetFileName(file.FileName);

            if (!fileName.EndsWith(".pdf", StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException("Solo se permiten archivos PDF.");

            using var ms = new MemoryStream();
            await file.CopyToAsync(ms);
            byte[] fileBytes = ms.ToArray();

            List<BlockInfo> blocks = new();
            int blockIndex = 0;
            for (int i = 0; i < fileBytes.Length; i += blockSize)
            {
                int length = Math.Min(blockSize, fileBytes.Length - i);
                byte[] chunk = new byte[length];
                Array.Copy(fileBytes, i, chunk, 0, length);

                var block = new BlockInfo
                {
                    FileName = fileName,
                    Index = blockIndex,
                    Data = chunk
                };

                int targetNode = blockIndex % nodeCount;
                await _httpClientService.SendBlockToNodeAsync(targetNode, block);

                blocks.Add(block);
                blockIndex++;
            }

            SaveMetadata(fileName, blocks);
            _logger.LogInformation($"Archivo {fileName} guardado correctamente con {blocks.Count} bloques.");
            return fileName;
        }

        public async Task<byte[]> DownloadFile(string fileName)
        {
            var metadata = LoadMetadata(fileName);
            if (metadata == null || metadata.Count == 0)
                throw new FileNotFoundException("Archivo no encontrado.");

            List<byte> result = new();

            foreach (var block in metadata.OrderBy(b => b.Index))
            {
                int node = block.Index % nodeCount;
                var received = await _httpClientService.GetBlockFromNodeAsync(node, block.FileName, block.Index);
                result.AddRange(received.Data);
            }

            return result.ToArray();
        }

        public IEnumerable<string> GetStoredFiles()
        {
            if (!Directory.Exists(metadataPath))
                return Enumerable.Empty<string>();

            return Directory.GetFiles(metadataPath, "*.meta")
                            .Select(f => Path.GetFileNameWithoutExtension(f));
        }

        public bool DeleteFile(string fileName)
        {
            var metadata = LoadMetadata(fileName);
            if (metadata == null) return false;

            foreach (var block in metadata)
            {
                int node = block.Index % nodeCount;
                _httpClientService.DeleteBlockFromNodeAsync(node, block.FileName, block.Index).Wait();
            }

            string metaFile = Path.Combine(metadataPath, fileName + ".meta");
            if (File.Exists(metaFile))
                File.Delete(metaFile);

            return true;
        }

        // --------------------------
        // Métodos auxiliares internos
        // --------------------------

        private void SaveMetadata(string fileName, List<BlockInfo> blocks)
        {
            string path = Path.Combine(metadataPath, fileName + ".meta");
            var lines = blocks.Select(b => $"{b.FileName}|{b.Index}");
            File.WriteAllLines(path, lines);
        }

        private List<BlockInfo> LoadMetadata(string fileName)
        {
            string path = Path.Combine(metadataPath, fileName + ".meta");
            if (!File.Exists(path)) return null;

            var lines = File.ReadAllLines(path);
            var blocks = lines.Select(line =>
            {
                var parts = line.Split('|');
                return new BlockInfo
                {
                    FileName = parts[0],
                    Index = int.Parse(parts[1])
                };
            }).ToList();

            return blocks;
        }

        public async Task<bool> StoreFileAsync(UploadRequest request)
        {
            if (request == null ||
                request.FileData == null ||
                request.FileData.Length == 0 ||
                string.IsNullOrWhiteSpace(request.FileName))
            {
                throw new ArgumentException("Solicitud de carga inválida.");
            }

            if (!request.IsValidPdf)
                throw new InvalidOperationException("Solo se permiten archivos PDF.");

            await SaveFile(request.FileName, request.FileData);
            return true;
        }

        public async Task<byte[]?> RetrieveFileAsync(string fileName)
        {
            if (string.IsNullOrWhiteSpace(fileName))
                throw new ArgumentException("Nombre de archivo inválido.");

            return await DownloadFile(fileName);
        }

        public Task<bool> DeleteFileAsync(string fileName)
        {
            if (string.IsNullOrWhiteSpace(fileName))
                throw new ArgumentException("Nombre de archivo inválido.");

            bool eliminado = DeleteFile(fileName);
            return Task.FromResult(eliminado);
        }

        public Task<List<FileMetadata>> ListFilesAsync()
        {
            var files = GetStoredFiles()
                .Select(name => new FileMetadata { FileName = name })
                .ToList();

            return Task.FromResult(files);
        }

        public Task<List<FileMetadata>> SearchFilesAsync(string searchQuery)
        {
            var results = GetStoredFiles()
                .Where(name => name.Contains(searchQuery, StringComparison.OrdinalIgnoreCase))
                .Select(name => new FileMetadata { FileName = name })
                .ToList();

            return Task.FromResult(results);
        }

        public Task<FileMetadata?> GetFileMetadataAsync(string fileName)
        {
            var metadata = LoadMetadata(fileName);
            if (metadata == null)
                return Task.FromResult<FileMetadata?>(null);

            return Task.FromResult<FileMetadata?>(new FileMetadata
            {
                FileName = fileName,
                Blocks = metadata,
                FileSize = metadata.Sum(b => b.Data?.Length ?? 0),
                IsComplete = true
            });
        }

        public async Task<StatusResponse> GetRaidStatusAsync()
        {
            var response = new StatusResponse
            {
                ComponentName = "ControllerNode",
                Status = StatusResponse.ComponentStatus.Healthy,
                Timestamp = DateTime.UtcNow,
                Version = "1.0.0",
                Uptime = DateTime.UtcNow - System.Diagnostics.Process.GetCurrentProcess().StartTime.ToUniversalTime(),
                TotalFiles = 0,
                TotalBlocks = 0,
                UsedStorage = 0,
                TotalStorage = 0,
                ErrorCount = 0,
                LastError = string.Empty,
                Nodes = new List<NodeStatus>()
            };

            try
            {
                // Suponemos 4 nodos (RAID 5)
                for (int nodeId = 0; nodeId < 4; nodeId++)
                {
                    var status = await _httpClientService.GetNodeStatusAsync(nodeId); // Este método debe existir
                    response.Nodes.Add(status);

                    if (!status.IsOnline)
                    {
                        response.Status = StatusResponse.ComponentStatus.Warning;
                        response.ErrorCount++;
                        response.LastError = $"Nodo {nodeId} no responde.";
                    }

                    response.TotalBlocks += status.BlockCount;
                    response.UsedStorage += status.UsedStorage;
                    response.TotalStorage += status.TotalStorage;
                }

                response.TotalFiles = GetStoredFiles().Count(); // usa los metadatos locales si es necesario
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al obtener el estado del sistema RAID");
                response.Status = StatusResponse.ComponentStatus.Error;
                response.LastError = ex.Message;
                response.ErrorCount++;
            }

            return response;
        }


        public Task<bool> RecoverFromNodeFailureAsync(int failedNodeId)
        {
            throw new NotImplementedException("La recuperación de fallos aún no está implementada.");
        }

        public Task<bool> VerifySystemIntegrityAsync()
        {
            throw new NotImplementedException("La verificación de integridad aún no está implementada.");
        }

        private async Task SaveFile(string fileName, byte[] data)
        {
            List<BlockInfo> blocks = new();
            int blockIndex = 0;

            for (int i = 0; i < data.Length; i += blockSize)
            {
                int length = Math.Min(blockSize, data.Length - i);
                byte[] chunk = new byte[length];
                Array.Copy(data, i, chunk, 0, length);

                var block = new BlockInfo
                {
                    FileName = fileName,
                    Index = blockIndex,
                    Data = chunk
                };

                int targetNode = blockIndex % nodeCount;
                await _httpClientService.SendBlockToNodeAsync(targetNode, block);

                blocks.Add(block);
                blockIndex++;
            }

            SaveMetadata(fileName, blocks);
            _logger.LogInformation($"Archivo {fileName} guardado correctamente con {blocks.Count} bloques.");
        }


    }
}
