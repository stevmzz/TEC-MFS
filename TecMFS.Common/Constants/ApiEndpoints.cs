using System;

namespace TecMFS.Common.Constants
{
    // endpoints de api para comunicacion entre componentes
    public static class ApiEndpoints
    {
        // ================================
        // CONTROLLER ENDPOINTS
        // ================================

        // base path para endpoints de archivos
        public const string FILES_BASE = "/api/files";

        // endpoint para subir archivos pdf
        // post con uploadrequest en body
        public const string FILES_UPLOAD = $"{FILES_BASE}/upload";

        // endpoint para descargar archivos
        // get /api/files/download/{filename}
        public const string FILES_DOWNLOAD = $"{FILES_BASE}/download";

        // endpoint para eliminar archivos
        // delete /api/files/delete/{filename}
        public const string FILES_DELETE = $"{FILES_BASE}/delete";

        // endpoint para listar todos los archivos
        // get que retorna list<filemetadata>
        public const string FILES_LIST = $"{FILES_BASE}/list";

        // endpoint para obtener informacion de un archivo
        // get /api/files/info/{filename}
        public const string FILES_INFO = $"{FILES_BASE}/info";

        // endpoint para buscar archivos por nombre
        // get /api/files/search?query={searchterm}
        public const string FILES_SEARCH = $"{FILES_BASE}/search";

        // ================================
        // STATUS ENDPOINTS
        // ================================

        // base path para endpoints de estado
        public const string STATUS_BASE = "/api/status";

        // endpoint para estado general del raid
        // get que retorna statusresponse
        public const string STATUS_RAID = $"{STATUS_BASE}/raid";

        // endpoint para estado de todos los nodos
        // get que retorna list<nodestatus>
        public const string STATUS_NODES = $"{STATUS_BASE}/nodes";

        // endpoint de health check del controller
        // get que retorna status simple
        public const string STATUS_HEALTH = $"{STATUS_BASE}/health";

        // endpoint para estadisticas detalladas
        // get que retorna metricas del sistema
        public const string STATUS_STATS = $"{STATUS_BASE}/stats";

        // ================================
        // DISKNODE ENDPOINTS
        // ================================

        // base path para endpoints de bloques
        public const string BLOCKS_BASE = "/api/blocks";

        // endpoint para almacenar un bloque
        // post con blockrequest en body
        public const string BLOCKS_STORE = $"{BLOCKS_BASE}/store";

        // endpoint para recuperar un bloque
        // get /api/blocks/retrieve/{blockid}
        public const string BLOCKS_RETRIEVE = $"{BLOCKS_BASE}/retrieve";

        // endpoint para eliminar un bloque
        // delete /api/blocks/delete/{blockid}
        public const string BLOCKS_DELETE = $"{BLOCKS_BASE}/delete";

        // endpoint para verificar si existe un bloque
        // head /api/blocks/exists/{blockid}
        public const string BLOCKS_EXISTS = $"{BLOCKS_BASE}/exists";

        // endpoint para listar bloques del nodo
        // get que retorna list<string> con blockids
        public const string BLOCKS_LIST = $"{BLOCKS_BASE}/list";

        // endpoint de health check del disknode
        // get que retorna nodestatus
        public const string BLOCKS_HEALTH = $"{BLOCKS_BASE}/health";

        // endpoint para informacion del nodo
        // get que retorna informacion de almacenamiento
        public const string BLOCKS_INFO = $"{BLOCKS_BASE}/info";

        // endpoint para verificar integridad de bloques
        // post para verificar checksums
        public const string BLOCKS_VERIFY = $"{BLOCKS_BASE}/verify";

        // ================================
        // METODOS HELPER PARA CONTRUIR URLS
        // ================================

        // construye url para descargar un archivo especifico
        public static string GetFileDownloadUrl(string fileName)
        {
            return $"{FILES_DOWNLOAD}/{fileName}";
        }



        // construye url para eliminar un archivo especifico
        public static string GetFileDeleteUrl(string fileName)
        {
            return $"{FILES_DELETE}/{fileName}";
        }



        // construye url para obtener informacion de un archivo
        public static string GetFileInfoUrl(string fileName)
        {
            return $"{FILES_INFO}/{fileName}";
        }



        // construye url para buscar archivos
        public static string GetFileSearchUrl(string searchQuery)
        {
            return $"{FILES_SEARCH}?query={Uri.EscapeDataString(searchQuery)}";
        }



        // construye url para recuperar un bloque especifico
        public static string GetBlockRetrieveUrl(string blockId)
        {
            return $"{BLOCKS_RETRIEVE}/{blockId}";
        }



        // construye url para eliminar un bloque especifico
        public static string GetBlockDeleteUrl(string blockId)
        {
            return $"{BLOCKS_DELETE}/{blockId}";
        }



        // construye url para verificar existencia de un bloque
        public static string GetBlockExistsUrl(string blockId)
        {
            return $"{BLOCKS_EXISTS}/{blockId}";
        }



        // construye url completa para un endpoint de disknode
        public static string GetDiskNodeUrl(int nodeId, string endpoint, bool useHttps = false)
        {
            var baseUrl = SystemConstants.GetDiskNodeBaseUrl(nodeId, "localhost", useHttps);
            return $"{baseUrl}{endpoint}";
        }



        // construye url completa para un endpoint del controller
        public static string GetControllerUrl(string endpoint, bool useHttps = false)
        {
            var baseUrl = SystemConstants.GetControllerBaseUrl("localhost", useHttps);
            return $"{baseUrl}{endpoint}";
        }
    }
}