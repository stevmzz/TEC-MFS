using System;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;
using TecMFS.Common.Interfaces;
using TecMFS.Common.Constants;

namespace TecMFS.Controller.Services
{
    // implementacion del servicio de comunicacion http entre componentes
    // maneja todas las operaciones http del sistema distribuido
    public class HttpClientService : IHttpClientService, IDisposable
    {
        private readonly HttpClient _httpClient;
        private readonly ILogger<HttpClientService> _logger;
        private int _timeoutMs;
        private int _maxRetries;
        private int _retryDelayMs;

        // constructor que configura el cliente http con valores por defecto
        public HttpClientService(HttpClient httpClient, ILogger<HttpClientService> logger)
        {
            _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _timeoutMs = SystemConstants.DEFAULT_REQUEST_TIMEOUT_MS;
            _maxRetries = SystemConstants.MAX_RETRY_ATTEMPTS;
            _retryDelayMs = 1000;

            ConfigureHttpClient();
        }

        // ================================
        // metodos publicos de la interface
        // ================================

        // envia request get y deserializa la respuesta
        public async Task<T?> SendGetAsync<T>(string url)
        {
            _logger.LogInformation("Enviando GET request a: {Url}", url);

            try
            {
                // CORREGIDO: ExecuteWithRetryAsync (sin 's')
                var response = await ExecuteWithRetryAsync(() => _httpClient.GetAsync(url));

                if (response.IsSuccessStatusCode)
                {
                    var content = await response.Content.ReadAsStringAsync();
                    var result = JsonConvert.DeserializeObject<T>(content);

                    _logger.LogInformation("GET request exitoso a: {Url}, tipo: {Type}", url, typeof(T).Name);
                    return result;
                }
                else
                {
                    _logger.LogWarning("GET falló a: {Url}, status: {Status}", url, response.StatusCode);
                    return default(T);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error en GET request a: {Url}", url);
                return default(T);
            }
        }



        // envia request post con datos y deserializa respuesta
        public async Task<T?> SendPostAsync<T>(string url, object data)
        {
            _logger.LogInformation("Enviando POST request a: {Url}", url);

            try
            {
                var json = JsonConvert.SerializeObject(data);
                var content = new StringContent(json, Encoding.UTF8, "application/json");

                var response = await ExecuteWithRetryAsync(async () => await _httpClient.PostAsync(url, content));

                if (response.IsSuccessStatusCode)
                {
                    var responseContent = await response.Content.ReadAsStringAsync();
                    var result = JsonConvert.DeserializeObject<T>(responseContent);

                    _logger.LogInformation("POST exitoso a: {Url}, tipo: {Type}", url, typeof(T).Name);
                    return result;
                }
                else
                {
                    _logger.LogWarning("POST falló a: {Url}, status: {Status}", url, response.StatusCode);
                    return default(T);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error en POST request a: {Url}", url);
                return default(T);
            }
        }



        // envia request put con datos y deserializa respuesta
        public async Task<T?> SendPutAsync<T>(string url, object data)
        {
            _logger.LogInformation("Enviando PUT request a: {Url}", url);

            try
            {
                var json = JsonConvert.SerializeObject(data);
                var content = new StringContent(json, Encoding.UTF8, "application/json");

                var response = await ExecuteWithRetryAsync(async () => await _httpClient.PutAsync(url, content));

                if (response.IsSuccessStatusCode)
                {
                    var responseContent = await response.Content.ReadAsStringAsync();
                    var result = JsonConvert.DeserializeObject<T>(responseContent);

                    _logger.LogInformation("PUT exitoso a: {Url}, tipo: {Type}", url, typeof(T).Name);
                    return result;
                }
                else
                {
                    _logger.LogWarning("PUT falló a: {Url}, status: {Status}", url, response.StatusCode);
                    return default(T);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error en PUT request a: {Url}", url);
                return default(T);
            }
        }



        // envia request delete
        public async Task<bool> SendDeleteAsync(string url)
        {
            _logger.LogInformation("Enviando DELETE request a: {Url}", url);

            try
            {
                var response = await ExecuteWithRetryAsync(async () => await _httpClient.DeleteAsync(url));

                var success = response.IsSuccessStatusCode;

                if (success)
                {
                    _logger.LogInformation("DELETE exitoso a: {Url}", url);
                }
                else
                {
                    _logger.LogWarning("DELETE falló a: {Url}, status: {Status}", url, response.StatusCode);
                }

                return success;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error en DELETE request a: {Url}", url);
                return false;
            }
        }



        // verifica si un endpoint responde (health check)
        public async Task<bool> CheckHealthAsync(string baseUrl)
        {
            var healthUrl = $"{baseUrl.TrimEnd('/')}/health";
            _logger.LogDebug("Verificando salud de: {Url}", healthUrl);

            try
            {
                using var response = await _httpClient.GetAsync(healthUrl);
                var isHealthy = response.IsSuccessStatusCode;

                _logger.LogDebug("Health check {Status} para: {Url}",
                    isHealthy ? "exitoso" : "falló", healthUrl);

                return isHealthy;
            }
            catch (Exception ex)
            {
                _logger.LogDebug(ex, "Health check falló para: {Url}", healthUrl);
                return false;
            }
        }



        // envia datos binarios (para archivos)
        public async Task<T?> SendBinaryDataAsync<T>(string url, byte[] data, string contentType)
        {
            _logger.LogInformation("Enviando datos binarios a: {Url}, tamaño: {Size} bytes", url, data.Length);

            try
            {
                var content = new ByteArrayContent(data);
                content.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue(contentType);

                var response = await ExecuteWithRetryAsync(async () => await _httpClient.PostAsync(url, content));

                if (response.IsSuccessStatusCode)
                {
                    var responseContent = await response.Content.ReadAsStringAsync();
                    var result = JsonConvert.DeserializeObject<T>(responseContent);

                    _logger.LogInformation("Envío binario exitoso a: {Url}", url);
                    return result;
                }
                else
                {
                    _logger.LogWarning("Envío binario falló a: {Url}, status: {Status}", url, response.StatusCode);
                    return default(T);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error enviando datos binarios a: {Url}", url);
                return default(T);
            }
        }

        // ================================
        // metodos de configuracion
        // ================================

        // configura timeout personalizado
        public void SetTimeout(int timeoutMs)
        {
            if (timeoutMs <= 0)
                throw new ArgumentException("Timeout debe ser mayor a 0", nameof(timeoutMs));

            _timeoutMs = timeoutMs;
            _httpClient.Timeout = TimeSpan.FromMilliseconds(timeoutMs);

            _logger.LogInformation("Timeout configurado a: {Timeout}ms", timeoutMs);
        }



        // configura reintentos automaticos
        public void SetRetryPolicy(int maxRetries, int delayMs)
        {
            if (maxRetries < 0)
                throw new ArgumentException("Reintentos no puede ser negativo", nameof(maxRetries));
            if (delayMs < 0)
                throw new ArgumentException("Delay no puede ser negativo", nameof(delayMs));

            _maxRetries = maxRetries;
            _retryDelayMs = delayMs;

            _logger.LogInformation("Política de reintentos: {MaxRetries} intentos, {Delay}ms delay",
                maxRetries, delayMs);
        }

        // ================================
        // metodos privados
        // ================================

        // configura el cliente http con valores por defecto
        private void ConfigureHttpClient()
        {
            _httpClient.Timeout = TimeSpan.FromMilliseconds(_timeoutMs);
            _httpClient.DefaultRequestHeaders.Add("User-Agent", "TecMFS-HttpClient/1.0");
            _httpClient.DefaultRequestHeaders.Add("Accept", "application/json");

            _logger.LogInformation("HttpClient configurado - Timeout: {Timeout}ms", _timeoutMs);
        }



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
                            _logger.LogInformation("Operación exitosa en intento: {Attempt}", attempt + 1);
                        }
                        return response;
                    }

                    // error de servidor (5xx) - reintentar
                    lastException = new HttpRequestException($"Server error: {response.StatusCode}");
                    _logger.LogWarning("Intento {Attempt} falló con status: {Status}",
                        attempt + 1, response.StatusCode);
                }
                catch (Exception ex)
                {
                    lastException = ex;
                    _logger.LogWarning(ex, "Intento {Attempt} falló con excepción", attempt + 1);
                }

                // si no es el último intento, esperar antes del siguiente
                if (attempt < _maxRetries)
                {
                    await Task.Delay(_retryDelayMs);
                }
            }

            _logger.LogError("Todos los intentos fallaron después de {MaxRetries} reintentos", _maxRetries);
            throw lastException ?? new HttpRequestException("Operación falló después de todos los reintentos");
        }



        // libera recursos del cliente http
        public void Dispose()
        {
            _httpClient?.Dispose();
            _logger.LogInformation("HttpClientService disposed");
        }
    }
}