using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using TecMFS.Common.Interfaces;
using TecMFS.Common.Models;
using TecMFS.Common.Constants;

namespace TecMFS.Controller.Services
{
    // implementacion de monitoreo de salud de nodos raid que verifica estado de los 4 nodos
    public class NodeHealthMonitor : INodeHealthMonitor
    {
        private readonly IHttpClientService _httpClient;
        private readonly ILogger<NodeHealthMonitor> _logger;
        private readonly Dictionary<int, string> _nodeUrls;
        private readonly Dictionary<int, NodeStatus> _nodeStatusCache;
        private Timer? _monitoringTimer;
        private bool _isMonitoring = false;

        public event EventHandler<NodeFailureEventArgs>? NodeFailureDetected;
        public event EventHandler<NodeRecoveryEventArgs>? NodeRecoveryDetected;

        public NodeHealthMonitor(IHttpClientService httpClient, ILogger<NodeHealthMonitor> logger)
        {
            _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));

            // configurar urls y cache de estado de nodos
            _nodeUrls = new Dictionary<int, string>();
            _nodeStatusCache = new Dictionary<int, NodeStatus>();

            for (int i = 1; i <= SystemConstants.RAID_TOTAL_NODES; i++)
            {
                _nodeUrls[i] = SystemConstants.GetDiskNodeBaseUrl(i);
                _nodeStatusCache[i] = new NodeStatus
                {
                    NodeId = i,
                    BaseUrl = _nodeUrls[i],
                    IsOnline = false,
                    LastHeartbeat = DateTime.MinValue
                };
            }

