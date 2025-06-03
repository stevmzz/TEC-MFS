using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.IO;
using System.Linq;
using Newtonsoft.Json;
using TecMFS.Common.Constants;

namespace TecMFS.Common.Configuration
{
    // esta clase unifica toda la configuracion del sistema distribuido
    public class SystemConfiguration
    {
        // configuracion del controller central
        [Required]
        [JsonProperty("controller")]
        public ControllerConfiguration Controller { get; set; } = new();

        // lista de configuraciones de todos los disknodes
        [Required]
        [JsonProperty("diskNodes")]
        public List<NodeConfiguration> DiskNodes { get; set; } = new();

        // configuracion especifica del raid
        [Required]
        [JsonProperty("raidConfig")]
        public RaidConfiguration RaidConfig { get; set; } = new();

        // configuracion de red y timeouts
        [JsonProperty("networkConfig")]
        public NetworkConfiguration NetworkConfig { get; set; } = new();

        // nombre del entorno (development, production, etc.)
        [JsonProperty("environment")]
        public string Environment { get; set; } = "Development";

        // version de la configuracion
        [JsonProperty("version")]
        public string Version { get; set; } = "1.0.0";

        // fecha de creacion de la configuracion
        [JsonProperty("createdAt")]
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        // lista de nodos activos (no se serializa)
        [JsonIgnore]
        public List<NodeConfiguration> ActiveNodes => DiskNodes.Where(n => n.IsActive).ToList();

        // numero de nodos activos (no se serializa)
        [JsonIgnore]
        public int ActiveNodeCount => ActiveNodes.Count;

        // capacidad total del sistema en bytes (no se serializa)
        [JsonIgnore]
        public long TotalSystemCapacity => DiskNodes.Sum(n => n.MaxStorageBytes);

        // capacidad efectiva (descontando paridad) en bytes (no se serializa)
        [JsonIgnore]
        public long EffectiveCapacity => (long)(TotalSystemCapacity * RaidConfig.StorageEfficiency / 100);

        // indica si el sistema esta en estado valido (no se serializa)
        [JsonIgnore]
        public bool IsSystemHealthy => IsValid() && ActiveNodeCount >= SystemConstants.RAID_TOTAL_NODES;

        // ================================
        // constructores
        // ================================

        // constructor que crea configuracion por defecto
        public SystemConfiguration()
        {
            InitializeDefaultConfiguration();
        }



        // constructor con configuracion personalizada
        public SystemConfiguration(string environment, bool useHttps = false)
        {
            Environment = environment;
            InitializeDefaultConfiguration();

            if (useHttps)
            {
                EnableHttpsForAllNodes();
            }
        }



        // ================================
        // metodos publicos
        // ================================

        // valida que toda la configuracion del sistema sea correcta
        public bool IsValid()
        {
            try
            {
                return Controller.IsValid() &&
                       DiskNodes.Count == SystemConstants.RAID_TOTAL_NODES &&
                       DiskNodes.All(n => n.IsValid()) &&
                       DiskNodes.Select(n => n.NodeId).Distinct().Count() == SystemConstants.RAID_TOTAL_NODES &&
                       DiskNodes.Select(n => n.Port).Distinct().Count() == SystemConstants.RAID_TOTAL_NODES &&
                       RaidConfig.IsValidConfiguration &&
                       NetworkConfig.IsValid();
            }
            catch
            {
                return false;
            }
        }



        // obtiene la configuracion de un nodo especifico
        public NodeConfiguration? GetNodeConfiguration(int nodeId)
        {
            return DiskNodes.FirstOrDefault(n => n.NodeId == nodeId);
        }



        // habilita https para todos los nodos
        public void EnableHttpsForAllNodes()
        {
            Controller.UseHttps = true;
            foreach (var node in DiskNodes)
            {
                node.UseHttps = true;
            }
        }



        // deshabilita https para todos los nodos
        public void DisableHttpsForAllNodes()
        {
            Controller.UseHttps = false;
            foreach (var node in DiskNodes)
            {
                node.UseHttps = false;
            }
        }



        // crea todos los directorios necesarios para el sistema
        public bool CreateAllDirectories()
        {
            try
            {
                foreach (var node in DiskNodes)
                {
                    if (!node.CreateDirectories())
                    {
                        return false;
                    }
                }
                return true;
            }
            catch
            {
                return false;
            }
        }



        // guarda la configuracion en un archivo json
        public bool SaveToFile(string filePath)
        {
            try
            {
                var json = JsonConvert.SerializeObject(this, Formatting.Indented);
                File.WriteAllText(filePath, json);
                return true;
            }
            catch
            {
                return false;
            }
        }



        // carga configuracion desde un archivo json
        public static SystemConfiguration? LoadFromFile(string filePath)
        {
            try
            {
                if (!File.Exists(filePath)) return null;

                var json = File.ReadAllText(filePath);
                return JsonConvert.DeserializeObject<SystemConfiguration>(json);
            }
            catch
            {
                return null;
            }
        }



        // clona la configuracion completa del sistema
        public SystemConfiguration Clone()
        {
            var json = JsonConvert.SerializeObject(this);
            return JsonConvert.DeserializeObject<SystemConfiguration>(json)!;
        }



