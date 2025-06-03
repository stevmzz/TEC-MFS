using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;

namespace TecMFS.Common.Configuration
{
    // clase que representa la configuracion del sistema raid distribuido para almacenamiento de archivos
    public class RaidConfiguration
    {
        // numero total de nodos en el sistema, debe ser entre 3 y 10
        [Range(3, 10)]
        [JsonProperty("totalNodes")]
        public int TotalNodes { get; set; } = 4;

        // numero de nodos de paridad (usados para redundancia), debe ser entre 1 y 3
        [Range(1, 3)]
        [JsonProperty("parityNodes")]
        public int ParityNodes { get; set; } = 1;

        // tamano de cada bloque en bytes, debe estar entre 1024 (1 kb) y 1048576 (1 mb)
        [Range(1024, 1048576)]
        [JsonProperty("blockSize")]
        public int BlockSize { get; set; } = 65536;

        // tamano maximo permitido por archivo en bytes, debe ser al menos 1 kb
        [Range(1024, long.MaxValue)]
        [JsonProperty("maxFileSize")]
        public long MaxFileSize { get; set; } = 104857600; // 100 mb

        // capacidad maxima de almacenamiento por nodo en bytes, debe ser al menos 1 gb
        [Range(1073741824, long.MaxValue)]
        [JsonProperty("maxNodeStorage")]
        public long MaxNodeStorage { get; set; } = 10737418240; // 10 gb

        // lista de tipos de archivo permitidos (por extension)
        [JsonProperty("allowedFileTypes")]
        public List<string> AllowedFileTypes { get; set; } = new List<string> { ".pdf" };

        // intervalo de verificacion de salud en segundos, entre 1 y 60
        [Range(1, 60)]
        [JsonProperty("healthCheckIntervalSeconds")]
        public int HealthCheckIntervalSeconds { get; set; } = 30;

        // cantidad maxima de intentos de reintento para operaciones fallidas
        [Range(1, 10)]
        [JsonProperty("maxRetryAttempts")]
        public int MaxRetryAttempts { get; set; } = 3;

        // tiempo limite para solicitudes en milisegundos, entre 1000 y 30000
        [Range(1000, 30000)]
        [JsonProperty("requestTimeoutMs")]
        public int RequestTimeoutMs { get; set; } = 10000;

        // propiedad calculada que indica la cantidad de nodos destinados a datos (no se incluye en el json)
        [JsonIgnore]
        public int DataNodes => TotalNodes - ParityNodes;

        // propiedad calculada que muestra el porcentaje de eficiencia de almacenamiento (no se incluye en el json)
        [JsonIgnore]
        public double StorageEfficiency => (double)DataNodes / TotalNodes * 100;

        // propiedad calculada que indica la cantidad maxima de bloques permitidos por archivo (no se incluye en el json)
        [JsonIgnore]
        public int MaxBlocksPerFile => (int)(MaxFileSize / BlockSize) + 1;

        // propiedad calculada que valida si la configuracion es logica y funcional (no se incluye en el json)
        [JsonIgnore]
        public bool IsValidConfiguration => TotalNodes >= 3 && ParityNodes >= 1 && DataNodes >= 2 && BlockSize > 0;
    }
}
