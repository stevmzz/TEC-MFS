using System;

namespace TecMFS.Common.Constants
{
    // constantes fundamentales del sistema
    // estas nunca deben cambiar durante la ejecucion
    public static class SystemConstants
    {
        // ================================
        // CONFIGURACION RAID 5
        // ================================

        // numero total de nodos en el sistema raid 5
        public const int RAID_TOTAL_NODES = 4;

        // numero de nodos dedicados a paridad
        public const int RAID_PARITY_NODES = 1;

        // numero de nodos dedicados a datos
        public const int RAID_DATA_NODES = RAID_TOTAL_NODES - RAID_PARITY_NODES; // = 3

        // ================================
        // TAMAÑOS Y LIMITES DE DATOS
        // ================================

        // tamano por defecto de cada bloque: 64kb
        public const int DEFAULT_BLOCK_SIZE = 65536;

        // tamano minimo de bloque: 1kb
        public const int MIN_BLOCK_SIZE = 1024;

        // tamano maximo de bloque: 1mb
        public const int MAX_BLOCK_SIZE = 1048576;

        // tamano maximo de archivo: 100mb
        public const long MAX_FILE_SIZE = 104857600;

        // capacidad maxima por nodo: 10gb
        public const long MAX_NODE_STORAGE = 10737418240;

        // ================================
        // CONFIGURACIONES DE RED
        // ================================

        // puerto base del controller
        public const int CONTROLLER_PORT = 5100;

        // puerto https del controller
        public const int CONTROLLER_HTTPS_PORT = 5443;

        // puerto base para disknodes (5001, 5002, 5003, 5004)
        public const int DISKNODE_BASE_PORT = 5001;

        // puerto https base para disknodes (5444, 5445, 5446, 5447)
        public const int DISKNODE_HTTPS_BASE_PORT = 5444;

        // ================================
        // TIMEOUTS Y RETRIES
        // ================================

        // timeout por defecto para requests http: 10 segundos
        public const int DEFAULT_REQUEST_TIMEOUT_MS = 10000;

        // intervalo para health checks: 30 segundos
        public const int HEALTH_CHECK_INTERVAL_SECONDS = 30;

        // maximo de intentos para operaciones fallidas
        public const int MAX_RETRY_ATTEMPTS = 3;

        // minutos antes de considerar un nodo como fallido
        public const int NODE_FAILURE_THRESHOLD_MINUTES = 2;

        // ================================
        // ARCHIVOS Y CONTENIDOS
        // ================================

        // extension de archivo permitida
        public const string ALLOWED_FILE_EXTENSION = ".pdf";

        // content-type por defecto
        public const string DEFAULT_CONTENT_TYPE = "application/pdf";

        // ================================
        // PATHS Y DIRECTORIOS
        // ================================

        // directorio base para almacenamiento
        public const string DEFAULT_STORAGE_PATH = "./raid_storage";

        // subdirectorio para bloques de datos
        public const string BLOCKS_SUBDIRECTORY = "blocks";

        // subdirectorio para metadatos
        public const string METADATA_SUBDIRECTORY = "metadata";

        // ================================
        // CATEGORIAS DE LOG
        // ================================

        // categoria de log para operaciones raid
        public const string LOG_CATEGORY_RAID = "RAID";

        // categoria de log para operaciones de almacenamiento
        public const string LOG_CATEGORY_STORAGE = "Storage";

        // categoria de log para operaciones de red
        public const string LOG_CATEGORY_NETWORK = "Network";

        // categoria de log para operaciones de paridad
        public const string LOG_CATEGORY_PARITY = "Parity";

        // ================================
        // METODOS HELPERS PARA CALCULOS
        // ================================

        // calcula el puerto http de un disknode especifico
        public static int GetDiskNodePort(int nodeId)
        {
            ValidateNodeId(nodeId);
            return DISKNODE_BASE_PORT + (nodeId - 1);
        }



        // calcula el puerto https de un disknode especifico
        public static int GetDiskNodeHttpsPort(int nodeId)
        {
            ValidateNodeId(nodeId);
            return DISKNODE_HTTPS_BASE_PORT + (nodeId - 1);
        }



        // genera la url base completa de un disknode
        public static string GetDiskNodeBaseUrl(int nodeId, string ipAddress = "localhost", bool useHttps = false)
        {
            ValidateNodeId(nodeId);
            var protocol = useHttps ? "https" : "http";
            var port = useHttps ? GetDiskNodeHttpsPort(nodeId) : GetDiskNodePort(nodeId);
            return $"{protocol}://{ipAddress}:{port}";
        }



        // genera la url base del controller
        public static string GetControllerBaseUrl(string ipAddress = "localhost", bool useHttps = false)
        {
            var protocol = useHttps ? "https" : "http";
            var port = useHttps ? CONTROLLER_HTTPS_PORT : CONTROLLER_PORT;
            return $"{protocol}://{ipAddress}:{port}";
        }



        // calcula cuantos bloques necesita un archivo de tamano dado
        public static int CalculateBlockCount(long fileSize, int blockSize = DEFAULT_BLOCK_SIZE)
        {
            if (fileSize <= 0) return 0;
            return (int)Math.Ceiling((double)fileSize / blockSize);
        }



        // calcula en que nodo debe ir un bloque especifico segun raid 5
        public static int CalculateNodeForBlock(int blockIndex, bool isParityBlock = false)
        {
            if (isParityBlock)
            {
                // rotacion de paridad: bloque 0 → nodo 4, bloque 1 → nodo 3, etc.
                return RAID_TOTAL_NODES - (blockIndex % RAID_TOTAL_NODES);
            }
            else
            {
                // distribucion de datos: evitar el nodo de paridad
                var parityNode = CalculateNodeForBlock(blockIndex, true);
                var dataNodeIndex = blockIndex % RAID_DATA_NODES;

                // ajustar si coincide con nodo de paridad
                var targetNode = dataNodeIndex + 1;
                if (targetNode >= parityNode) targetNode++;

                return targetNode;
            }
        }



        // valida si un archivo cumple con las restricciones del sistema
        public static bool IsValidFile(string fileName, long fileSize)
        {
            return !string.IsNullOrWhiteSpace(fileName) &&
                   fileName.EndsWith(ALLOWED_FILE_EXTENSION, StringComparison.OrdinalIgnoreCase) &&
                   fileSize > 0 &&
                   fileSize <= MAX_FILE_SIZE;
        }



        // valida si un nodeid esta en el rango valido (1-4)
        private static void ValidateNodeId(int nodeId)
        {
            if (nodeId < 1 || nodeId > RAID_TOTAL_NODES)
            {
                throw new ArgumentException($"NodeId debe estar entre 1 y {RAID_TOTAL_NODES}, recibido: {nodeId}");
            }
        }
    }
}