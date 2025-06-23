using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using TecMFS.Common.Interfaces;

namespace TecMFS.DiskNode.Services
{
    // implementacion de almacenamiento fisico de bloques en disco
    // maneja operaciones de guardar, recuperar y eliminar bloques
    public class BlockStorage : IBlockStorage
    {
        private readonly string _storagePath;
        private readonly ILogger<BlockStorage>? _logger;
        private readonly ParityCalculator _parityCalculator;

        public BlockStorage(string storagePath, ILogger<BlockStorage>? logger = null)
        {
            _storagePath = storagePath ?? throw new ArgumentNullException(nameof(storagePath));
            _logger = logger;
            _parityCalculator = new ParityCalculator();

            // crear directorio si no existe
            if (!Directory.Exists(_storagePath))
            {
                Directory.CreateDirectory(_storagePath);
                _logger?.LogInformation($"Directorio de almacenamiento creado: {_storagePath}");
            }
        }

        // almacena un bloque en el disco
        public async Task<bool> StoreBlockAsync(string blockId, byte[] blockData)
        {
            if (string.IsNullOrEmpty(blockId) || blockData == null)
            {
                _logger?.LogWarning("BlockId o datos del bloque son invalidos");
                return false;
            }

            try
            {
                var filePath = GetBlockFilePath(blockId);
                await File.WriteAllBytesAsync(filePath, blockData);

                // crear archivo de metadata con checksum
                var checksum = _parityCalculator.CalculateChecksum(blockData);
                var metadataPath = GetMetadataFilePath(blockId);
                var metadata = $"{blockData.Length}|{checksum}|{DateTime.UtcNow:yyyy-MM-dd HH:mm:ss}";
                await File.WriteAllTextAsync(metadataPath, metadata);

                _logger?.LogInformation($"Bloque almacenado exitosamente: {blockId}, Tamaño: {blockData.Length} bytes");
                return true;
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, $"Error almacenando bloque: {blockId}");
                return false;
            }
        }

