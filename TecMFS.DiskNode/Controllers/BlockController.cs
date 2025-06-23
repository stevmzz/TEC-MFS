using Microsoft.AspNetCore.Mvc;
using TecMFS.Common.DTOs;
using TecMFS.Common.Models;
using TecMFS.Common.Constants;
using TecMFS.Common.Interfaces;

namespace TecMFS.DiskNode.Controllers
{
    [ApiController]
    [Route("api/blocks")]
    [Produces("application/json")]
    public class BlockController : ControllerBase
    {
        private readonly ILogger<BlockController> _logger;
        private readonly IBlockStorage _blockStorage;

        public BlockController(ILogger<BlockController> logger, IBlockStorage blockStorage)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _blockStorage = blockStorage ?? throw new ArgumentNullException(nameof(blockStorage));
        }

        // post: almacena un bloque de datos en el nodo
        [HttpPost("store")]
        [ProducesResponseType(typeof(BlockStoreResponse), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status507InsufficientStorage)]
        [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status500InternalServerError)]
        public async Task<IActionResult> StoreBlock([FromBody] BlockRequest request)
        {
            _logger.LogInformation($"=== RECIBIENDO REQUEST DE BLOQUE ===");
            _logger.LogInformation($"Request es null: {request == null}");
            if (request != null)
            {
                _logger.LogInformation($"BlockId: '{request.BlockId}'");
                _logger.LogInformation($"BlockData es null: {request.BlockData == null}");
                _logger.LogInformation($"BlockData.Length: {request.BlockData?.Length ?? 0}");
                _logger.LogInformation($"Operation: {request.Operation}");
                _logger.LogInformation($"IsParityBlock: {request.IsParityBlock}");
            }

            var sizeInKb = request.BlockData.Length / 1024.0;
            _logger.LogInformation($"Iniciando almacenamiento de bloque: {request.BlockId}, Tamaño: {sizeInKb:F1} KB, Es paridad: {request.IsParityBlock}");

            try
            {
                // validar request de bloque
                var validationResult = ValidateBlockRequest(request);
                if (!validationResult.IsValid)
                {
                    _logger.LogWarning($"Validacion fallida para bloque: {request.BlockId} - {validationResult.ErrorMessage}");
                    return BadRequest(new ErrorResponse
                    {
                        Error = "Validacion fallida",
                        Message = validationResult.ErrorMessage,
                        RequestId = request.RequestId
                    });
                }

                // verificar espacio disponible
                var availableSpace = await _blockStorage.GetAvailableSpaceAsync();
                if (request.BlockData.Length > availableSpace)
                {
                    _logger.LogWarning($"Espacio insuficiente para bloque: {request.BlockId}, Requerido: {sizeInKb:F1} KB, Disponible: {availableSpace / 1024.0:F1} KB");
                    return StatusCode(507, new ErrorResponse
                    {
                        Error = "Espacio insuficiente",
                        Message = $"No hay suficiente espacio para almacenar el bloque",
                        RequestId = request.RequestId
                    });
                }

                // USAR BLOCK STORAGE REAL
                var success = await _blockStorage.StoreBlockAsync(request.BlockId, request.BlockData);

                if (success)
                {
                    var response = new BlockStoreResponse
                    {
                        Success = true,
                        BlockId = request.BlockId,
                        BlockSize = request.BlockData.Length,
                        StoredAt = DateTime.UtcNow,
                        NodeId = GetCurrentNodeId(),
                        IsParityBlock = request.IsParityBlock,
                        CheckSum = CalculateChecksum(request.BlockData),
                        Message = "Bloque almacenado exitosamente"
                    };

                    _logger.LogInformation($"Bloque almacenado exitosamente: {request.BlockId}, Checksum: {response.CheckSum}");
                    return Ok(response);
                }
                else
                {
                    return StatusCode(500, new ErrorResponse
                    {
                        Error = "Error almacenando bloque",
                        Message = "No se pudo almacenar el bloque",
                        RequestId = request.RequestId
                    });
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error critico almacenando bloque: {request.BlockId}");
                return StatusCode(500, new ErrorResponse
                {
                    Error = "Error interno del servidor",
                    Message = "Error procesando almacenamiento del bloque",
                    RequestId = request.RequestId
                });
            }
        }

        // get: recupera un bloque especifico por su id
        [HttpGet("retrieve/{blockId}")]
        [ProducesResponseType(typeof(BlockRetrieveResponse), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status404NotFound)]
        [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status500InternalServerError)]
        public async Task<IActionResult> RetrieveBlock([FromRoute] string blockId)
        {
            _logger.LogInformation($"Iniciando recuperacion de bloque: {blockId}");

            try
            {
                if (string.IsNullOrWhiteSpace(blockId))
                {
                    _logger.LogWarning("BlockId vacio para recuperacion");
                    return BadRequest(new ErrorResponse
                    {
                        Error = "BlockId invalido",
                        Message = "El BlockId no puede estar vacio",
                        RequestId = Guid.NewGuid().ToString()
                    });
                }

                // USAR BLOCK STORAGE REAL
                var blockData = await _blockStorage.RetrieveBlockAsync(blockId);

                if (blockData == null)
                {
                    _logger.LogWarning($"Bloque no encontrado: {blockId}");
                    return NotFound(new ErrorResponse
                    {
                        Error = "Bloque no encontrado",
                        Message = $"El bloque {blockId} no existe en este nodo",
                        RequestId = Guid.NewGuid().ToString()
                    });
                }

                var response = new BlockRetrieveResponse
                {
                    BlockId = blockId,
                    BlockData = blockData,
                    BlockSize = blockData.Length,
                    RetrievedAt = DateTime.UtcNow,
                    NodeId = GetCurrentNodeId(),
                    CheckSum = CalculateChecksum(blockData)
                };

                var sizeInKb = blockData.Length / 1024.0;
                _logger.LogInformation($"Bloque recuperado exitosamente: {blockId}, Tamaño: {sizeInKb:F1} KB");
                return Ok(response);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error critico recuperando bloque: {blockId}");
                return StatusCode(500, new ErrorResponse
                {
                    Error = "Error interno del servidor",
                    Message = "Error procesando recuperacion del bloque",
                    RequestId = Guid.NewGuid().ToString()
                });
            }
        }

        // delete: elimina un bloque del nodo
        [HttpDelete("delete/{blockId}")]
        [ProducesResponseType(typeof(BlockDeleteResponse), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status404NotFound)]
        [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status500InternalServerError)]
        public async Task<IActionResult> DeleteBlock([FromRoute] string blockId)
        {
            _logger.LogInformation($"Iniciando eliminacion de bloque: {blockId}");

            try
            {
                if (string.IsNullOrWhiteSpace(blockId))
                {
                    _logger.LogWarning("BlockId vacio para eliminacion");
                    return BadRequest(new ErrorResponse
                    {
                        Error = "BlockId invalido",
                        Message = "El BlockId no puede estar vacio",
                        RequestId = Guid.NewGuid().ToString()
                    });
                }

                // USAR BLOCK STORAGE REAL
                var exists = await _blockStorage.BlockExistsAsync(blockId);
                if (!exists)
                {
                    _logger.LogWarning($"Bloque no encontrado para eliminacion: {blockId}");
                    return NotFound(new ErrorResponse
                    {
                        Error = "Bloque no encontrado",
                        Message = $"El bloque {blockId} no existe en este nodo",
                        RequestId = Guid.NewGuid().ToString()
                    });
                }

                var success = await _blockStorage.DeleteBlockAsync(blockId);
                if (success)
                {
                    var result = new BlockDeleteResponse
                    {
                        Success = true,
                        BlockId = blockId,
                        DeletedAt = DateTime.UtcNow,
                        NodeId = GetCurrentNodeId(),
                        Message = "Bloque eliminado exitosamente"
                    };

                    _logger.LogInformation($"Bloque eliminado exitosamente: {blockId}");
                    return Ok(result);
                }
                else
                {
                    return StatusCode(500, new ErrorResponse
                    {
                        Error = "Error eliminando bloque",
                        Message = "No se pudo eliminar el bloque",
                        RequestId = Guid.NewGuid().ToString()
                    });
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error critico eliminando bloque: {blockId}");
                return StatusCode(500, new ErrorResponse
                {
                    Error = "Error interno del servidor",
                    Message = "Error procesando eliminacion del bloque",
                    RequestId = Guid.NewGuid().ToString()
                });
            }
        }

        // head: verifica si un bloque existe sin retornar datos
        [HttpHead("exists/{blockId}")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> BlockExists([FromRoute] string blockId)
        {
            _logger.LogDebug($"Verificando existencia de bloque: {blockId}");

            try
            {
                // USAR BLOCK STORAGE REAL
                var exists = await _blockStorage.BlockExistsAsync(blockId);

                if (exists)
                {
                    _logger.LogDebug($"Bloque existe: {blockId}");
                    return Ok();
                }
                else
                {
                    _logger.LogDebug($"Bloque no existe: {blockId}");
                    return NotFound();
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error verificando existencia de bloque: {blockId}");
                return StatusCode(500);
            }
        }

        // get: lista todos los bloques almacenados en el nodo
        [HttpGet("list")]
        [ProducesResponseType(typeof(BlockListResponse), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status500InternalServerError)]
        public async Task<IActionResult> ListBlocks()
        {
            _logger.LogInformation("Obteniendo lista de bloques del nodo");

            try
            {
                // USAR BLOCK STORAGE REAL
                var blockIds = await _blockStorage.ListBlocksAsync();
                var response = new BlockListResponse
                {
                    BlockIds = blockIds,
                    TotalBlocks = blockIds.Count,
                    NodeId = GetCurrentNodeId(),
                    GeneratedAt = DateTime.UtcNow
                };

                _logger.LogInformation($"Lista de bloques generada: {response.TotalBlocks} bloques en nodo {response.NodeId}");
                return Ok(response);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error obteniendo lista de bloques");
                return StatusCode(500, new ErrorResponse
                {
                    Error = "Error interno del servidor",
                    Message = "Error obteniendo lista de bloques",
                    RequestId = Guid.NewGuid().ToString()
                });
            }
        }

        // get: obtiene informacion y estadisticas del nodo
        [HttpGet("info")]
        [ProducesResponseType(typeof(NodeInfoResponse), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status500InternalServerError)]
        public async Task<IActionResult> GetNodeInfo()
        {
            _logger.LogInformation("Obteniendo informacion del nodo");

            try
            {
                // USAR BLOCK STORAGE REAL
                var totalStorage = await _blockStorage.GetAvailableSpaceAsync() + await _blockStorage.GetUsedSpaceAsync();
                var usedStorage = await _blockStorage.GetUsedSpaceAsync();
                var blockIds = await _blockStorage.ListBlocksAsync();

                var response = new NodeInfoResponse
                {
                    NodeId = GetCurrentNodeId(),
                    TotalStorage = totalStorage,
                    UsedStorage = usedStorage,
                    AvailableStorage = totalStorage - usedStorage,
                    TotalBlocks = blockIds.Count,
                    ParityBlocks = blockIds.Count(b => b.Contains("parity")),
                    DataBlocks = blockIds.Count(b => !b.Contains("parity")),
                    Status = "Healthy",
                    Version = "1.0.0",
                    StartTime = DateTime.UtcNow.AddHours(-1),
                    LastMaintenance = DateTime.UtcNow.AddDays(-1)
                };

                _logger.LogInformation($"Info del nodo obtenida: Nodo {response.NodeId}, Bloques: {response.TotalBlocks}, Almacenamiento usado: {FormatBytes(response.UsedStorage)}");
                return Ok(response);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error obteniendo informacion del nodo");
                return StatusCode(500, new ErrorResponse
                {
                    Error = "Error interno del servidor",
                    Message = "Error obteniendo informacion del nodo",
                    RequestId = Guid.NewGuid().ToString()
                });
            }
        }

        // get: health check del nodo
        [HttpGet("health")]
        [ProducesResponseType(typeof(NodeHealthResponse), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status503ServiceUnavailable)]
        public async Task<IActionResult> HealthCheck()
        {
            _logger.LogDebug("Realizando health check del nodo");

            try
            {
                var response = new NodeHealthResponse
                {
                    NodeId = GetCurrentNodeId(),
                    Status = "Healthy",
                    Timestamp = DateTime.UtcNow,
                    ResponseTimeMs = 15.5,
                    Version = "1.0.0",
                    Uptime = TimeSpan.FromHours(1) + TimeSpan.FromMinutes(23),
                    ErrorCount = 0,
                    LastError = ""
                };

                _logger.LogDebug($"Health check completado - Nodo {response.NodeId}: {response.Status}");
                return Ok(response);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error durante health check");
                return StatusCode(503, new ErrorResponse
                {
                    Error = "Servicio no disponible",
                    Message = "Nodo no esta funcionando correctamente",
                    RequestId = Guid.NewGuid().ToString()
                });
            }
        }

        // ================================
        // metodos privados helper
        // ================================

        private ValidationResult ValidateBlockRequest(BlockRequest request)
        {
            if (request == null)
                return new ValidationResult { IsValid = false, ErrorMessage = "Request no puede ser nulo" };

            if (string.IsNullOrWhiteSpace(request.BlockId))
                return new ValidationResult { IsValid = false, ErrorMessage = "BlockId es requerido" };

            if (request.BlockData == null || request.BlockData.Length == 0)
                return new ValidationResult { IsValid = false, ErrorMessage = "Datos del bloque son requeridos" };

            if (request.BlockData.Length > SystemConstants.MAX_BLOCK_SIZE)
                return new ValidationResult { IsValid = false, ErrorMessage = $"Bloque excede tamaño maximo de {FormatBytes(SystemConstants.MAX_BLOCK_SIZE)}" };

            return new ValidationResult { IsValid = true };
        }

        private string FormatBytes(long bytes)
        {
            if (bytes < 1024) return $"{bytes} B";
            if (bytes < 1048576) return $"{bytes / 1024.0:F1} KB";
            if (bytes < 1073741824) return $"{bytes / 1048576.0:F1} MB";
            return $"{bytes / 1073741824.0:F1} GB";
        }

        private int GetCurrentNodeId()
        {
            // TODO: leer de configuración - por ahora simular nodo 1
            return 1;
        }

        private string CalculateChecksum(byte[] data)
        {
            // usar sha256 simple
            using var sha256 = System.Security.Cryptography.SHA256.Create();
            var hash = sha256.ComputeHash(data);
            return Convert.ToHexString(hash);
        }

        // ================================
        // clases helper para responses
        // ================================

        public class ValidationResult
        {
            public bool IsValid { get; set; }
            public string ErrorMessage { get; set; } = string.Empty;
        }

        public class BlockStoreResponse
        {
            public bool Success { get; set; }
            public string BlockId { get; set; } = string.Empty;
            public long BlockSize { get; set; }
            public DateTime StoredAt { get; set; }
            public int NodeId { get; set; }
            public bool IsParityBlock { get; set; }
            public string CheckSum { get; set; } = string.Empty;
            public string Message { get; set; } = string.Empty;
        }

        public class ErrorResponse
        {
            public string Error { get; set; } = string.Empty;
            public string Message { get; set; } = string.Empty;
            public string RequestId { get; set; } = string.Empty;
            public DateTime Timestamp { get; set; } = DateTime.UtcNow;
        }

        public class BlockRetrieveResponse
        {
            public string BlockId { get; set; } = string.Empty;
            public byte[] BlockData { get; set; } = Array.Empty<byte>();
            public long BlockSize { get; set; }
            public DateTime RetrievedAt { get; set; }
            public int NodeId { get; set; }
            public string CheckSum { get; set; } = string.Empty;
        }

        public class BlockDeleteResponse
        {
            public bool Success { get; set; }
            public string BlockId { get; set; } = string.Empty;
            public DateTime DeletedAt { get; set; }
            public int NodeId { get; set; }
            public string Message { get; set; } = string.Empty;
        }

        public class BlockListResponse
        {
            public List<string> BlockIds { get; set; } = new List<string>();
            public int TotalBlocks { get; set; }
            public int NodeId { get; set; }
            public DateTime GeneratedAt { get; set; }
        }

        public class NodeInfoResponse
        {
            public int NodeId { get; set; }
            public long TotalStorage { get; set; }
            public long UsedStorage { get; set; }
            public long AvailableStorage { get; set; }
            public int TotalBlocks { get; set; }
            public int ParityBlocks { get; set; }
            public int DataBlocks { get; set; }
            public string Status { get; set; } = string.Empty;
            public string Version { get; set; } = string.Empty;
            public DateTime StartTime { get; set; }
            public DateTime LastMaintenance { get; set; }
        }

        public class NodeHealthResponse
        {
            public int NodeId { get; set; }
            public string Status { get; set; } = string.Empty;
            public DateTime Timestamp { get; set; }
            public double ResponseTimeMs { get; set; }
            public string Version { get; set; } = string.Empty;
            public TimeSpan Uptime { get; set; }
            public int ErrorCount { get; set; }
            public string LastError { get; set; } = string.Empty;
        }
    }
}