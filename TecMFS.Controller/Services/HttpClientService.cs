using System;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using System.Collections.Concurrent;
using Newtonsoft.Json;
using TecMFS.Common.Interfaces;
using TecMFS.Common.Constants;
using System.IO.Compression;

namespace TecMFS.Controller.Services
{
    // implementacion del servicio de comunicacion http entre componentes
    // maneja todas las operaciones http del sistema distribuido
    public class HttpClientService : IHttpClientService, IDisposable
    {
        private readonly ILogger<HttpClientService> _logger;
        private readonly ConcurrentDictionary<string, HttpClient> _clientPool; // pool de clientes por servidor
        private readonly HttpClientHandler _handler; // handler compartido para optimizacion
        private int _timeoutMs;
        private int _maxRetries;
        private int _retryDelayMs;
        private bool _compressionEnabled = true; // habilitar compresion por defecto

        // configuracion del pool de conexiones
        private readonly int _maxConnectionsPerServer = 10; // maximo conexiones simultaneas por servidor

        // constructor que configura el cliente http con valores por defecto
        public HttpClientService(ILogger<HttpClientService> logger)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _clientPool = new ConcurrentDictionary<string, HttpClient>(); // inicializar pool thread-safe
            _timeoutMs = SystemConstants.DEFAULT_REQUEST_TIMEOUT_MS;
            _maxRetries = SystemConstants.MAX_RETRY_ATTEMPTS;
            _retryDelayMs = 1000;

            // configurar handler con pool optimizado para reutilizacion de conexiones
            _handler = new HttpClientHandler()
            {
                MaxConnectionsPerServer = _maxConnectionsPerServer, // limitar conexiones simultaneas
                UseCookies = false // deshabilitar cookies para apis rest
            };

            _logger.LogInformation($"HttpClient service inicializado exitosamente con pool de conexiones habilitado - Conexiones maximas por servidor: {_maxConnectionsPerServer}");
        }

        // ================================
        // metodos publicos de la interface
        // ================================

