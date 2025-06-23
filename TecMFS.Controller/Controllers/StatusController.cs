using Microsoft.AspNetCore.Mvc;
using TecMFS.Common.Constants;
using TecMFS.Common.DTOs;
using TecMFS.Common.Interfaces;
using TecMFS.Common.Models;
using static TecMFS.Controller.Controllers.FilesController;

namespace TecMFS.Controller.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Produces("application/json")]
    public class StatusController : ControllerBase
    {
        private readonly ILogger<StatusController> _logger;
        private readonly IRaidManager _raidManager;

        public StatusController(ILogger<StatusController> logger, IRaidManager raidManager)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _raidManager = raidManager ?? throw new ArgumentNullException(nameof(raidManager));
        }


        // get: obtiene el estado general del sistema raid
        [HttpGet("raid")]
        [ProducesResponseType(typeof(StatusResponse), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status500InternalServerError)]
        public async Task<IActionResult> GetRaidStatus()
        {
            _logger.LogInformation("Obteniendo estado general del sistema RAID");
    
            try
            {
                var raidStatus = await _raidManager.GetRaidStatusAsync();
                _logger.LogInformation($"Estado RAID obtenido exitosamente - Estado: {raidStatus.Status}, Nodos online: {raidStatus.OnlineNodes}/{SystemConstants.RAID_TOTAL_NODES}");
                return Ok(raidStatus);

            }

            catch (Exception ex)
            {
                _logger.LogError(ex, "Error critico obteniendo estado del sistema RAID");
                return StatusCode(500, new ErrorResponse
                {
                    Error = "Error interno del servidor",
                    Message = "Error obteniendo estado del sistema RAID",
                    RequestId = Guid.NewGuid().ToString()
                });
            }
        }



        // get: obtiene el estado de todos los nodos del sistema
        [HttpGet("nodes")]
        [ProducesResponseType(typeof(List<NodeStatus>), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status500InternalServerError)]
        public async Task<IActionResult> GetNodesStatus()
        {
            _logger.LogInformation("Obteniendo estado de todos los nodos del sistema");

            try
            {
                var raidStatus = await _raidManager.GetRaidStatusAsync();
                var nodes = raidStatus.Nodes;
                var onlineCount = nodes.Count(n => n.IsOnline);
                _logger.LogInformation($"Estado de nodos obtenido exitosamente - Online: {onlineCount}/{nodes.Count}");
                return Ok(nodes);

            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error critico obteniendo estado de nodos");
                return StatusCode(500, new ErrorResponse
                {
                    Error = "Error interno del servidor",
                    Message = "Error obteniendo estado de nodos",
                    RequestId = Guid.NewGuid().ToString()
                });
            }
        }



        // get: health check simple del controller
        [HttpGet("health")]
        [ProducesResponseType(typeof(HealthResponse), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status503ServiceUnavailable)]
        public async Task<IActionResult> GetHealth()
        {
            _logger.LogDebug("Realizando health check del controller");

            try
            {
                // verificar estado basico del controller
                var healthResponse = new HealthResponse
                {
                    Status = "Healthy",
                    Timestamp = DateTime.UtcNow,
                    Version = "1.0.0",
                    Uptime = GetControllerUptime(),
                    ComponentName = "TecMFS Controller"
                };

                _logger.LogDebug($"Health check completado - Estado: {healthResponse.Status}, Uptime: {healthResponse.UptimeFormatted}");
                return Ok(healthResponse);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error critico durante health check");
                return StatusCode(503, new ErrorResponse
                {
                    Error = "Servicio no disponible",
                    Message = "Controller no esta funcionando correctamente",
                    RequestId = Guid.NewGuid().ToString()
                });
            }
        }



        // get: estadisticas detalladas del sistema
        [HttpGet("stats")]
        [ProducesResponseType(typeof(SystemStatsResponse), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status500InternalServerError)]
        public async Task<IActionResult> GetSystemStats()
        {
            _logger.LogInformation("Obteniendo estadisticas detalladas del sistema");

            try
            {
                // TODO: Jose reemplazara esto con logica real del RaidManager y NodeHealthMonitor
                // var stats = await _raidManager.GetSystemStatsAsync();

                // mock response para testing
                var mockStats = CreateMockSystemStats();

                _logger.LogInformation($"Estadisticas obtenidas exitosamente - Archivos: {mockStats.StorageInfo.TotalFiles}, Almacenamiento usado: {FormatBytes(mockStats.StorageInfo.UsedSpace)}"); return Ok(mockStats);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error critico obteniendo estadisticas del sistema");
                return StatusCode(500, new ErrorResponse
                {
                    Error = "Error interno del servidor",
                    Message = "Error obteniendo estadisticas del sistema",
                    RequestId = Guid.NewGuid().ToString()
                });
            }
        }

        // ================================
        // metodos privados helper
        // ================================

        // crea estado mock del sistema raid
        private StatusResponse CreateMockRaidStatus()
        {
            var mockNodes = CreateMockNodesStatus();
            var onlineNodes = mockNodes.Count(n => n.IsOnline);

            return new StatusResponse
            {
                ComponentName = "TecMFS RAID System",
                Status = onlineNodes >= SystemConstants.RAID_TOTAL_NODES ?
                    StatusResponse.ComponentStatus.Healthy :
                    StatusResponse.ComponentStatus.Warning,
                Message = onlineNodes >= SystemConstants.RAID_TOTAL_NODES ?
                    "Sistema RAID funcionando correctamente" :
                    "Algunos nodos estan offline",
                Timestamp = DateTime.UtcNow,
                Version = "1.0.0",
                Uptime = GetControllerUptime(),
                Nodes = mockNodes,
                TotalFiles = 15,
                TotalBlocks = 45,
                UsedStorage = 1024L * 1024L * 50L, // 50MB  
                TotalStorage = 1024L * 1024L * 1024L * 10L, // 10GB
                ErrorCount = onlineNodes < SystemConstants.RAID_TOTAL_NODES ? 1 : 0,
                LastError = onlineNodes < SystemConstants.RAID_TOTAL_NODES ? "Nodo 3 offline" : ""
            };
        }



        // crea estado mock de todos los nodos
        private List<NodeStatus> CreateMockNodesStatus()
        {
            return new List<NodeStatus>
            {
                new NodeStatus
                {
                    NodeId = 1,
                    IsOnline = true,
                    BaseUrl = SystemConstants.GetDiskNodeBaseUrl(1),
                    TotalStorage = 1024L * 1024L * 1024L * 2L, // 2GB
                    UsedStorage = 1024L * 1024L * 200L, // 200MB
                    LastHeartbeat = DateTime.UtcNow.AddSeconds(-5),
                    ResponseTimeMs = 45.2,
                    ErrorCount = 0,
                    Version = "1.0.0"
                },
                new NodeStatus
                {
                    NodeId = 2,
                    IsOnline = true,
                    BaseUrl = SystemConstants.GetDiskNodeBaseUrl(2),
                    TotalStorage = 1024L * 1024L * 1024L * 2L, // 2GB
                    UsedStorage = 1024L * 1024L * 180L, // 180MB
                    LastHeartbeat = DateTime.UtcNow.AddSeconds(-8),
                    ResponseTimeMs = 52.1,
                    ErrorCount = 0,
                    Version = "1.0.0"
                },
                new NodeStatus
                {
                    NodeId = 3,
                    IsOnline = false, // simular nodo offline
                    BaseUrl = SystemConstants.GetDiskNodeBaseUrl(3),
                    TotalStorage = 1024L * 1024L * 1024L * 2L, // 2GB
                    UsedStorage = 1024L * 1024L * 190L, // 190MB
                    LastHeartbeat = DateTime.UtcNow.AddMinutes(-5), // ultimo heartbeat hace 5 minutos
                    ResponseTimeMs = 0,
                    ErrorCount = 3,
                    Version = "1.0.0"
                },
                new NodeStatus
                {
                    NodeId = 4,
                    IsOnline = true,
                    BaseUrl = SystemConstants.GetDiskNodeBaseUrl(4),
                    TotalStorage = 1024L * 1024L * 1024L * 2L, // 2GB
                    UsedStorage = 1024L * 1024L * 175L, // 175MB
                    LastHeartbeat = DateTime.UtcNow.AddSeconds(-3),
                    ResponseTimeMs = 38.7,
                    ErrorCount = 0,
                    Version = "1.0.0"
                }
            };
        }



        // obtiene el uptime del controller (simulado)
        private TimeSpan GetControllerUptime()
        {
            // simular que el controller ha estado corriendo por 2 horas
            return TimeSpan.FromHours(2) + TimeSpan.FromMinutes(35) + TimeSpan.FromSeconds(22);
        }



        // crea estadisticas mock del sistema
        private SystemStatsResponse CreateMockSystemStats()
        {
            var nodes = CreateMockNodesStatus();
            var onlineNodes = nodes.Count(n => n.IsOnline);
            var totalStorage = nodes.Sum(n => n.TotalStorage);
            var usedStorage = nodes.Sum(n => n.UsedStorage);

            return new SystemStatsResponse
            {
                SystemInfo = new SystemInfo
                {
                    Version = "1.0.0",
                    StartTime = DateTime.UtcNow.AddHours(-2), // sistema iniciado hace 2 horas
                    Uptime = GetControllerUptime(),
                    Environment = "Development"
                },
                RaidInfo = new RaidInfo
                {
                    TotalNodes = SystemConstants.RAID_TOTAL_NODES,
                    OnlineNodes = onlineNodes,
                    OfflineNodes = SystemConstants.RAID_TOTAL_NODES - onlineNodes,
                    RaidLevel = "RAID 5",
                    BlockSize = SystemConstants.DEFAULT_BLOCK_SIZE,
                    Status = onlineNodes >= SystemConstants.RAID_TOTAL_NODES ? "Healthy" : "Degraded"
                },
                StorageInfo = new StorageInfo
                {
                    TotalCapacity = totalStorage,
                    UsedSpace = usedStorage,
                    AvailableSpace = totalStorage - usedStorage,
                    UsagePercentage = totalStorage > 0 ? (double)usedStorage / totalStorage * 100 : 0,
                    TotalFiles = 15,
                    TotalBlocks = 45
                },
                PerformanceInfo = new PerformanceInfo
                {
                    AverageResponseTime = nodes.Where(n => n.IsOnline).Average(n => n.ResponseTimeMs),
                    TotalRequests = 1250,
                    SuccessfulRequests = 1235,
                    FailedRequests = 15,
                    SuccessRate = 98.8
                }
            };
        }



        // formatea bytes en formato legible
        private string FormatBytes(long bytes)
        {
            if (bytes < 1024) return $"{bytes} B";
            if (bytes < 1048576) return $"{bytes / 1024.0:F1} KB";
            if (bytes < 1073741824) return $"{bytes / 1048576.0:F1} MB";
            return $"{bytes / 1073741824.0:F1} GB";
        }

        // ================================
        // clases helper para responses
        // ================================

        public class HealthResponse
        {
            public string Status { get; set; } = string.Empty;
            public DateTime Timestamp { get; set; }
            public string Version { get; set; } = string.Empty;
            public TimeSpan Uptime { get; set; }
            public string ComponentName { get; set; } = string.Empty;

            public string UptimeFormatted => $"{Uptime.Days}d {Uptime.Hours}h {Uptime.Minutes}m";
        }

        public class SystemStatsResponse
        {
            public SystemInfo SystemInfo { get; set; } = new SystemInfo();
            public RaidInfo RaidInfo { get; set; } = new RaidInfo();
            public StorageInfo StorageInfo { get; set; } = new StorageInfo();
            public PerformanceInfo PerformanceInfo { get; set; } = new PerformanceInfo();
        }

        public class SystemInfo
        {
            public string Version { get; set; } = string.Empty;
            public DateTime StartTime { get; set; }
            public TimeSpan Uptime { get; set; }
            public string Environment { get; set; } = string.Empty;
        }

        public class RaidInfo
        {
            public int TotalNodes { get; set; }
            public int OnlineNodes { get; set; }
            public int OfflineNodes { get; set; }
            public string RaidLevel { get; set; } = string.Empty;
            public int BlockSize { get; set; }
            public string Status { get; set; } = string.Empty;
        }

        public class StorageInfo
        {
            public long TotalCapacity { get; set; }
            public long UsedSpace { get; set; }
            public long AvailableSpace { get; set; }
            public double UsagePercentage { get; set; }
            public int TotalFiles { get; set; }
            public int TotalBlocks { get; set; }
        }

        public class PerformanceInfo
        {
            public double AverageResponseTime { get; set; }
            public long TotalRequests { get; set; }
            public long SuccessfulRequests { get; set; }
            public long FailedRequests { get; set; }
            public double SuccessRate { get; set; }
        }

    }
}