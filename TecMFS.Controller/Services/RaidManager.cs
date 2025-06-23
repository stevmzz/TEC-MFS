using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using TecMFS.Common.DTOs;
using TecMFS.Common.Models;
using TecMFS.Common.Interfaces;
using TecMFS.Common.Constants;
using System.Security.Cryptography;
using System.Text;

namespace TecMFS.Controller.Services
{
    // implementacion del gestor raid 5 para distribucion de archivos y tolerancia a fallos
    public class RaidManager : IRaidManager
    {
        private readonly IHttpClientService _httpClient;
        private readonly ILogger<RaidManager> _logger;
        private readonly List<FileMetadata> _fileDatabase; // base de datos en memoria
        private readonly Dictionary<int, string> _nodeUrls; // urls de los nodos

        public RaidManager(IHttpClientService httpClient, ILogger<RaidManager> logger)
        {
            _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _fileDatabase = new List<FileMetadata>();

            // configurar urls de los nodos
            _nodeUrls = new Dictionary<int, string>();
            for (int i = 1; i <= SystemConstants.RAID_TOTAL_NODES; i++)
            {
                _nodeUrls[i] = SystemConstants.GetDiskNodeBaseUrl(i);
            }

            _logger.LogInformation("RaidManager inicializado con 4 nodos RAID 5");
        }



        // calcula paridad local usando operacion xor
        private byte[] CalculateParityLocal(List<byte[]> dataBlocks)
        {
            if (dataBlocks == null || !dataBlocks.Any())
            {
                return Array.Empty<byte>();
            }

            var maxSize = dataBlocks.Max(b => b.Length);
            var parityBlock = new byte[maxSize];

            foreach (var dataBlock in dataBlocks)
            {
                for (int i = 0; i < dataBlock.Length; i++)
                {
                    parityBlock[i] ^= dataBlock[i];
                }
            }

            return parityBlock;
        }



        // recupera bloque de datos perdido usando bloques disponibles y paridad
        private byte[] RecoverDataBlockLocal(List<byte[]> availableBlocks, byte[] parityBlock, int missingBlockIndex)
        {
            var recoveredBlock = new byte[parityBlock.Length];
            Array.Copy(parityBlock, recoveredBlock, parityBlock.Length);

            foreach (var availableBlock in availableBlocks)
            {
                for (int i = 0; i < Math.Min(availableBlock.Length, recoveredBlock.Length); i++)
                {
                    recoveredBlock[i] ^= availableBlock[i];
                }
            }

            return recoveredBlock;
        }



        // calcula checksum sha256 local para verificacion de integridad
        private string CalculateChecksumLocal(byte[] blockData)
        {
            if (blockData == null || blockData.Length == 0)
            {
                return string.Empty;
            }

            using var sha256 = SHA256.Create();
            var hash = sha256.ComputeHash(blockData);
            return Convert.ToHexString(hash);
        }



        // almacena un archivo en el sistema raid distribuyendo bloques
        public async Task<bool> StoreFileAsync(UploadRequest request)
        {
            if (request == null || string.IsNullOrEmpty(request.FileName) || request.FileData == null)
            {
                _logger.LogWarning("Request de upload invalido");
                return false;
            }

            try
            {
                _logger.LogInformation($"Iniciando almacenamiento RAID para archivo: {request.FileName}");

                // dividir archivo en bloques
                var blocks = DivideFileIntoBlocks(request.FileData);
                var fileMetadata = new FileMetadata
                {
                    Id = Guid.NewGuid().ToString(),
                    FileName = request.FileName,
                    FileSize = request.FileData.Length,
                    UploadDate = DateTime.UtcNow,
                    ContentType = request.ContentType,
                    Blocks = new List<BlockInfo>()
                };

                // almacenar cada conjunto de 3 bloques + 1 paridad
                for (int i = 0; i < blocks.Count; i += 3)
                {
                    var dataBlocks = blocks.Skip(i).Take(3).ToList();
                    if (!await StoreBlockGroup(dataBlocks, i, fileMetadata))
                    {
                        _logger.LogError($"Error almacenando grupo de bloques {i} para archivo {request.FileName}");
                        return false;
                    }
                }

                fileMetadata.IsComplete = true;
                _fileDatabase.Add(fileMetadata);

                _logger.LogInformation($"Archivo {request.FileName} almacenado exitosamente con {fileMetadata.Blocks.Count} bloques");
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error critico almacenando archivo: {request.FileName}");
                return false;
            }
        }



