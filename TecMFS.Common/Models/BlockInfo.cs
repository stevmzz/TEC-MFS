using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using System.ComponentModel.DataAnnotations;
using Newtonsoft.Json;

namespace TecMFS.Common.Models
{
    public class BlockInfo
    {
        [Required]
        [JsonProperty("blockId")]
        public string BlockId { get; set; } = string.Empty;

        [Range(1, 4)]
        [JsonProperty("nodeId")]
        public int NodeId { get; set; }

        [JsonProperty("isParityBlock")]
        public bool IsParityBlock { get; set; } = false;

        [Range(0, int.MaxValue)]
        [JsonProperty("blockIndex")]
        public int BlockIndex { get; set; }

        [Range(1, long.MaxValue)]
        [JsonProperty("blockSize")]
        public long BlockSize { get; set; }

        [JsonProperty("checkSum")]
        public string CheckSum { get; set; } = string.Empty;

        [JsonProperty("filePath")]
        public string FilePath { get; set; } = string.Empty;

        [JsonProperty("createdAt")]
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }
}