            _logger.LogInformation("NodeHealthMonitor inicializado para 4 nodos RAID");
        }



        // verifica salud de un nodo especifico mediante llamada http
        public async Task<NodeStatus> CheckNodeHealthAsync(int nodeId)
        {
            if (nodeId < 1 || nodeId > SystemConstants.RAID_TOTAL_NODES)
            {
                _logger.LogWarning($"NodeId invalido: {nodeId}");
                return new NodeStatus { NodeId = nodeId, IsOnline = false };
            }

            try
            {
                var startTime = DateTime.UtcNow;
                var healthUrl = $"{_nodeUrls[nodeId]}/api/blocks/health";

                // usar http get directo en lugar de checkhealthasync
                var response = await _httpClient.SendGetAsync<NodeHealthResponse>(healthUrl);
                var responseTime = (DateTime.UtcNow - startTime).TotalMilliseconds;

                var previousStatus = _nodeStatusCache[nodeId];

                // verificar si la respuesta es valida y healthy
                var isOnline = response != null &&
                              (response.Status?.Equals("Healthy", StringComparison.OrdinalIgnoreCase) == true ||
                               response.Status?.Equals("Online", StringComparison.OrdinalIgnoreCase) == true);

                var currentStatus = new NodeStatus
                {
                    NodeId = nodeId,
                    IsOnline = isOnline,
                    BaseUrl = _nodeUrls[nodeId],
                    LastHeartbeat = isOnline ? DateTime.UtcNow : previousStatus.LastHeartbeat,
                    ResponseTimeMs = isOnline ? responseTime : 0,
                    ErrorCount = isOnline ? 0 : previousStatus.ErrorCount + 1,
                    Version = response?.Version ?? "1.0.0",
                    TotalStorage = 10737418240, // 10gb por defecto
                    UsedStorage = isOnline ? 1073741824 : 0 // 1gb usado estimado
                };

                // detectar cambios de estado
                if (previousStatus.IsOnline && !isOnline)
                {
                    _logger.LogWarning($"Nodo {nodeId} detectado como offline");
                    OnNodeFailureDetected(new NodeFailureEventArgs
                    {
                        NodeId = nodeId,
                        Reason = "Health check failed",
                        FailureTime = DateTime.UtcNow,
                        LastKnownStatus = previousStatus
                    });
                }
                else if (!previousStatus.IsOnline && isOnline)
                {
                    var downTime = DateTime.UtcNow - previousStatus.LastHeartbeat;
                    _logger.LogInformation($"Nodo {nodeId} recuperado despues de {downTime.TotalMinutes:F1} minutos");
                    OnNodeRecoveryDetected(new NodeRecoveryEventArgs
                    {
                        NodeId = nodeId,
                        RecoveryTime = DateTime.UtcNow,
                        DownTime = downTime,
                        CurrentStatus = currentStatus
                    });
                }

                _nodeStatusCache[nodeId] = currentStatus;
                _logger.LogDebug($"Health check nodo {nodeId}: {(isOnline ? "Online" : "Offline")}");
                return currentStatus;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error verificando salud del nodo {nodeId}");

                var errorStatus = _nodeStatusCache[nodeId];
                errorStatus.IsOnline = false;
                errorStatus.ErrorCount++;

                return errorStatus;
            }
        }



        // verifica salud de todos los nodos del raid en paralelo
        public async Task<List<NodeStatus>> CheckAllNodesAsync()
        {
            var tasks = new List<Task<NodeStatus>>();

            for (int i = 1; i <= SystemConstants.RAID_TOTAL_NODES; i++)
            {
                tasks.Add(CheckNodeHealthAsync(i));
            }

            var results = await Task.WhenAll(tasks);
            var onlineCount = results.Count(r => r.IsOnline);

            _logger.LogDebug($"Health check completado: {onlineCount}/{SystemConstants.RAID_TOTAL_NODES} nodos online");
            return results.ToList();
        }



        // verifica si un nodo especifico esta disponible
        public async Task<bool> IsNodeAvailableAsync(int nodeId)
        {
            var status = await CheckNodeHealthAsync(nodeId);
            return status.IsOnline;
        }



        // inicia monitoreo continuo con intervalo especificado
        public async Task StartMonitoringAsync(int intervalSeconds)
        {
            if (_isMonitoring)
            {
                _logger.LogWarning("Monitoreo ya esta en progreso");
                return;
            }

            _isMonitoring = true;
            var interval = TimeSpan.FromSeconds(intervalSeconds);

            _monitoringTimer = new Timer(async _ =>
            {
                try
                {
                    await CheckAllNodesAsync();
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error durante monitoreo automatico");
                }
            }, null, TimeSpan.Zero, interval);

            _logger.LogInformation($"Monitoreo continuo iniciado con intervalo de {intervalSeconds} segundos");
        }



        // detiene el monitoreo continuo y libera recursos
        public void StopMonitoring()
        {
            if (!_isMonitoring)
            {
                return;
            }

            _monitoringTimer?.Dispose();
            _monitoringTimer = null;
            _isMonitoring = false;

            _logger.LogInformation("Monitoreo continuo detenido");
        }



        // obtiene estado actual de todos los nodos desde cache local
        public List<NodeStatus> GetCachedNodeStatus()
        {
            return _nodeStatusCache.Values.ToList();
        }



        // calcula estadisticas de disponibilidad del sistema raid
        public NodeAvailabilityStats GetAvailabilityStats()
        {
            var allNodes = _nodeStatusCache.Values.ToList();
            var onlineNodes = allNodes.Count(n => n.IsOnline);
            var healthyNodes = allNodes.Count(n => n.IsHealthy);

            return new NodeAvailabilityStats
            {
                TotalNodes = SystemConstants.RAID_TOTAL_NODES,
                OnlineNodes = onlineNodes,
                HealthyNodes = healthyNodes,
                OfflineNodes = SystemConstants.RAID_TOTAL_NODES - onlineNodes,
                AvailabilityPercentage = (double)onlineNodes / SystemConstants.RAID_TOTAL_NODES * 100,
                SystemStatus = onlineNodes >= 3 ? "Operational" : onlineNodes >= 2 ? "Degraded" : "Critical"
            };
        }



        // dispara evento cuando se detecta fallo de nodo
        protected virtual void OnNodeFailureDetected(NodeFailureEventArgs e)
        {
            NodeFailureDetected?.Invoke(this, e);
        }



        // dispara evento cuando se detecta recuperacion de nodo
        protected virtual void OnNodeRecoveryDetected(NodeRecoveryEventArgs e)
        {
            NodeRecoveryDetected?.Invoke(this, e);
        }
    }

    // clase para deserializar la respuesta de health check de los nodos
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