        // recupera un archivo del sistema raid reconstruyendo desde bloques
        public async Task<byte[]?> RetrieveFileAsync(string fileName)
        {
            if (string.IsNullOrEmpty(fileName))
            {
                _logger.LogWarning("Nombre de archivo vacio");
                return null;
            }

            try
            {
                var fileMetadata = _fileDatabase.FirstOrDefault(f => f.FileName.Equals(fileName, StringComparison.OrdinalIgnoreCase));
                if (fileMetadata == null)
                {
                    _logger.LogWarning($"Archivo no encontrado: {fileName}");
                    return null;
                }

                _logger.LogInformation($"Recuperando archivo: {fileName} con {fileMetadata.Blocks.Count} bloques");

                var allBlocks = new List<byte[]>();

                // recuperar bloques de datos (ignorar bloques de paridad)
                var dataBlocks = fileMetadata.Blocks.Where(b => !b.IsParityBlock).OrderBy(b => b.BlockIndex).ToList();

                foreach (var blockInfo in dataBlocks)
                {
                    var blockData = await RetrieveBlockFromNode(blockInfo);
                    if (blockData != null)
                    {
                        allBlocks.Add(blockData);
                    }
                    else
                    {
                        // intentar recuperar con paridad si falla
                        var recoveredBlock = await RecoverBlockWithParity(blockInfo, fileMetadata);
                        if (recoveredBlock != null)
                        {
                            allBlocks.Add(recoveredBlock);
                            _logger.LogInformation($"Bloque {blockInfo.BlockIndex} recuperado usando paridad");
                        }
                        else
                        {
                            _logger.LogError($"No se pudo recuperar bloque {blockInfo.BlockIndex}");
                            return null;
                        }
                    }
                }

                // combinar todos los bloques
                var totalSize = (int)fileMetadata.FileSize;
                var result = new byte[totalSize];
                var offset = 0;

                foreach (var block in allBlocks)
                {
                    var copySize = Math.Min(block.Length, totalSize - offset);
                    Array.Copy(block, 0, result, offset, copySize);
                    offset += copySize;
                }

                _logger.LogInformation($"Archivo {fileName} recuperado exitosamente");
                return result;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error recuperando archivo: {fileName}");
                return null;
            }
        }



        // elimina un archivo del sistema raid removiendo todos sus bloques
        public async Task<bool> DeleteFileAsync(string fileName)
        {
            if (string.IsNullOrEmpty(fileName))
            {
                return false;
            }

            try
            {
                var fileMetadata = _fileDatabase.FirstOrDefault(f => f.FileName.Equals(fileName, StringComparison.OrdinalIgnoreCase));
                if (fileMetadata == null)
                {
                    return false;
                }

                _logger.LogInformation($"Eliminando archivo: {fileName} con {fileMetadata.Blocks.Count} bloques");

                // eliminar todos los bloques
                foreach (var blockInfo in fileMetadata.Blocks)
                {
                    await DeleteBlockFromNode(blockInfo);
                }

                _fileDatabase.Remove(fileMetadata);
                _logger.LogInformation($"Archivo {fileName} eliminado exitosamente");
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error eliminando archivo: {fileName}");
                return false;
            }
        }



        // lista todos los archivos almacenados en el sistema
        public async Task<List<FileMetadata>> ListFilesAsync()
        {
            return _fileDatabase.ToList();
        }



        // busca archivos por termino en el nombre
        public async Task<List<FileMetadata>> SearchFilesAsync(string searchQuery)
        {
            if (string.IsNullOrEmpty(searchQuery))
            {
                return new List<FileMetadata>();
            }

            return _fileDatabase.Where(f => f.FileName.Contains(searchQuery, StringComparison.OrdinalIgnoreCase)).ToList();
        }



        // obtiene metadata completa de un archivo especifico
        public async Task<FileMetadata?> GetFileMetadataAsync(string fileName)
        {
            return _fileDatabase.FirstOrDefault(f => f.FileName.Equals(fileName, StringComparison.OrdinalIgnoreCase));
        }



