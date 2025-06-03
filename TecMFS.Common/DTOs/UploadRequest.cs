using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.ComponentModel.DataAnnotations;
using Newtonsoft.Json;

namespace TecMFS.Common.DTOs
{
    // clase que representa una solicitud de carga (upload) de archivo
    public class UploadRequest
    {
        // nombre del archivo, obligatorio y con longitud entre 1 y 255 caracteres
        [Required]
        [StringLength(255, MinimumLength = 1)]
        [JsonProperty("fileName")]
        public string FileName { get; set; } = string.Empty;

        // contenido binario del archivo, obligatorio
        [Required]
        [JsonProperty("fileData")]
        public byte[] FileData { get; set; } = Array.Empty<byte>();

        // tipo de contenido del archivo (mime type), por defecto es application/pdf
        [JsonProperty("contentType")]
        public string ContentType { get; set; } = "application/pdf";

        // identificador del cliente que envia el archivo
        [JsonProperty("clientId")]
        public string ClientId { get; set; } = string.Empty;

        // fecha y hora en que se subio el archivo
        [JsonProperty("uploadedAt")]
        public DateTime UploadedAt { get; set; } = DateTime.UtcNow;

        // tamano del archivo en bytes, calculado a partir del arreglo de bytes (no se incluye en el json)
        [JsonIgnore]
        public long FileSize => FileData.Length;

        // indica si el archivo es un pdf valido, segun el tipo de contenido y la extension del nombre (no se incluye en el json)
        [JsonIgnore]
        public bool IsValidPdf => ContentType == "application/pdf" && FileName.EndsWith(".pdf", StringComparison.OrdinalIgnoreCase);

        // propiedad calculada que devuelve el tamano del archivo en formato legible (b, kb o mb) (no se incluye en el json)
        [JsonIgnore]
        public string FileSizeFormatted
        {
            get
            {
                if (FileSize < 1024) return $"{FileSize} B";
                if (FileSize < 1048576) return $"{FileSize / 1024:F1} KB";
                return $"{FileSize / 1048576:F1} MB";
            }
        }
    }
}
