using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.ComponentModel.DataAnnotations;
using Newtonsoft.Json;
using TecMFS.Common.Models;

namespace TecMFS.Common.Models
{
    public class NodeStatus
    {
        [Required]
        [Range(1, 4)]
        [JsonProperty("nodeId")]
        public int NodeId { get; set; }

        [Required]
        [JsonProperty("isOnline")]
        public bool IsOnline { get; set; } = false;

        [JsonProperty("baseUrl")]
        public string BaseUrl { get; set; } = string.Empty;

        [Range(0, long.MaxValue)]
        [JsonProperty("totalStorage")]
        public long TotalStorage { get; set; }

        [Range(0, long.MaxValue)]
        [JsonProperty("usedStorage")]
        public long UsedStorage { get; set; }

        [JsonProperty("blockCount")]
        public int BlockCount { get; set; }

        [JsonProperty("lastHeartbeat")]
        public DateTime LastHeartbeat { get; set; } = DateTime.UtcNow;

        [JsonProperty("responseTimeMs")]
        public double ResponseTimeMs { get; set; }

        [JsonProperty("errorCount")]
        public int ErrorCount { get; set; } = 0;

        [JsonProperty("version")]
        public string Version { get; set; } = "1.0.0";

        [JsonIgnore]
        public long AvailableStorage => TotalStorage - UsedStorage;

        [JsonIgnore]
        public double StorageUsagePercentage => TotalStorage > 0 ? (double)UsedStorage / TotalStorage * 100 : 0;

        [JsonIgnore]
        public bool IsHealthy => IsOnline && ErrorCount < 5 && (DateTime.UtcNow - LastHeartbeat).TotalMinutes < 2;
    }
}
