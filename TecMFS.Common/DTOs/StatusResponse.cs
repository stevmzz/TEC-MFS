using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.ComponentModel.DataAnnotations;
using Newtonsoft.Json;
using TecMFS.Common.Models;
using static TecMFS.Common.DTOs.StatusResponse;

namespace TecMFS.Common.DTOs
{
    // clase que representa la respuesta de estado general del sistema o de un componente
    public class StatusResponse
    {
        // nombre del componente o servicio
        [JsonProperty("componentName")]
        public string ComponentName { get; set; } = string.Empty;

        // estado actual del componente (ej: saludable, error, etc)
        [JsonProperty("status")]
        public ComponentStatus Status { get; set; } = ComponentStatus.Unknown;

        // mensaje informativo adicional sobre el estado
        [JsonProperty("message")]
        public string Message { get; set; } = string.Empty;

        // fecha y hora en que se genero el estado
        [JsonProperty("timestamp")]
        public DateTime Timestamp { get; set; } = DateTime.UtcNow;

        // version del componente o servicio
        [JsonProperty("version")]
        public string Version { get; set; } = "1.0.0";

        // tiempo que ha estado activo el componente
        [JsonProperty("uptime")]
        public TimeSpan Uptime { get; set; }

        // lista de nodos que forman parte del sistema
        [JsonProperty("nodes")]
        public List<NodeStatus> Nodes { get; set; } = new List<NodeStatus>();

        // cantidad total de archivos almacenados
        [JsonProperty("totalFiles")]
        public int TotalFiles { get; set; }

        // cantidad total de bloques almacenados
        [JsonProperty("totalBlocks")]
        public int TotalBlocks { get; set; }

        // espacio de almacenamiento usado (en bytes)
        [JsonProperty("usedStorage")]
        public long UsedStorage { get; set; }

        // espacio total de almacenamiento disponible (en bytes)
        [JsonProperty("totalStorage")]
        public long TotalStorage { get; set; }

        // cantidad de errores registrados
        [JsonProperty("errorCount")]
        public int ErrorCount { get; set; }

        // descripcion del ultimo error registrado
        [JsonProperty("lastError")]
        public string LastError { get; set; } = string.Empty;

        // indica si el componente esta en estado saludable (no se incluye en el json)
        [JsonIgnore]
        public bool IsHealthy => Status == ComponentStatus.Healthy && ErrorCount == 0;

        // cantidad de nodos que estan en linea (no se incluye en el json)
        [JsonIgnore]
        public int OnlineNodes => Nodes.Count(n => n.IsOnline);

        // porcentaje del almacenamiento usado respecto al total (no se incluye en el json)
        [JsonIgnore]
        public double StorageUsagePercentage => TotalStorage > 0 ?
            (double)UsedStorage / TotalStorage * 100 : 0;

        // representa el tiempo activo en formato legible (dias, horas, minutos) (no se incluye en el json)
        [JsonIgnore]
        public string UptimeFormatted => $"{Uptime.Days}d {Uptime.Hours}h {Uptime.Minutes}m";

        // representa el almacenamiento usado y total en formato gb, con porcentaje (no se incluye en el json)
        [JsonIgnore]
        public string StorageFormatted
        {
            get
            {
                var usedGB = UsedStorage / (1024.0 * 1024.0 * 1024.0);
                var totalGB = TotalStorage / (1024.0 * 1024.0 * 1024.0);
                return $"{usedGB:F1} GB / {totalGB:F1} GB ({StorageUsagePercentage:F1}%)";
            }
        }

        // enumeracion que define los posibles estados de un componente
        public enum ComponentStatus
        {
            Unknown,   // estado desconocido
            Healthy,   // funcionando correctamente
            Warning,   // funcionando pero con advertencias
            Error,     // hay errores importantes
            Offline    // fuera de linea
        }
    }
}