        // representacion en string de la configuracion
        public override string ToString()
        {
            var status = IsSystemHealthy ? "✅ HEALTHY" : "❌ UNHEALTHY";
            return $"{status} TECMFS System Config v{Version} ({Environment})\n" +
                   $"  Controller: {Controller}\n" +
                   $"  Active Nodes: {ActiveNodeCount}/{SystemConstants.RAID_TOTAL_NODES}\n" +
                   $"  Total Capacity: {FormatBytes(TotalSystemCapacity)}\n" +
                   $"  Effective Capacity: {FormatBytes(EffectiveCapacity)} ({RaidConfig.StorageEfficiency:F1}%)";
        }

        // ================================
        // metodos privados
        // ================================

        // inicializa la configuracion por defecto del sistema
        private void InitializeDefaultConfiguration()
        {
            // configurar controller
            Controller = new ControllerConfiguration();

            // configurar disknodes (4 nodos para raid 5)
            DiskNodes = new List<NodeConfiguration>();
            for (int i = 1; i <= SystemConstants.RAID_TOTAL_NODES; i++)
            {
                DiskNodes.Add(new NodeConfiguration(i));
            }

            // configuracion raid por defecto
            RaidConfig = new RaidConfiguration();

            // configuracion de red por defecto
            NetworkConfig = new NetworkConfiguration();
        }



        // formatea bytes en formato legible
        private static string FormatBytes(long bytes)
        {
            if (bytes < 1024) return $"{bytes} B";
            if (bytes < 1048576) return $"{bytes / 1024.0:F1} KB";
            if (bytes < 1073741824) return $"{bytes / 1048576.0:F1} MB";
            return $"{bytes / 1073741824.0:F1} GB";
        }
    }

    // configuracion especifica del controller
    public class ControllerConfiguration
    {
        // direccion ip del controller
        [Required]
        [JsonProperty("ipAddress")]
        public string IpAddress { get; set; } = "localhost";

        // puerto http del controller
        [Range(1000, 65535)]
        [JsonProperty("port")]
        public int Port { get; set; } = SystemConstants.CONTROLLER_PORT;

        // puerto https del controller
        [Range(1000, 65535)]
        [JsonProperty("httpsPort")]
        public int HttpsPort { get; set; } = SystemConstants.CONTROLLER_HTTPS_PORT;

        // indica si usar https
        [JsonProperty("useHttps")]
        public bool UseHttps { get; set; } = false;

        // directorio para logs del controller
        [JsonProperty("logPath")]
        public string LogPath { get; set; } = "./logs/controller";

        // url base http (no se serializa)
        [JsonIgnore]
        public string BaseUrl => $"http://{IpAddress}:{Port}";

        // url base https (no se serializa)
        [JsonIgnore]
        public string BaseUrlHttps => $"https://{IpAddress}:{HttpsPort}";

        // url base efectiva (no se serializa)
        [JsonIgnore]
        public string EffectiveBaseUrl => UseHttps ? BaseUrlHttps : BaseUrl;

        // valida la configuracion del controller
        public bool IsValid()
        {
            return !string.IsNullOrWhiteSpace(IpAddress) &&
                   Port > 0 && Port <= 65535 &&
                   HttpsPort > 0 && HttpsPort <= 65535 &&
                   Port != HttpsPort;
        }

        public override string ToString()
        {
            var protocol = UseHttps ? "HTTPS" : "HTTP";
            return $"{EffectiveBaseUrl} ({protocol})";
        }
    }

    // configuracion de red y timeouts
    public class NetworkConfiguration
    {
        // timeout por defecto para requests http
        [Range(1000, 60000)]
        [JsonProperty("defaultTimeoutMs")]
        public int DefaultTimeoutMs { get; set; } = SystemConstants.DEFAULT_REQUEST_TIMEOUT_MS;

        // intervalo para health checks
        [Range(5, 300)]
        [JsonProperty("healthCheckIntervalSeconds")]
        public int HealthCheckIntervalSeconds { get; set; } = SystemConstants.HEALTH_CHECK_INTERVAL_SECONDS;

        // maximo de reintentos para operaciones fallidas
        [Range(1, 10)]
        [JsonProperty("maxRetryAttempts")]
        public int MaxRetryAttempts { get; set; } = SystemConstants.MAX_RETRY_ATTEMPTS;

        // tiempo entre reintentos en milisegundos
        [Range(100, 10000)]
        [JsonProperty("retryDelayMs")]
        public int RetryDelayMs { get; set; } = 1000;

        // minutos antes de considerar un nodo como fallido
        [Range(1, 60)]
        [JsonProperty("nodeFailureThresholdMinutes")]
        public int NodeFailureThresholdMinutes { get; set; } = SystemConstants.NODE_FAILURE_THRESHOLD_MINUTES;

        // valida la configuracion de red
        public bool IsValid()
        {
            return DefaultTimeoutMs > 0 &&
                   HealthCheckIntervalSeconds > 0 &&
                   MaxRetryAttempts > 0 &&
                   RetryDelayMs > 0 &&
                   NodeFailureThresholdMinutes > 0;
        }
    }
}