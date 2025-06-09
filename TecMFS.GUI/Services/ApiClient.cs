using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using System.IO;
using System.Net;
using Newtonsoft.Json;
using TecMFS.Common.DTOs;
using TecMFS.Common.Models;
using TecMFS.Common.Constants;

namespace TecMFS.GUI.Services
{
    // cliente para manejar comunicacion entre controller y gui
    public class ApiClient : IDisposable
    {
        private readonly HttpClient _httpClient;
        private readonly string _baseUrl;
        private bool _disposed = false;

        public string BaseUrl => _baseUrl; // url base del controller al que se conecta
        public TimeSpan Timeout => _httpClient.Timeout; // timeout configurado para las peticiones htt
        public bool IsDisposed => _disposed; // índica si el cliente ha sido disposed

        // constructor
        public ApiClient(string controllerBaseUrl = "http://localhost:5000")
        {
            _baseUrl = controllerBaseUrl.TrimEnd('/');
            _httpClient = new HttpClient
            {
                Timeout = TimeSpan.FromSeconds(60), // establecer timeout de 60 segundos
                BaseAddress = new Uri(_baseUrl)
            };

            // configurar headers por defecto
            _httpClient.DefaultRequestHeaders.Add("User-Agent", "TecMFS-GUI/1.0");
            _httpClient.DefaultRequestHeaders.Add("Accept", "application/json");

            Console.WriteLine($"ApiClient inicializado para: {_baseUrl}");
        }

        // ================================
        // operaciones basicas
        // ================================

        // verifica si el controller está disponible
        public async Task<bool> IsControllerAvailableAsync()
        {
            try
            {
                var response = await _httpClient.GetAsync(ApiEndpoints.STATUS_HEALTH);
                var isAvailable = response.IsSuccessStatusCode;

                Console.WriteLine($"Controller disponible: {isAvailable}");
                return isAvailable;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error verificando controller: {ex.Message}");
                return false;
            }
        }



        // obtiene informacion basica del sistema
        public async Task<StatusResponse?> GetSystemStatusAsync()
        {
            try
            {
                var response = await _httpClient.GetAsync(ApiEndpoints.STATUS_RAID);

                if (response.IsSuccessStatusCode)
                {
                    var json = await response.Content.ReadAsStringAsync();
                    var status = JsonConvert.DeserializeObject<StatusResponse>(json);

                    Console.WriteLine($"Estado del sistema obtenido: {status?.Status}");
                    return status;
                }

                Console.WriteLine($"Error obteniendo estado: {response.StatusCode}");
                return null;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error en GetSystemStatus: {ex.Message}");
                return null;
            }
        }

        // ================================
        // operaciones de archivos
        // ================================

        // obtiene lista de archivos del servidor
        public async Task<List<FileMetadata>?> GetFileListAsync()
        {
            try
            {
                Console.WriteLine("Obteniendo lista de archivos del servidor...");
                var response = await _httpClient.GetAsync(ApiEndpoints.FILES_LIST);

                if (response.IsSuccessStatusCode)
                {
                    var json = await response.Content.ReadAsStringAsync();

                    // el controller retorna un FileListResponse, extraemos solo los archivos
                    var fileListResponse = JsonConvert.DeserializeObject<FileListResponse>(json);
                    var files = fileListResponse?.Files;

                    Console.WriteLine($"{files?.Count ?? 0} archivos obtenidos del servidor");
                    return files;
                }

                Console.WriteLine($"Error obteniendo archivos: {response.StatusCode}");
                return null;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error en GetFileList: {ex.Message}");
                return null;
            }
        }



        // busca archivos por query
        public async Task<List<FileMetadata>?> SearchFilesAsync(string query)
        {
            if (string.IsNullOrWhiteSpace(query) || query.Length < 2)
            {
                Console.WriteLine("Query de búsqueda debe tener al menos 2 caracteres");
                return null;
            }

            try
            {
                Console.WriteLine($"Buscando archivos para '{query}' en el servidor...");
                var searchUrl = ApiEndpoints.GetFileSearchUrl(query);
                var response = await _httpClient.GetAsync(searchUrl);

                if (response.IsSuccessStatusCode)
                {
                    var json = await response.Content.ReadAsStringAsync();
                    var fileListResponse = JsonConvert.DeserializeObject<FileListResponse>(json);
                    var files = fileListResponse?.Files;

                    Console.WriteLine($"{files?.Count ?? 0} resultados encontrados para '{query}'");
                    return files;
                }

                Console.WriteLine($"Error en búsqueda: {response.StatusCode}");
                return null;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error en SearchFiles: {ex.Message}");
                return null;
            }
        }



