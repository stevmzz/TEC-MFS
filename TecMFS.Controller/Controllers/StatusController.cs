using Microsoft.AspNetCore.Mvc;
using TecMFS.Common.DTOs;
using TecMFS.Common.Models;
using TecMFS.Common.Constants;
using TecMFS.Common.Interfaces;

namespace TecMFS.Controller.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Produces("application/json")]
    public class StatusController : ControllerBase
    {
        private readonly ILogger<StatusController> _logger;
        private readonly IRaidManager _raidManager;
        private readonly INodeHealthMonitor _nodeHealthMonitor;

        public StatusController(ILogger<StatusController> logger, IRaidManager raidManager, INodeHealthMonitor nodeHealthMonitor)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _raidManager = raidManager ?? throw new ArgumentNullException(nameof(raidManager));
            _nodeHealthMonitor = nodeHealthMonitor ?? throw new ArgumentNullException(nameof(nodeHealthMonitor));
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
                // USAR RAID MANAGER REAL
                var raidStatus = await _raidManager.GetRaidStatusAsync();
                var nodesStatus = await _nodeHealthMonitor.CheckAllNodesAsync();

                raidStatus.Nodes = nodesStatus;

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
                // USAR NODE HEALTH MONITOR REAL
                var nodesStatus = await _nodeHealthMonitor.CheckAllNodesAsync();

                var onlineCount = nodesStatus.Count(n => n.IsOnline);
                _logger.LogInformation($"Estado de nodos obtenido exitosamente - Online: {onlineCount}/{nodesStatus.Count}");
                return Ok(nodesStatus);
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
                // USAR SERVICIOS REALES
                var raidStatus = await _raidManager.GetRaidStatusAsync();
                var nodesStatus = await _nodeHealthMonitor.CheckAllNodesAsync();
                var availabilityStats = _nodeHealthMonitor.GetAvailabilityStats();

                var stats = new SystemStatsResponse
                {
                    SystemInfo = new SystemInfo
                    {
                        Version = "1.0.0",
                        StartTime = DateTime.UtcNow.AddHours(-2),
                        Uptime = GetControllerUptime(),
                        Environment = "Development"
                    },
                    RaidInfo = new RaidInfo
                    {
                        TotalNodes = SystemConstants.RAID_TOTAL_NODES,
                        OnlineNodes = availabilityStats.OnlineNodes,
                        OfflineNodes = availabilityStats.OfflineNodes,
                        RaidLevel = "RAID 5",
                        BlockSize = SystemConstants.DEFAULT_BLOCK_SIZE,
                        Status = availabilityStats.SystemStatus
                    },
                    StorageInfo = new StorageInfo
                    {
                        TotalCapacity = nodesStatus.Sum(n => n.TotalStorage),
                        UsedSpace = nodesStatus.Sum(n => n.UsedStorage),
                        AvailableSpace = nodesStatus.Sum(n => n.AvailableStorage),
                        UsagePercentage = nodesStatus.Sum(n => n.StorageUsagePercentage) / nodesStatus.Count,
                        TotalFiles = raidStatus.TotalFiles,
                        TotalBlocks = raidStatus.TotalBlocks
                    },
                    PerformanceInfo = new PerformanceInfo
                    {
                        AverageResponseTime = nodesStatus.Where(n => n.IsOnline).Average(n => n.ResponseTimeMs),
                        TotalRequests = 0, // esto podria ser un contador interno
                        SuccessfulRequests = 0,
                        FailedRequests = 0,
                        SuccessRate = availabilityStats.AvailabilityPercentage
                    }
                };

                _logger.LogInformation($"Estadisticas obtenidas exitosamente - Archivos: {stats.StorageInfo.TotalFiles}, Almacenamiento usado: {FormatBytes(stats.StorageInfo.UsedSpace)}");
                return Ok(stats);
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

        private TimeSpan GetControllerUptime()
        {
            // simular que el controller ha estado corriendo por 2 horas
            return TimeSpan.FromHours(2) + TimeSpan.FromMinutes(35) + TimeSpan.FromSeconds(22);
        }

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

        public class ErrorResponse
        {
            public string Error { get; set; } = string.Empty;
            public string Message { get; set; } = string.Empty;
            public string RequestId { get; set; } = string.Empty;
            public DateTime Timestamp { get; set; } = DateTime.UtcNow;
        }

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