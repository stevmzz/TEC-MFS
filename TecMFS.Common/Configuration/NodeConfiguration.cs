using System;
using System.ComponentModel.DataAnnotations;
using System.IO;
using Newtonsoft.Json;
using TecMFS.Common.Constants;

namespace TecMFS.Common.Configuration
{
    // configuracion de un nodo individual del sistema raid
    // usado para configurar tanto controller como disknodes
    public class NodeConfiguration
    {
        // id unico del nodo (1-4 para raid 5)
        [Required]
        [Range(1, SystemConstants.RAID_TOTAL_NODES)]
        [JsonProperty("nodeId")]
        public int NodeId { get; set; }

        // direccion ip o hostname del nodo
        [Required]
        [JsonProperty("ipAddress")]
        public string IpAddress { get; set; } = "localhost";

        // puerto http del nodo
        [Range(1000, 65535)]
        [JsonProperty("port")]
        public int Port { get; set; }

        // puerto https del nodo (opcional)
        [Range(1000, 65535)]
        [JsonProperty("httpsPort")]
        public int HttpsPort { get; set; }

        // ruta del directorio donde se almacenan los datos
        [Required]
        [JsonProperty("storagePath")]
        public string StoragePath { get; set; } = SystemConstants.DEFAULT_STORAGE_PATH;

        // capacidad maxima de almacenamiento en bytes
        [Range(1, long.MaxValue)]
        [JsonProperty("maxStorageBytes")]
        public long MaxStorageBytes { get; set; } = SystemConstants.MAX_NODE_STORAGE;

        // indica si el nodo esta activo en el sistema
        [JsonProperty("isActive")]
        public bool IsActive { get; set; } = true;

        // indica si el nodo debe usar https
        [JsonProperty("useHttps")]
        public bool UseHttps { get; set; } = false;

        // timeout personalizado para este nodo en milisegundos
        [Range(1000, 60000)]
        [JsonProperty("timeoutMs")]
        public int TimeoutMs { get; set; } = SystemConstants.DEFAULT_REQUEST_TIMEOUT_MS;

        // maximo de intentos de conexion para este nodo
        [Range(1, 10)]
        [JsonProperty("maxRetries")]
        public int MaxRetries { get; set; } = SystemConstants.MAX_RETRY_ATTEMPTS;

        // url base http del nodo (no se serializa)
        [JsonIgnore]
        public string BaseUrl => $"http://{IpAddress}:{Port}";

        // url base https del nodo (no se serializa)
        [JsonIgnore]
        public string BaseUrlHttps => $"https://{IpAddress}:{HttpsPort}";

        // url base efectiva segun configuracion (no se serializa)
        [JsonIgnore]
        public string EffectiveBaseUrl => UseHttps ? BaseUrlHttps : BaseUrl;

        // capacidad en formato legible (no se serializa)
        [JsonIgnore]
        public string MaxStorageFormatted => FormatBytes(MaxStorageBytes);

        // ruta completa para almacenar bloques (no se serializa)
        [JsonIgnore]
        public string BlocksPath => Path.Combine(StoragePath, SystemConstants.BLOCKS_SUBDIRECTORY);

        // ruta completa para almacenar metadatos (no se serializa)
        [JsonIgnore]
        public string MetadataPath => Path.Combine(StoragePath, SystemConstants.METADATA_SUBDIRECTORY);

        // ================================
        // constructores
        // ================================

        // constructor por defecto
        public NodeConfiguration() { }



        // constructor que auto-configura puertos segun nodeid
        public NodeConfiguration(int nodeId)
        {
            NodeId = nodeId;
            Port = SystemConstants.GetDiskNodePort(nodeId);
            HttpsPort = SystemConstants.GetDiskNodeHttpsPort(nodeId);
            StoragePath = Path.Combine(SystemConstants.DEFAULT_STORAGE_PATH, $"node{nodeId}");
        }



        // constructor completo
        public NodeConfiguration(int nodeId, string ipAddress, int port, string storagePath)
        {
            NodeId = nodeId;
            IpAddress = ipAddress;
            Port = port;
            HttpsPort = SystemConstants.GetDiskNodeHttpsPort(nodeId);
            StoragePath = storagePath;
        }

        // ================================
        // metodos publicos
        // ================================

        // valida que la configuracion del nodo sea correcta
        public bool IsValid()
        {
            try
            {
                return NodeId >= 1 &&
                       NodeId <= SystemConstants.RAID_TOTAL_NODES &&
                       !string.IsNullOrWhiteSpace(IpAddress) &&
                       Port > 0 && Port <= 65535 &&
                       HttpsPort > 0 && HttpsPort <= 65535 &&
                       Port != HttpsPort &&
                       !string.IsNullOrWhiteSpace(StoragePath) &&
                       MaxStorageBytes > 0 &&
                       TimeoutMs > 0 &&
                       MaxRetries > 0;
            }
            catch
            {
                return false;
            }
        }



        // crea los directorios necesarios para el nodo
        public bool CreateDirectories()
        {
            try
            {
                Directory.CreateDirectory(StoragePath);
                Directory.CreateDirectory(BlocksPath);
                Directory.CreateDirectory(MetadataPath);
                return true;
            }
            catch
            {
                return false;
            }
        }



        // obtiene el espacio disponible en el directorio de almacenamiento
        public long GetAvailableSpace()
        {
            try
            {
                var drive = new DriveInfo(Path.GetPathRoot(Path.GetFullPath(StoragePath))!);
                return Math.Min(drive.AvailableFreeSpace, MaxStorageBytes);
            }
            catch
            {
                return -1;
            }
        }



        // clona la configuracion del nodo
        public NodeConfiguration Clone()
        {
            return new NodeConfiguration
            {
                NodeId = NodeId,
                IpAddress = IpAddress,
                Port = Port,
                HttpsPort = HttpsPort,
                StoragePath = StoragePath,
                MaxStorageBytes = MaxStorageBytes,
                IsActive = IsActive,
                UseHttps = UseHttps,
                TimeoutMs = TimeoutMs,
                MaxRetries = MaxRetries
            };
        }



        // representacion en string del nodo
        public override string ToString()
        {
            var status = IsActive ? "si" : "no";
            var protocol = UseHttps ? "HTTPS" : "HTTP";
            return $"{status} Node {NodeId}: {EffectiveBaseUrl} ({protocol}) -> {StoragePath} [{MaxStorageFormatted}]";
        }



        // compara dos configuraciones de nodo
        public override bool Equals(object? obj)
        {
            return obj is NodeConfiguration other && NodeId == other.NodeId;
        }



        // hash code basado en nodeid
        public override int GetHashCode()
        {
            return NodeId.GetHashCode();
        }

        // ================================
        // metodos privados
        // ================================

        // formatea bytes en formato legible (b, kb, mb, gb)
        private static string FormatBytes(long bytes)
        {
            if (bytes < 1024) return $"{bytes} B";
            if (bytes < 1048576) return $"{bytes / 1024.0:F1} KB";
            if (bytes < 1073741824) return $"{bytes / 1048576.0:F1} MB";
            return $"{bytes / 1073741824.0:F1} GB";
        }
    }
}