        // obtiene informacion de un archivo especifico
        public async Task<FileMetadata?> GetFileInfoAsync(string fileName)
        {
            if (string.IsNullOrWhiteSpace(fileName))
            {
                Console.WriteLine("Nombre de archivo no puede estar vacío");
                return null;
            }

            try
            {
                Console.WriteLine($"Obteniendo info de '{fileName}' del servidor...");
                var infoUrl = ApiEndpoints.GetFileInfoUrl(fileName);
                var response = await _httpClient.GetAsync(infoUrl);

                if (response.IsSuccessStatusCode)
                {
                    var json = await response.Content.ReadAsStringAsync();
                    var fileInfo = JsonConvert.DeserializeObject<FileMetadata>(json);

                    Console.WriteLine($"Info de '{fileName}' obtenida exitosamente");
                    return fileInfo;
                }

                if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
                {
                    Console.WriteLine($"Archivo '{fileName}' no encontrado");
                    return null;
                }

                Console.WriteLine($"Error obteniendo info: {response.StatusCode}");
                return null;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error en GetFileInfo: {ex.Message}");
                return null;
            }
        }



        // elimina un archivo del sistema
        public async Task<bool> DeleteFileAsync(string fileName)
        {
            if (string.IsNullOrWhiteSpace(fileName))
            {
                Console.WriteLine("Nombre de archivo no puede estar vacío");
                return false;
            }

            try
            {
                Console.WriteLine($"Eliminando archivo '{fileName}'...");
                var deleteUrl = ApiEndpoints.GetFileDeleteUrl(fileName);
                var response = await _httpClient.DeleteAsync(deleteUrl);

                if (response.IsSuccessStatusCode)
                {
                    Console.WriteLine($"Archivo '{fileName}' eliminado exitosamente");
                    return true;
                }

                Console.WriteLine($"Error eliminando archivo: {response.StatusCode}");
                return false;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error en DeleteFile: {ex.Message}");
                return false;
            }
        }

        // ================================
        // UPLOAD CON PROGRESS TRACKING
        // ================================

        // sube un archivo con tracking de progreso
        public async Task<UploadResult?> UploadFileAsync(string filePath, IProgress<ProgressEventArgs>? progress = null)
        {
            if (!File.Exists(filePath))
            {
                Console.WriteLine($"Archivo no encontrado: {filePath}");
                return null;
            }

            var fileName = Path.GetFileName(filePath);
            var fileBytes = await File.ReadAllBytesAsync(filePath);

            return await UploadFileAsync(fileName, fileBytes, progress);
        }