        // obtiene estado general del sistema raid
        public async Task<StatusResponse> GetRaidStatusAsync()
        {
            var totalFiles = _fileDatabase.Count;
            var totalBlocks = _fileDatabase.Sum(f => f.Blocks.Count);

            return new StatusResponse
            {
                ComponentName = "RAID 5 System",
                Status = StatusResponse.ComponentStatus.Healthy,
                Message = "Sistema funcionando correctamente",
                TotalFiles = totalFiles,
                TotalBlocks = totalBlocks,
                Timestamp = DateTime.UtcNow
            };
        }



        // maneja recuperacion automatica cuando falla un nodo
        public async Task<bool> RecoverFromNodeFailureAsync(int failedNodeId)
        {
            _logger.LogWarning($"Nodo {failedNodeId} marcado como fallido - recuperacion automatica disponible via paridad");
            return true; // el sistema puede funcionar con un nodo caido
        }



        // verifica integridad del sistema chequeando nodos online
        public async Task<bool> VerifySystemIntegrityAsync()
        {
            var onlineNodes = 0;
            for (int i = 1; i <= SystemConstants.RAID_TOTAL_NODES; i++)
            {
                if (await _httpClient.CheckHealthAsync(_nodeUrls[i]))
                {
                    onlineNodes++;
                }
            }

            var isHealthy = onlineNodes >= 3; // necesita al menos 3 nodos para funcionar
            _logger.LogInformation($"Verificacion de integridad: {onlineNodes}/4 nodos online - Estado: {(isHealthy ? "Saludable" : "Degradado")}");
            return isHealthy;
        }



        // divide archivo en bloques de tamaño fijo para distribucion
        private List<byte[]> DivideFileIntoBlocks(byte[] fileData)
        {
            var blocks = new List<byte[]>();
            var blockSize = SystemConstants.DEFAULT_BLOCK_SIZE;

            for (int i = 0; i < fileData.Length; i += blockSize)
            {
                var remainingBytes = Math.Min(blockSize, fileData.Length - i);
                var block = new byte[remainingBytes];
                Array.Copy(fileData, i, block, 0, remainingBytes);
                blocks.Add(block);
            }

            return blocks;
        }



        // almacena un grupo de 3 bloques de datos mas 1 bloque de paridad
        private async Task<bool> StoreBlockGroup(List<byte[]> dataBlocks, int baseIndex, FileMetadata fileMetadata)
        {
            try
            {
                // calcular paridad para este grupo usando xor simple
                var parityBlock = CalculateParityLocal(dataBlocks);

                // determinar nodo de paridad para este grupo (rotacion)
                var parityNodeId = (baseIndex / 3 % SystemConstants.RAID_TOTAL_NODES) + 1;

                // almacenar bloques de datos
                for (int i = 0; i < dataBlocks.Count; i++)
                {
                    var nodeId = GetNodeForDataBlock(baseIndex + i, parityNodeId);
                    var blockId = $"{fileMetadata.Id}_block_{baseIndex + i}";

                    if (await StoreBlockInNode(nodeId, blockId, dataBlocks[i], false))
                    {
                        var blockInfo = new BlockInfo
                        {
                            BlockId = blockId,
                            NodeId = nodeId,
                            IsParityBlock = false,
                            BlockIndex = baseIndex + i,
                            BlockSize = dataBlocks[i].Length,
                            CheckSum = CalculateChecksumLocal(dataBlocks[i]),
                            CreatedAt = DateTime.UtcNow
                        };
                        fileMetadata.Blocks.Add(blockInfo);
                    }
                    else
                    {
                        return false;
                    }
                }

                // almacenar bloque de paridad
                var parityBlockId = $"{fileMetadata.Id}_parity_{baseIndex / 3}";
                if (await StoreBlockInNode(parityNodeId, parityBlockId, parityBlock, true))
                {
                    var parityBlockInfo = new BlockInfo
                    {
                        BlockId = parityBlockId,
                        NodeId = parityNodeId,
                        IsParityBlock = true,
                        BlockIndex = baseIndex / 3,
                        BlockSize = parityBlock.Length,
                        CheckSum = CalculateChecksumLocal(parityBlock),
                        CreatedAt = DateTime.UtcNow
                    };
                    fileMetadata.Blocks.Add(parityBlockInfo);
                }
                else
                {
                    return false;
                }

                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error almacenando grupo de bloques en base {baseIndex}");
                return false;
            }
        }