        // envia request get y deserializa la respuesta
        public async Task<T?> SendGetAsync<T>(string url)
        {
            var client = GetOrCreateClient(url); // obtener cliente del pool
            _logger.LogInformation($"Iniciando GET request al endpoint: {url}");

            try
            {
                var response = await ExecuteWithRetryAsync(() => client.GetAsync(url));

                if (response.IsSuccessStatusCode)
                {
                    var content = await response.Content.ReadAsStringAsync();
                    var result = JsonConvert.DeserializeObject<T>(content);

                    _logger.LogInformation($"GET request completado exitosamente - Endpoint: {url}, Tipo de respuesta: {typeof(T).Name}");
                    return result;
                }
                else
                {
                    _logger.LogWarning($"GET request fallo con codigo de estado - Endpoint: {url}, Status: {response.StatusCode}");
                    return default(T);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error critico durante GET request - Endpoint: {url}");
                return default(T);
            }
        }



        // envia request post con datos y deserializa respuesta
        public async Task<T?> SendPostAsync<T>(string url, object data)
        {
            var client = GetOrCreateClient(url); // obtener cliente del pool
            _logger.LogInformation($"Iniciando POST request al endpoint: {url}");

            try
            {
                var json = JsonConvert.SerializeObject(data); // serializar datos a json
                var content = CreateCompressedContent(json, "application/json");

                var response = await ExecuteWithRetryAsync(async () => await client.PostAsync(url, content));

                if (response.IsSuccessStatusCode)
                {
                    var responseContent = await response.Content.ReadAsStringAsync();
                    var result = JsonConvert.DeserializeObject<T>(responseContent);

                    _logger.LogInformation($"POST request completado exitosamente - Endpoint: {url}, Tipo de respuesta: {typeof(T).Name}");
                    return result;
                }
                else
                {
                    _logger.LogWarning($"POST request fallo con codigo de estado - Endpoint: {url}, Status: {response.StatusCode}");
                    return default(T);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error critico durante POST request - Endpoint: {url}");
                return default(T);
            }
        }



        // envia request put con datos y deserializa respuesta
        public async Task<T?> SendPutAsync<T>(string url, object data)
        {
            var client = GetOrCreateClient(url); // obtener cliente del pool
            _logger.LogInformation($"Iniciando PUT request al endpoint: {url}");

            try
            {
                var json = JsonConvert.SerializeObject(data); // serializar datos a json
                var content = CreateCompressedContent(json, "application/json");

                var response = await ExecuteWithRetryAsync(async () => await client.PutAsync(url, content));

                if (response.IsSuccessStatusCode)
                {
                    var responseContent = await response.Content.ReadAsStringAsync();
                    var result = JsonConvert.DeserializeObject<T>(responseContent);

                    _logger.LogInformation($"PUT request completado exitosamente - Endpoint: {url}, Tipo de respuesta: {typeof(T).Name}");
                    return result;
                }
                else
                {
                    _logger.LogWarning($"PUT request fallo con codigo de estado - Endpoint: {url}, Status: {response.StatusCode}");
                    return default(T);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error critico durante PUT request - Endpoint: {url}");
                return default(T);
            }
        }



        // envia request delete
        public async Task<bool> SendDeleteAsync(string url)
        {
            var client = GetOrCreateClient(url); // obtener cliente del pool
            _logger.LogInformation($"Iniciando DELETE request al endpoint: {url}");

            try
            {
                var response = await ExecuteWithRetryAsync(async () => await client.DeleteAsync(url));
                var success = response.IsSuccessStatusCode;

                if (success)
                {
                    _logger.LogInformation($"DELETE request completado exitosamente - Endpoint: {url}");
                }
                else
                {
                    _logger.LogWarning($"DELETE request fallo con codigo de estado - Endpoint: {url}, Status: {response.StatusCode}");
                }

                return success;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error critico durante DELETE request - Endpoint: {url}");
                return false;
            }
        }



        // verifica si un endpoint responde (health check)
        public async Task<bool> CheckHealthAsync(string baseUrl)
        {
            var healthUrl = $"{baseUrl.TrimEnd('/')}/health"; // construir url de health check
            var client = GetOrCreateClient(healthUrl); // obtener cliente del pool

            _logger.LogDebug($"Realizando health check en servicio - Base URL: {healthUrl}");

            try
            {
                using var response = await client.GetAsync(healthUrl);
                var isHealthy = response.IsSuccessStatusCode;

                var status = isHealthy ? "operacional" : "no disponible";
                _logger.LogDebug($"Health check completado - Estado del servicio: {status}, URL: {healthUrl}");

                return isHealthy;
            }
            catch (Exception ex)
            {
                _logger.LogDebug(ex, $"Health check fallo debido a problemas de conectividad - URL: {healthUrl}");
                return false;
            }
        }



        // envia datos binarios (para archivos)
        public async Task<T?> SendBinaryDataAsync<T>(string url, byte[] data, string contentType)
        {
            var client = GetOrCreateClient(url); // obtener cliente del pool
            var sizeInKb = data.Length / 1024.0; // calcular tamaño en kb
            _logger.LogInformation($"Iniciando transferencia de datos binarios - Endpoint: {url}, Tamaño: {sizeInKb:F1} KB, Content-Type: {contentType}");

            try
            {
                var content = CreateCompressedBinaryContent(data, contentType);

                var response = await ExecuteWithRetryAsync(async () => await client.PostAsync(url, content));

                if (response.IsSuccessStatusCode)
                {
                    var responseContent = await response.Content.ReadAsStringAsync();
                    var result = JsonConvert.DeserializeObject<T>(responseContent);

                    _logger.LogInformation($"Transferencia de datos binarios completada exitosamente - Endpoint: {url}, Tamaño: {sizeInKb:F1} KB");
                    return result;
                }
                else
                {
                    _logger.LogWarning($"Transferencia de datos binarios fallo con codigo de estado - Endpoint: {url}, Status: {response.StatusCode}, Tamaño: {sizeInKb:F1} KB");
                    return default(T);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error critico durante transferencia de datos binarios - Endpoint: {url}, Tamaño: {sizeInKb:F1} KB");
                return default(T);
            }
        }



        // configura si usar compresion en transferencias
        public void SetCompressionEnabled(bool enabled)
        {
            _compressionEnabled = enabled;
            _logger.LogInformation($"Compresion de datos {(enabled ? "habilitada" : "deshabilitada")} para futuras transferencias");
        }

        // ================================
        // metodos de configuracion
        // ================================

        // configura timeout personalizado
        public void SetTimeout(int timeoutMs)
        {
            if (timeoutMs <= 0)
                throw new ArgumentException("Timeout debe ser mayor a cero", nameof(timeoutMs));

            _timeoutMs = timeoutMs;

            // solo aplicar timeout a nuevos clientes - los existentes no se pueden modificar
            var currentClientCount = _clientPool.Count;

            _logger.LogInformation($"Configuracion de timeout actualizada globalmente - Nuevo timeout: {timeoutMs}ms, Se aplicara a nuevos clientes (actuales: {currentClientCount})");
            _logger.LogWarning("Los clientes HTTP existentes mantendran su timeout original - Solo nuevos clientes usaran el nuevo timeout");
        }



        // configura reintentos automaticos
        public void SetRetryPolicy(int maxRetries, int delayMs)
        {
            if (maxRetries < 0)
                throw new ArgumentException("Reintentos maximos no puede ser negativo", nameof(maxRetries));
            if (delayMs < 0)
                throw new ArgumentException("Delay de reintentos no puede ser negativo", nameof(delayMs));

            _maxRetries = maxRetries;
            _retryDelayMs = delayMs;

            _logger.LogInformation($"Configuracion de politica de reintentos actualizada - Reintentos maximos: {maxRetries}, Delay entre reintentos: {delayMs}ms");
        }

        // ================================
        // metodos del pool de conexiones
        // ================================

        // obtiene o crea un cliente http para un servidor especifico
        private HttpClient GetOrCreateClient(string url)
        {
            var uri = new Uri(url);
            var serverKey = $"{uri.Scheme}://{uri.Host}:{uri.Port}"; // crear clave unica por servidor

            return _clientPool.GetOrAdd(serverKey, key =>
            {
                // crear nuevo cliente con configuracion optimizada
                var client = new HttpClient(_handler, disposeHandler: false)
                {
                    Timeout = TimeSpan.FromMilliseconds(_timeoutMs)
                };

                // configurar headers por defecto para apis rest
                client.DefaultRequestHeaders.Add("User-Agent", "TecMFS-HttpClient/1.0");
                client.DefaultRequestHeaders.Add("Accept", "application/json");
                client.DefaultRequestHeaders.Add("Connection", "keep-alive");

                _logger.LogInformation($"Nuevo cliente HTTP creado y agregado al pool de conexiones - Servidor: {serverKey}, Total de clientes: {_clientPool.Count}");
                return client;
            });
        }



        // obtiene estadisticas del pool de conexiones
        public int GetActiveConnectionCount()
        {
            return _clientPool.Count; // retornar numero de clientes activos
        }

        // ================================
        // metodos privados
        // ================================

        // ejecuta una operacion http con logica de reintentos
        private async Task<HttpResponseMessage> ExecuteWithRetryAsync(Func<Task<HttpResponseMessage>> operation)
        {
            Exception lastException = null;

            for (int attempt = 0; attempt <= _maxRetries; attempt++)
            {
                try
                {
                    var response = await operation();

                    // si es exitoso o es un error de cliente (4xx), no reintentar
                    if (response.IsSuccessStatusCode ||
                        ((int)response.StatusCode >= 400 && (int)response.StatusCode < 500))
                    {
                        if (attempt > 0)
                        {
                            _logger.LogInformation($"Operacion exitosa despues de reintentos - Intento exitoso: {attempt + 1}");
                        }
                        return response;
                    }

                    // error de servidor (5xx) - reintentar
                    lastException = new HttpRequestException($"Servidor retorno error de estado: {response.StatusCode}");
                    _logger.LogWarning($"Intento de reintento fallo con error de servidor - Intento: {attempt + 1}/{_maxRetries + 1}, Status: {response.StatusCode}");
                }
                catch (Exception ex)
                {
                    lastException = ex;
                    _logger.LogWarning(ex, $"Intento de reintento fallo con excepcion - Intento: {attempt + 1}/{_maxRetries + 1}");
                }

                // si no es el ultimo intento, esperar antes del siguiente
                if (attempt < _maxRetries)
                {
                    _logger.LogDebug($"Esperando antes del siguiente intento de reintento - Delay: {_retryDelayMs}ms");
                    await Task.Delay(_retryDelayMs);
                }
            }

            _logger.LogError($"Todos los intentos de reintento agotados sin exito - Total de intentos: {_maxRetries + 1}");
            throw lastException ?? new HttpRequestException("Operacion fallo despues de agotar todos los intentos de reintento");
        }



        // comprime datos usando gzip
        private byte[] CompressData(byte[] data)
        {
            using var output = new MemoryStream();
            using (var gzip = new GZipStream(output, CompressionMode.Compress))
            {
                gzip.Write(data, 0, data.Length);
            }
            return output.ToArray();
        }



        // descomprime datos gzip
        private byte[] DecompressData(byte[] compressedData)
        {
            using var input = new MemoryStream(compressedData);
            using var gzip = new GZipStream(input, CompressionMode.Decompress);
            using var output = new MemoryStream();
            gzip.CopyTo(output);
            return output.ToArray();
        }



        // crea contenido http comprimido para datos de texto
        private HttpContent CreateCompressedContent(string data, string mediaType)
        {
            if (!_compressionEnabled)
            {
                return new StringContent(data, Encoding.UTF8, mediaType);
            }

            var originalBytes = Encoding.UTF8.GetBytes(data);
            var compressedBytes = CompressData(originalBytes);
            var content = new ByteArrayContent(compressedBytes);

            content.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue(mediaType);
            content.Headers.ContentEncoding.Add("gzip");

            var compressionRatio = originalBytes.Length > 0 ? (double)compressedBytes.Length / originalBytes.Length * 100 : 0;
            _logger.LogDebug($"Datos comprimidos - Original: {originalBytes.Length} bytes, Comprimido: {compressedBytes.Length} bytes ({compressionRatio:F1}%)");

            return content;
        }



        // crea contenido http comprimido para datos binarios
        private HttpContent CreateCompressedBinaryContent(byte[] data, string contentType)
        {
            if (!_compressionEnabled)
            {
                var content = new ByteArrayContent(data);
                content.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue(contentType);
                return content;
            }

            var compressedData = CompressData(data);
            var compressedContent = new ByteArrayContent(compressedData);

            compressedContent.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue(contentType);
            compressedContent.Headers.ContentEncoding.Add("gzip");

            var compressionRatio = data.Length > 0 ? (double)compressedData.Length / data.Length * 100 : 0;
            _logger.LogDebug($"Datos binarios comprimidos - Original: {data.Length} bytes, Comprimido: {compressedData.Length} bytes ({compressionRatio:F1}%)");

            return compressedContent;
        }



        // libera recursos del cliente http
        public void Dispose()
        {
            var clientCount = _clientPool.Count; // contar clientes antes de limpiar

            foreach (var client in _clientPool.Values)
            {
                client?.Dispose();
            }
            _clientPool.Clear();
            _handler?.Dispose();

            _logger.LogInformation($"HttpClientService disposed exitosamente - Liberados {clientCount} clientes HTTP y pool de conexiones");
        }
    }
}