        // sube un archivo desde bytes con tracking de progreso
        public async Task<UploadResult?> UploadFileAsync(string fileName, byte[] fileData, IProgress<ProgressEventArgs>? progress = null)
        {
            if (string.IsNullOrWhiteSpace(fileName))
            {
                Console.WriteLine("Nombre de archivo no puede estar vacío");
                return null;
            }

            if (fileData == null || fileData.Length == 0)
            {
                Console.WriteLine("Datos del archivo no pueden estar vacíos");
                return null;
            }

            if (!fileName.EndsWith(".pdf", StringComparison.OrdinalIgnoreCase))
            {
                Console.WriteLine("Solo se permiten archivos PDF");
                return null;
            }

            // verificar tamaño máximo
            if (fileData.Length > SystemConstants.MAX_FILE_SIZE)
            {
                Console.WriteLine($"Archivo excede tamaño máximo de {FormatBytes(SystemConstants.MAX_FILE_SIZE)}");
                return null;
            }

            try
            {
                Console.WriteLine($"Iniciando upload de '{fileName}' ({FormatBytes(fileData.Length)})...");

                // reportar progreso inicial
                progress?.Report(new ProgressEventArgs
                {
                    FileName = fileName,
                    BytesTransferred = 0,
                    TotalBytes = fileData.Length,
                    Speed = "Iniciando..."
                });

                // crear request de upload
                var uploadRequest = new UploadRequest
                {
                    FileName = fileName,
                    FileData = fileData,
                    ContentType = "application/pdf",
                    ClientId = Environment.MachineName,
                    UploadedAt = DateTime.UtcNow
                };

                // serializar a json
                var json = JsonConvert.SerializeObject(uploadRequest);
                var content = new StringContent(json, Encoding.UTF8, "application/json");

                // reportar progreso de serialización
                progress?.Report(new ProgressEventArgs
                {
                    FileName = fileName,
                    BytesTransferred = fileData.Length / 2, // simular 50% por serialización
                    TotalBytes = fileData.Length,
                    Speed = "Serializando..."
                });

                var startTime = DateTime.UtcNow;

                // enviar request
                var response = await _httpClient.PostAsync(ApiEndpoints.FILES_UPLOAD, content);

                var duration = DateTime.UtcNow - startTime;
                var speed = fileData.Length / Math.Max(duration.TotalSeconds, 0.1); // evitar división por cero

                if (response.IsSuccessStatusCode)
                {
                    var responseJson = await response.Content.ReadAsStringAsync();
                    var uploadResponse = JsonConvert.DeserializeObject<UploadResponse>(responseJson);

                    var result = new UploadResult
                    {
                        Success = true,
                        FileName = fileName,
                        FileSize = fileData.Length,
                        Duration = duration,
                        Speed = FormatSpeed(speed),
                        Message = uploadResponse?.Message ?? "Upload exitoso",
                        FileId = uploadResponse?.FileId ?? "unknown"
                    };

                    Console.WriteLine($"Upload completado: '{fileName}' en {duration.TotalSeconds:F1}s ({result.Speed})");

                    // reporte final de progreso (100%)
                    progress?.Report(new ProgressEventArgs
                    {
                        FileName = fileName,
                        BytesTransferred = fileData.Length,
                        TotalBytes = fileData.Length,
                        Speed = result.Speed
                    });

                    return result;
                }
                else
                {
                    var errorContent = await response.Content.ReadAsStringAsync();
                    Console.WriteLine($"Error en upload: {response.StatusCode} - {errorContent}");
                    return new UploadResult
                    {
                        Success = false,
                        FileName = fileName,
                        Message = $"Error {response.StatusCode}: {errorContent}"
                    };
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error en UploadFile: {ex.Message}");
                return new UploadResult
                {
                    Success = false,
                    FileName = fileName,
                    Message = $"Error: {ex.Message}"
                };
            }
        }

        // ================================
        // DOWNLOAD CON PROGRESS TRACKING
        // ================================

        // descarga un archivo con tracking de progreso
        public async Task<DownloadResult?> DownloadFileAsync(string fileName, string savePath, IProgress<ProgressEventArgs>? progress = null)
        {
            if (string.IsNullOrWhiteSpace(fileName))
            {
                Console.WriteLine("Nombre de archivo no puede estar vacío");
                return null;
            }

            if (string.IsNullOrWhiteSpace(savePath))
            {
                Console.WriteLine("Ruta de destino no puede estar vacía");
                return null;
            }

            try
            {
                Console.WriteLine($"Iniciando download de '{fileName}'...");
                var downloadUrl = ApiEndpoints.GetFileDownloadUrl(fileName);

                var startTime = DateTime.UtcNow;
                var response = await _httpClient.GetAsync(downloadUrl, HttpCompletionOption.ResponseHeadersRead);

                if (response.IsSuccessStatusCode)
                {
                    var totalBytes = response.Content.Headers.ContentLength ?? 0;
                    var bytesRead = 0L;

                    using var stream = await response.Content.ReadAsStreamAsync();
                    using var fileStream = new FileStream(savePath, FileMode.Create, FileAccess.Write, FileShare.None);

                    var buffer = new byte[8192]; // buffer de 8KB
                    int read;

                    while ((read = await stream.ReadAsync(buffer, 0, buffer.Length)) > 0)
                    {
                        await fileStream.WriteAsync(buffer, 0, read);
                        bytesRead += read;

                        // reportar progreso
                        progress?.Report(new ProgressEventArgs
                        {
                            FileName = fileName,
                            BytesTransferred = bytesRead,
                            TotalBytes = totalBytes,
                            Speed = "Descargando..."
                        });
                    }

                    var duration = DateTime.UtcNow - startTime;
                    var speed = bytesRead / Math.Max(duration.TotalSeconds, 0.1);

                    var result = new DownloadResult
                    {
                        Success = true,
                        FileName = fileName,
                        SavePath = savePath,
                        FileSize = bytesRead,
                        Duration = duration,
                        Speed = FormatSpeed(speed),
                        Message = "Download exitoso"
                    };

                    Console.WriteLine($"Download completado: '{fileName}' en {duration.TotalSeconds:F1}s ({result.Speed})");

                    // reporte final de progreso
                    progress?.Report(new ProgressEventArgs
                    {
                        FileName = fileName,
                        BytesTransferred = bytesRead,
                        TotalBytes = totalBytes,
                        Speed = result.Speed
                    });

                    return result;
                }
                else
                {
                    Console.WriteLine($"Error en download: {response.StatusCode}");
                    return new DownloadResult
                    {
                        Success = false,
                        FileName = fileName,
                        Message = $"Error: {response.StatusCode}"
                    };
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error en DownloadFile: {ex.Message}");
                return new DownloadResult
                {
                    Success = false,
                    FileName = fileName,
                    Message = $"Error: {ex.Message}"
                };
            }
        }

        // ================================
        // metodos auxiliares
        // ================================

        // formatea bytes en formato legible
        private string FormatBytes(long bytes)
        {
            if (bytes < 1024) return $"{bytes} B";
            if (bytes < 1048576) return $"{bytes / 1024.0:F1} KB";
            if (bytes < 1073741824) return $"{bytes / 1048576.0:F1} MB";
            return $"{bytes / 1073741824.0:F1} GB";
        }



        // formatea velocidad de transferencia
        private string FormatSpeed(double bytesPerSecond)
        {
            if (bytesPerSecond < 1024) return $"{bytesPerSecond:F1} B/s";
            if (bytesPerSecond < 1048576) return $"{bytesPerSecond / 1024.0:F1} KB/s";
            return $"{bytesPerSecond / 1048576.0:F1} MB/s";
        }



        // liberar recursos
        public void Dispose()
        {
            _httpClient?.Dispose();
            Console.WriteLine("ApiClient disposed");
        }

        // ================================
        // clases helper
        // ================================

        // argumentos para eventos de progreso
        public class ProgressEventArgs : EventArgs
        {
            public string FileName { get; set; } = string.Empty;
            public long BytesTransferred { get; set; }
            public long TotalBytes { get; set; }
            public double PercentageComplete => TotalBytes > 0 ?
                (double)BytesTransferred / TotalBytes * 100 : 0;
            public string Speed { get; set; } = string.Empty;
        }

        // resultado de upload
        public class UploadResult
        {
            public bool Success { get; set; }
            public string FileName { get; set; } = string.Empty;
            public long FileSize { get; set; }
            public TimeSpan Duration { get; set; }
            public string Speed { get; set; } = string.Empty;
            public string Message { get; set; } = string.Empty;
            public string FileId { get; set; } = string.Empty;
        }

        // resultado de download
        public class DownloadResult
        {
            public bool Success { get; set; }
            public string FileName { get; set; } = string.Empty;
            public string SavePath { get; set; } = string.Empty;
            public long FileSize { get; set; }
            public TimeSpan Duration { get; set; }
            public string Speed { get; set; } = string.Empty;
            public string Message { get; set; } = string.Empty;
        }

        // response del controller para lista de archivos
        public class FileListResponse
        {
            public List<FileMetadata> Files { get; set; } = new List<FileMetadata>();
            public int TotalCount { get; set; }
            public long TotalSize { get; set; }
            public DateTime GeneratedAt { get; set; }
        }

        // response del controller para upload
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

    }

    
}