        // recupera un bloque del disco
        public async Task<byte[]?> RetrieveBlockAsync(string blockId)
        {
            if (string.IsNullOrEmpty(blockId))
            {
                _logger?.LogWarning("BlockId vacio para recuperacion");
                return null;
            }

            try
            {
                var filePath = GetBlockFilePath(blockId);
                if (!File.Exists(filePath))
                {
                    _logger?.LogWarning($"Bloque no encontrado: {blockId}");
                    return null;
                }

                var blockData = await File.ReadAllBytesAsync(filePath);

                // verificar integridad si existe metadata
                var metadataPath = GetMetadataFilePath(blockId);
                if (File.Exists(metadataPath))
                {
                    var metadata = await File.ReadAllTextAsync(metadataPath);
                    var parts = metadata.Split('|');
                    if (parts.Length >= 2)
                    {
                        var expectedChecksum = parts[1];
                        if (!_parityCalculator.VerifyChecksum(blockData, expectedChecksum))
                        {
                            _logger?.LogError($"Verificacion de integridad fallo para bloque: {blockId}");
                            return null;
                        }
                    }
                }

                _logger?.LogDebug($"Bloque recuperado exitosamente: {blockId}, Tamaño: {blockData.Length} bytes");
                return blockData;
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, $"Error recuperando bloque: {blockId}");
                return null;
            }
        }

        // elimina un bloque del disco
        public async Task<bool> DeleteBlockAsync(string blockId)
        {
            if (string.IsNullOrEmpty(blockId))
            {
                _logger?.LogWarning("BlockId vacio para eliminacion");
                return false;
            }

            try
            {
                var filePath = GetBlockFilePath(blockId);
                var metadataPath = GetMetadataFilePath(blockId);

                if (File.Exists(filePath))
                {
                    File.Delete(filePath);
                }

                if (File.Exists(metadataPath))
                {
                    File.Delete(metadataPath);
                }

                _logger?.LogInformation($"Bloque eliminado exitosamente: {blockId}");
                return true;
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, $"Error eliminando bloque: {blockId}");
                return false;
            }
        }

        // verifica si un bloque existe
        public async Task<bool> BlockExistsAsync(string blockId)
        {
            return await Task.Run(() =>
            {
                if (string.IsNullOrEmpty(blockId))
                {
                    return false;
                }

                var filePath = GetBlockFilePath(blockId);
                return File.Exists(filePath);
            });
        }

        // lista todos los bloques almacenados
        public async Task<List<string>> ListBlocksAsync()
        {
            return await Task.Run(() =>
            {
                try
                {
                    if (!Directory.Exists(_storagePath))
                    {
                        return new List<string>();
                    }

                    var files = Directory.GetFiles(_storagePath, "*.block");
                    var blockIds = files.Select(f => Path.GetFileNameWithoutExtension(f)).ToList();

                    _logger?.LogDebug($"Listado de bloques: {blockIds.Count} bloques encontrados");
                    return blockIds;
                }
                catch (Exception ex)
                {
                    _logger?.LogError(ex, "Error listando bloques");
                    return new List<string>();
                }
            });
        }

        // obtiene espacio disponible
        public async Task<long> GetAvailableSpaceAsync()
        {
            return await Task.Run(() =>
            {
                try
                {
                    var drive = new DriveInfo(Path.GetPathRoot(Path.GetFullPath(_storagePath))!);
                    return drive.AvailableFreeSpace;
                }
                catch (Exception ex)
                {
                    _logger?.LogError(ex, "Error obteniendo espacio disponible");
                    return 0;
                }
            });
        }

        // obtiene espacio usado
        public async Task<long> GetUsedSpaceAsync()
        {
            return await Task.Run(() =>
            {
                try
                {
                    if (!Directory.Exists(_storagePath))
                    {
                        return 0;
                    }

                    var files = Directory.GetFiles(_storagePath, "*.*", SearchOption.AllDirectories);
                    return files.Sum(f => new FileInfo(f).Length);
                }
                catch (Exception ex)
                {
                    _logger?.LogError(ex, "Error obteniendo espacio usado");
                    return 0;
                }
            });
        }

        // verifica integridad de un bloque
        public async Task<bool> VerifyBlockIntegrityAsync(string blockId, string expectedChecksum)
        {
            var blockData = await RetrieveBlockAsync(blockId);
            if (blockData == null)
            {
                return false;
            }

            return _parityCalculator.VerifyChecksum(blockData, expectedChecksum);
        }

        // limpia bloques huerfanos
        public async Task<int> CleanupOrphanedBlocksAsync()
        {
            return await Task.Run(() =>
            {
                try
                {
                    var blockFiles = Directory.GetFiles(_storagePath, "*.block");
                    var metadataFiles = Directory.GetFiles(_storagePath, "*.meta");

                    var blockIds = blockFiles.Select(f => Path.GetFileNameWithoutExtension(f)).ToHashSet();
                    var metadataIds = metadataFiles.Select(f => Path.GetFileNameWithoutExtension(f)).ToHashSet();

                    var orphanedCount = 0;

                    // eliminar archivos de metadata sin bloque correspondiente
                    foreach (var metadataId in metadataIds)
                    {
                        if (!blockIds.Contains(metadataId))
                        {
                            File.Delete(GetMetadataFilePath(metadataId));
                            orphanedCount++;
                        }
                    }

                    _logger?.LogInformation($"Limpieza completada: {orphanedCount} archivos huerfanos eliminados");
                    return orphanedCount;
                }
                catch (Exception ex)
                {
                    _logger?.LogError(ex, "Error durante limpieza de archivos huerfanos");
                    return 0;
                }
            });
        }

        // metodos privados helper
        private string GetBlockFilePath(string blockId)
        {
            return Path.Combine(_storagePath, $"{blockId}.block");
        }

        private string GetMetadataFilePath(string blockId)
        {
            return Path.Combine(_storagePath, $"{blockId}.meta");
        }
    }
}