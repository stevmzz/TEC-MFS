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
    public class FileMetadata
    {
        [Required]
        [JsonProperty("id")]
        public string Id { get; set; } = Guid.NewGuid().ToString();

        [Required]
        [StringLength(255, MinimumLength = 1)]
        [JsonProperty("fileName")]
        public string FileName { get; set; } = string.Empty;

        [Range(1, long.MaxValue)]
        [JsonProperty("fileSize")]
        public long FileSize { get; set; }

        [JsonProperty("uploadDate")]
        public DateTime UploadDate { get; set; } = DateTime.UtcNow;

        [JsonProperty("contentType")]
        public string ContentType { get; set; } = "application/pdf";

        [JsonProperty("blocks")]
        public List<BlockInfo> Blocks { get; set; } = new List<BlockInfo>();

        [JsonProperty("checkSum")]
        public string CheckSum { get; set; } = string.Empty;

        [JsonProperty("isComplete")]
        public bool IsComplete { get; set; } = false;
    }
}