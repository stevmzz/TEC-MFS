using Microsoft.AspNetCore.Mvc;
using TecMFS.Common.DTOs;
using TecMFS.Common.Models;
using TecMFS.Common.Constants;
using TecMFS.Common.Interfaces;
using System.ComponentModel.DataAnnotations;

namespace TecMFS.Controller.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Produces("application/json")]
    public class FilesController : ControllerBase
    {
        private readonly ILogger<FilesController> _logger;
        private readonly IRaidManager _raidManager;

        public FilesController(ILogger<FilesController> logger, IRaidManager raidManager)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _raidManager = raidManager ?? throw new ArgumentNullException(nameof(raidManager));
        }

        // post: sube un archivo pdf al sistema raid
        [HttpPost("upload")]
        [ProducesResponseType(typeof(UploadResponse), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status500InternalServerError)]
        public async Task<IActionResult> UploadFile([FromBody] UploadRequest request)
        {
            _logger.LogInformation($"Iniciando carga de archivo: {request.FileName}, Tamaño: {request.FileSizeFormatted}");

            try
            {
                var validationResults = ValidateUploadRequest(request);
                if (!validationResults.IsValid)
                {
                    _logger.LogWarning($"Validacion fallida para archivo: {request.FileName} - {validationResults.ErrorMessage}");
                    return BadRequest(new ErrorResponse
                    {
                        Error = "Validacion fallida",
                        Message = validationResults.ErrorMessage,
                        RequestId = Guid.NewGuid().ToString()
                    });
                }

                // USAR RAID MANAGER REAL
                var success = await _raidManager.StoreFileAsync(request);

                if (success)
                {
                    var response = new UploadResponse
                    {
                        Success = true,
                        FileId = Guid.NewGuid().ToString(),
                        FileName = request.FileName,
                        FileSize = request.FileSize,
                        UploadedAt = DateTime.UtcNow,
                        Message = "Archivo subido correctamente",
                        BlocksCreated = CalculateBlockCount(request.FileSize),
                        NodesUsed = SystemConstants.RAID_TOTAL_NODES
                    };

                    _logger.LogInformation($"Archivo cargado exitosamente: {request.FileName}, Bloques: {response.BlocksCreated}");
                    return Ok(response);
                }
                else
                {
                    return StatusCode(500, new ErrorResponse
                    {
                        Error = "Error almacenando archivo",
                        Message = "No se pudo almacenar el archivo en el sistema RAID",
                        RequestId = Guid.NewGuid().ToString()
                    });
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error critico durante carga de archivo: {request.FileName}");
                return StatusCode(500, new ErrorResponse
                {
                    Error = "Error interno del servidor",
                    Message = "Error procesando la carga del archivo",
                    RequestId = Guid.NewGuid().ToString()
                });
            }
        }

        // get: descarga un archivo del sistema raid por su nombre
        [HttpGet("download/{fileName}")]
        [ProducesResponseType(typeof(FileContentResult), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status404NotFound)]
        [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status500InternalServerError)]
        public async Task<IActionResult> DownloadFile([FromRoute] string fileName)
        {
            _logger.LogInformation($"Iniciando descarga de archivo: {fileName}");

            try
            {
                if (string.IsNullOrEmpty(fileName) || !fileName.EndsWith(".pdf", StringComparison.OrdinalIgnoreCase))
                {
                    _logger.LogWarning($"Nombre de archivo invalido para descarga: {fileName}");
                    return BadRequest(new ErrorResponse
                    {
                        Error = "Nombre invalido",
                        Message = "El nombre del archivo debe ser valido y terminar en .pdf",
                        RequestId = Guid.NewGuid().ToString()
                    });
                }

                // USAR RAID MANAGER REAL
                var fileData = await _raidManager.RetrieveFileAsync(fileName);

                if (fileData == null)
                {
                    _logger.LogWarning($"Archivo no encontrado: {fileName}");
                    return NotFound(new ErrorResponse
                    {
                        Error = "Archivo no encontrado",
                        Message = $"El archivo {fileName} no existe en el sistema",
                        RequestId = Guid.NewGuid().ToString()
                    });
                }

                _logger.LogInformation($"Archivo descargado exitosamente: {fileName}, Tamaño: {fileData.Length} bytes");
                return File(fileData, "application/pdf", fileName);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error critico durante descarga de archivo: {fileName}");
                return StatusCode(500, new ErrorResponse
                {
                    Error = "Error interno del servidor",
                    Message = "Error procesando la descarga del archivo",
                    RequestId = Guid.NewGuid().ToString()
                });
            }
        }

        // get: lista todos los archivos disponibles en el sistema
        [HttpGet("list")]
        [ProducesResponseType(typeof(FileListResponse), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status500InternalServerError)]
        public async Task<IActionResult> ListFiles()
        {
            _logger.LogInformation("Obteniendo lista de archivos del sistema RAID");

            try
            {
                // USAR RAID MANAGER REAL
                var files = await _raidManager.ListFilesAsync();
                var response = new FileListResponse
                {
                    Files = files,
                    TotalCount = files.Count,
                    TotalSize = files.Sum(f => f.FileSize),
                    GeneratedAt = DateTime.UtcNow
                };

                _logger.LogInformation($"Lista de archivos generada exitosamente: {response.TotalCount} archivos, Tamaño total: {FormatBytes(response.TotalSize)}");
                return Ok(response);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error critico obteniendo lista de archivos");
                return StatusCode(500, new ErrorResponse
                {
                    Error = "Error interno del servidor",
                    Message = "Error obteniendo lista de archivos",
                    RequestId = Guid.NewGuid().ToString()
                });
            }
        }

        // get: busca archivos por nombre o termino de busqueda
        [HttpGet("search")]
        [ProducesResponseType(typeof(FileListResponse), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status500InternalServerError)]
        [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status400BadRequest)]
        public async Task<IActionResult> SearchFiles([FromQuery] string query)
        {
            _logger.LogInformation($"Iniciando busqueda de archivos con termino: {query}");

            try
            {
                if (string.IsNullOrWhiteSpace(query) || query.Length < 2)
                {
                    _logger.LogWarning($"Termino de busqueda invalido: {query}");
                    return BadRequest(new ErrorResponse
                    {
                        Error = "Termino invalido",
                        Message = "El termino de busqueda debe tener al menos 2 caracteres",
                        RequestId = Guid.NewGuid().ToString()
                    });
                }

                // USAR RAID MANAGER REAL
                var files = await _raidManager.SearchFilesAsync(query);
                var response = new FileListResponse
                {
                    Files = files,
                    TotalCount = files.Count,
                    TotalSize = files.Sum(f => f.FileSize),
                    GeneratedAt = DateTime.UtcNow
                };

                _logger.LogInformation($"Busqueda completada: {response.TotalCount} archivos encontrados para termino '{query}'");
                return Ok(response);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error critico durante busqueda de archivos con termino: {query}");
                return StatusCode(500, new ErrorResponse
                {
                    Error = "Error interno del servidor",
                    Message = "Error procesando busqueda de archivos",
                    RequestId = Guid.NewGuid().ToString()
                });
            }
        }

        // get: obtiene informacion de un archivo especifico
        [HttpGet("info/{fileName}")]
        [ProducesResponseType(typeof(FileMetadata), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status404NotFound)]
        [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status500InternalServerError)]
        public async Task<IActionResult> GetFileInfo([FromRoute] string fileName)
        {
            _logger.LogInformation($"Obteniendo informacion del archivo: {fileName}");

            try
            {
                if (string.IsNullOrWhiteSpace(fileName))
                {
                    _logger.LogWarning($"Nombre de archivo vacio para obtencion de info");
                    return BadRequest(new ErrorResponse
                    {
                        Error = "Nombre invalido",
                        Message = "El nombre del archivo no puede estar vacio",
                        RequestId = Guid.NewGuid().ToString()
                    });
                }

                // USAR RAID MANAGER REAL
                var metadata = await _raidManager.GetFileMetadataAsync(fileName);

                if (metadata == null)
                {
                    _logger.LogWarning($"Archivo no encontrado para info: {fileName}");
                    return NotFound(new ErrorResponse
                    {
                        Error = "Archivo no encontrado",
                        Message = $"El archivo {fileName} no existe en el sistema",
                        RequestId = Guid.NewGuid().ToString()
                    });
                }

                _logger.LogInformation($"Informacion de archivo obtenida exitosamente: {fileName}, Bloques: {metadata.Blocks.Count}");
                return Ok(metadata);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error critico obteniendo informacion de archivo: {fileName}");
                return StatusCode(500, new ErrorResponse
                {
                    Error = "Error interno del servidor",
                    Message = "Error obteniendo informacion del archivo",
                    RequestId = Guid.NewGuid().ToString()
                });
            }
        }

        // delete: elimina un archivo del sistema raid
        [HttpDelete("delete/{fileName}")]
        [ProducesResponseType(typeof(DeleteResponse), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status404NotFound)]
        [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status500InternalServerError)]
        public async Task<IActionResult> DeleteFile([FromRoute] string fileName)
        {
            _logger.LogInformation($"Iniciando eliminacion de archivo: {fileName}");

            try
            {
                if (string.IsNullOrWhiteSpace(fileName))
                {
                    _logger.LogWarning($"Nombre de archivo vacio para eliminacion");
                    return BadRequest(new ErrorResponse
                    {
                        Error = "Nombre invalido",
                        Message = "El nombre del archivo no puede estar vacio",
                        RequestId = Guid.NewGuid().ToString()
                    });
                }

                // USAR RAID MANAGER REAL
                var success = await _raidManager.DeleteFileAsync(fileName);

                if (!success)
                {
                    _logger.LogWarning($"Archivo no encontrado para eliminacion: {fileName}");
                    return NotFound(new ErrorResponse
                    {
                        Error = "Archivo no encontrado",
                        Message = $"El archivo {fileName} no existe en el sistema",
                        RequestId = Guid.NewGuid().ToString()
                    });
                }

                var result = new DeleteResponse
                {
                    Success = true,
                    FileName = fileName,
                    BlocksDeleted = CalculateBlockCount(1024 * 100), // estimacion
                    DeletedAt = DateTime.UtcNow,
                    Message = "Archivo eliminado exitosamente del sistema RAID"
                };

                _logger.LogInformation($"Archivo eliminado exitosamente: {fileName}");
                return Ok(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error critico durante eliminacion de archivo: {fileName}");
                return StatusCode(500, new ErrorResponse
                {
                    Error = "Error interno del servidor",
                    Message = "Error procesando eliminacion del archivo",
                    RequestId = Guid.NewGuid().ToString()
                });
            }
        }

        // ================================
        // metodos privados helper (sin cambios)
        // ================================

        private ValidationResult ValidateUploadRequest(UploadRequest request)
        {
            if (request == null)
                return new ValidationResult { IsValid = false, ErrorMessage = "Request no puede ser nulo" };

            if (string.IsNullOrWhiteSpace(request.FileName))
                return new ValidationResult { IsValid = false, ErrorMessage = "Nombre de archivo requerido" };

            if (!request.IsValidPdf)
                return new ValidationResult { IsValid = false, ErrorMessage = "Solo archivos PDF son permitidos" };

            if (request.FileSize == 0)
                return new ValidationResult { IsValid = false, ErrorMessage = "Archivo no puede estar vacio" };

            if (request.FileSize > SystemConstants.MAX_FILE_SIZE)
                return new ValidationResult { IsValid = false, ErrorMessage = $"Archivo excede tamaño maximo de {FormatBytes(SystemConstants.MAX_FILE_SIZE)}" };

            return new ValidationResult { IsValid = true };
        }

        private string FormatBytes(long bytes)
        {
            if (bytes < 1024) return $"{bytes} B";
            if (bytes < 1048576) return $"{bytes / 1024.0:F1} KB";
            return $"{bytes / 1048576.0:F1} MB";
        }

        private int CalculateBlockCount(long fileSize)
        {
            return SystemConstants.CalculateBlockCount(fileSize);
        }

        // ================================
        // clases helper para responses
        // ================================

        public class UploadResponse
        {
            public bool Success { get; set; }
            public string FileId { get; set; } = string.Empty;
            public string FileName { get; set; } = string.Empty;
            public long FileSize { get; set; }
            public string Message { get; set; } = string.Empty;
            public DateTime UploadedAt { get; set; }
            public int BlocksCreated { get; set; }
            public int NodesUsed { get; set; }
        }

        public class ErrorResponse
        {
            public string Error { get; set; } = string.Empty;
            public string Message { get; set; } = string.Empty;
            public string RequestId { get; set; } = string.Empty;
            public DateTime Timestamp { get; set; } = DateTime.UtcNow;
        }

        public class ValidationResult
        {
            public bool IsValid { get; set; }
            public string ErrorMessage { get; set; } = string.Empty;
        }

        public class FileListResponse
        {
            public List<FileMetadata> Files { get; set; } = new List<FileMetadata>();
            public int TotalCount { get; set; }
            public long TotalSize { get; set; }
            public DateTime GeneratedAt { get; set; }
        }

        public class DeleteResponse
        {
            public bool Success { get; set; }
            public string FileName { get; set; } = string.Empty;
            public int BlocksDeleted { get; set; }
            public DateTime DeletedAt { get; set; }
            public string Message { get; set; } = string.Empty;
        }
    }
}