        // determina en que nodo va un bloque de datos evitando el nodo de paridad
        private int GetNodeForDataBlock(int blockIndex, int parityNodeId)
        {
            var nodeId = (blockIndex % SystemConstants.RAID_TOTAL_NODES) + 1;

            // si coincide con nodo de paridad, usar el siguiente
            if (nodeId == parityNodeId)
            {
                nodeId = (nodeId % SystemConstants.RAID_TOTAL_NODES) + 1;
            }

            return nodeId;
        }



        // almacena un bloque en un nodo especifico via api http
        private async Task<bool> StoreBlockInNode(int nodeId, string blockId, byte[] blockData, bool isParityBlock)
        {
            try
            {
                var request = new BlockRequest
                {
                    BlockId = blockId,
                    Operation = BlockRequest.BlockOperation.Store,
                    BlockData = blockData,
                    IsParityBlock = isParityBlock,
                    BlockIndex = 0,
                    FileId = "temp",
                    CheckSum = CalculateChecksumLocal(blockData)
                };

                _logger.LogInformation($"Enviando bloque a nodo {nodeId}: BlockId={blockId}, Tamano={blockData.Length} bytes, EsParidad={isParityBlock}");

                var url = $"{_nodeUrls[nodeId]}{ApiEndpoints.BLOCKS_STORE}";
                var response = await _httpClient.SendPostAsync<object>(url, request);

                return response != null;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error almacenando bloque {blockId} en nodo {nodeId}");
                return false;
            }
        }



        // recupera un bloque de un nodo especifico
        private async Task<byte[]?> RetrieveBlockFromNode(BlockInfo blockInfo)
        {
            try
            {
                var url = $"{_nodeUrls[blockInfo.NodeId]}{ApiEndpoints.GetBlockRetrieveUrl(blockInfo.BlockId)}";
                var response = await _httpClient.SendGetAsync<BlockRetrieveResponse>(url);

                return response?.BlockData;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error recuperando bloque {blockInfo.BlockId} del nodo {blockInfo.NodeId}");
                return null;
            }
        }



        // recupera un bloque perdido usando paridad y bloques disponibles del grupo
        private async Task<byte[]?> RecoverBlockWithParity(BlockInfo failedBlock, FileMetadata fileMetadata)
        {
            try
            {
                // encontrar grupo de paridad correspondiente
                var groupIndex = failedBlock.BlockIndex / 3;
                var parityBlock = fileMetadata.Blocks.FirstOrDefault(b => b.IsParityBlock && b.BlockIndex == groupIndex);

                if (parityBlock == null)
                {
                    return null;
                }

                // obtener bloques disponibles del mismo grupo
                var groupBlocks = fileMetadata.Blocks
                    .Where(b => !b.IsParityBlock && b.BlockIndex >= groupIndex * 3 && b.BlockIndex < (groupIndex + 1) * 3 && !b.BlockId.Equals(failedBlock.BlockId))
                    .ToList();

                var availableBlocks = new List<byte[]>();
                foreach (var block in groupBlocks)
                {
                    var blockData = await RetrieveBlockFromNode(block);
                    if (blockData != null)
                    {
                        availableBlocks.Add(blockData);
                    }
                }

                var parityData = await RetrieveBlockFromNode(parityBlock);
                if (parityData == null)
                {
                    return null;
                }

                // recuperar usando paridad
                return RecoverDataBlockLocal(availableBlocks, parityData, failedBlock.BlockIndex % 3);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error recuperando bloque {failedBlock.BlockId} con paridad");
                return null;
            }
        }



        // elimina un bloque de un nodo especifico
        private async Task<bool> DeleteBlockFromNode(BlockInfo blockInfo)
        {
            try
            {
                var url = $"{_nodeUrls[blockInfo.NodeId]}{ApiEndpoints.GetBlockDeleteUrl(blockInfo.BlockId)}";
                return await _httpClient.SendDeleteAsync(url);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error eliminando bloque {blockInfo.BlockId} del nodo {blockInfo.NodeId}");
                return false;
            }
        }

        // clase helper para respuesta de recuperacion de bloque
        private class BlockRetrieveResponse
        {
            public string BlockId { get; set; } = string.Empty;
            public byte[] BlockData { get; set; } = Array.Empty<byte>();
            public long BlockSize { get; set; }
            public DateTime RetrievedAt { get; set; }
            public int NodeId { get; set; }
            public string CheckSum { get; set; } = string.Empty;
        }
    }
}