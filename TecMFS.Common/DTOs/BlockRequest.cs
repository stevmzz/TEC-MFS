using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.ComponentModel.DataAnnotations;
using Newtonsoft.Json;

namespace TecMFS.Common.DTOs
{
    // clase que representa una solicitud para operar sobre un bloque de datos en el sistema de almacenamiento distribuido
    public class BlockRequest
    {
        // identificador unico del bloque, obligatorio
        [Required]
        [JsonProperty("blockId")]
        public string BlockId { get; set; } = string.Empty;

        // tipo de operacion a realizar sobre el bloque, obligatorio
        [Required]
        [JsonProperty("operation")]
        public BlockOperation Operation { get; set; }

        // contenido binario del bloque
        [JsonProperty("blockData")]
        public byte[] BlockData { get; set; } = Array.Empty<byte>();

        // indice del bloque dentro del archivo
        [Range(0, int.MaxValue)]
        [JsonProperty("blockIndex")]
        public int BlockIndex { get; set; }

        // indica si el bloque es un bloque de paridad (para redundancia)
        [JsonProperty("isParityBlock")]
        public bool IsParityBlock { get; set; } = false;

        // identificador del archivo al que pertenece el bloque
        [JsonProperty("fileId")]
        public string FileId { get; set; } = string.Empty;

        // suma de verificacion del bloque para validar integridad
        [JsonProperty("checkSum")]
        public string CheckSum { get; set; } = string.Empty;

        // identificador unico de la solicitud, generado por defecto
        [JsonProperty("requestId")]
        public string RequestId { get; set; } = Guid.NewGuid().ToString();

        // fecha y hora en que se realizo la solicitud
        [JsonProperty("timestamp")]
        public DateTime Timestamp { get; set; } = DateTime.UtcNow;

        // tamano del bloque en bytes, calculado a partir del arreglo blockData (no se incluye en el json)
        [JsonIgnore]
        public long BlockSize => BlockData.Length;

        // indica si el bloque contiene datos (no se incluye en el json)
        [JsonIgnore]
        public bool HasData => BlockData.Length > 0;

        // devuelve el tamano del bloque en formato legible (b o kb) (no se incluye en el json)
        [JsonIgnore]
        public string BlockSizeFormatted
        {
            get
            {
                if (BlockSize < 1024) return $"{BlockSize} B";
                return $"{BlockSize / 1024:F1} KB";
            }
        }

        // enumeracion que define los posibles tipos de operaciones sobre un bloque
        public enum BlockOperation
        {
            Store,      // almacenar bloque
            Retrieve,   // recuperar bloque
            Delete,     // eliminar bloque
            Verify,     // verificar bloque
            GetInfo     // obtener informacion del bloque
        }
    }
}
