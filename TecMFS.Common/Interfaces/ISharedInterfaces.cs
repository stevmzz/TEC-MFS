using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using TecMFS.Common.DTOs;
using TecMFS.Common.Models;

namespace TecMFS.Common.Interfaces
{
    // ================================
    // INTERFACE PARA RAID MANAGER (JOSE LO IMPLEMENTARA)
    // ================================

    // contrato que define como debe funcionar el gestor de raid
    // jose implementara esto en tecmfs.controller
    public interface IRaidManager
    {
        // almacena un archivo en el sistema raid distribuyendo bloques
        // request: solicitud con archivo y metadatos
        // returns: true si se almaceno exitosamente
        Task<bool> StoreFileAsync(UploadRequest request);

        // recupera un archivo completo del sistema raid
        // filename: nombre del archivo a recuperar
        // returns: contenido binario del archivo o null si no existe
        Task<byte[]?> RetrieveFileAsync(string fileName);

        // elimina un archivo del sistema raid
        // filename: nombre del archivo a eliminar
        // returns: true si se elimino exitosamente
        Task<bool> DeleteFileAsync(string fileName);

        // obtiene lista de todos los archivos almacenados
        // returns: lista de metadatos de archivos
        Task<List<FileMetadata>> ListFilesAsync();

        // busca archivos por nombre o criterio
        // searchQuery: termino de busqueda
        // returns: lista de archivos que coinciden
        Task<List<FileMetadata>> SearchFilesAsync(string searchQuery);

        // obtiene metadatos de un archivo especifico
        // filename: nombre del archivo
        // returns: metadatos del archivo o null si no existe
        Task<FileMetadata?> GetFileMetadataAsync(string fileName);

        // obtiene estado completo del sistema raid
        // returns: estado y estadisticas del raid
        Task<StatusResponse> GetRaidStatusAsync();

        // recupera datos cuando un nodo falla (tolerancia a fallos)
        // failednodeid: id del nodo que fallo (1-4)
        // returns: true si la recuperacion fue exitosa
        Task<bool> RecoverFromNodeFailureAsync(int failedNodeId);

        // verifica integridad de todos los archivos
        // returns: true si todos los archivos estan integros
        Task<bool> VerifySystemIntegrityAsync();
    }

    // ================================
    // INTERFACE PARA BLOCK STORAGE (SEBAS LO IMPLEMENTARA)
    // ================================

    // contrato que define como debe funcionar el almacenamiento de bloques
    // sebas implementara esto en tecmfs.disknode
    public interface IBlockStorage
    {
        // almacena un bloque de datos en el nodo
        // blockid: identificador unico del bloque
        // blockdata: contenido binario del bloque
        // returns: true si se almaceno exitosamente
        Task<bool> StoreBlockAsync(string blockId, byte[] blockData);

        // recupera un bloque de datos del nodo
        // blockid: identificador del bloque a recuperar
        // returns: contenido del bloque o null si no existe
        Task<byte[]?> RetrieveBlockAsync(string blockId);

        // elimina un bloque del nodo
        // blockid: identificador del bloque a eliminar
        // returns: true si se elimino exitosamente
        Task<bool> DeleteBlockAsync(string blockId);

        // verifica si un bloque existe en el nodo
        // blockid: identificador del bloque
        // returns: true si el bloque existe
        Task<bool> BlockExistsAsync(string blockId);

        // obtiene lista de todos los bloques almacenados
        // returns: lista de identificadores de bloques
        Task<List<string>> ListBlocksAsync();

        // obtiene espacio disponible en el nodo
        // returns: bytes disponibles para almacenamiento
        Task<long> GetAvailableSpaceAsync();

        // obtiene espacio usado en el nodo
        // returns: bytes actualmente ocupados
        Task<long> GetUsedSpaceAsync();

        // verifica integridad de un bloque usando checksum
        // blockid: identificador del bloque
        // expectedchecksum: checksum esperado
        // returns: true si el bloque esta integro
        Task<bool> VerifyBlockIntegrityAsync(string blockId, string expectedChecksum);

        // limpia bloques huerfanos o corruptos
        // returns: numero de bloques limpiados
        Task<int> CleanupOrphanedBlocksAsync();
    }

    // ================================
    // INTERFACE PARA PARITY CALCULATOR (SEBAS LO IMPLEMENTARA)
    // ================================

    // contrato que define como calcular paridad para raid 5
    // sebas implementara esto en tecmfs.disknode
    public interface IParityCalculator
    {
        // calcula bloque de paridad a partir de bloques de datos
        // datablocks: lista de bloques de datos
        // returns: bloque de paridad calculado
        byte[] CalculateParity(List<byte[]> dataBlocks);

        // recupera un bloque de datos perdido usando paridad
        // availableblocks: bloques que aun estan disponibles
        // parityblock: bloque de paridad
        // missingblockindex: indice del bloque que se perdio
        // returns: bloque de datos recuperado
        byte[] RecoverDataBlock(List<byte[]> availableBlocks, byte[] parityBlock, int missingBlockIndex);

        // verifica si la paridad es correcta
        // datablocks: bloques de datos originales
        // parityblock: bloque de paridad a verificar
        // returns: true si la paridad es correcta
        bool VerifyParity(List<byte[]> dataBlocks, byte[] parityBlock);

        // calcula checksum de un bloque
        // blockdata: contenido del bloque
        // returns: checksum como string
        string CalculateChecksum(byte[] blockData);

        // verifica checksum de un bloque
        // blockdata: contenido del bloque
        // expectedchecksum: checksum esperado
        // returns: true si el checksum coincide
        bool VerifyChecksum(byte[] blockData, string expectedChecksum);
    }

    // ================================
    // INTERFACE PARA NODE HEALTH MONITOR (JOSE LO IMPLEMENTARA)
    // ================================

    // contrato para monitorear salud de nodos
    // jose implementara esto en tecmfs.controller
    public interface INodeHealthMonitor
    {
        // verifica salud de un nodo especifico
        // nodeid: id del nodo a verificar (1-4)
        // returns: estado del nodo
        Task<NodeStatus> CheckNodeHealthAsync(int nodeId);

        // verifica salud de todos los nodos
        // returns: lista con estado de todos los nodos
        Task<List<NodeStatus>> CheckAllNodesAsync();

        // verifica si un nodo esta disponible
        // nodeid: id del nodo
        // returns: true si el nodo responde
        Task<bool> IsNodeAvailableAsync(int nodeId);

        // inicia monitoreo continuo de nodos
        // intervalseconds: intervalo entre verificaciones
        // returns: task que representa el monitoreo continuo
        Task StartMonitoringAsync(int intervalSeconds);

        // detiene monitoreo continuo
        void StopMonitoring();

        // evento que se dispara cuando un nodo falla
        event EventHandler<NodeFailureEventArgs> NodeFailureDetected;

        // evento que se dispara cuando un nodo se recupera
        event EventHandler<NodeRecoveryEventArgs> NodeRecoveryDetected;
    }

    // ================================
    // INTERFACE PARA HTTP CLIENT SERVICE (STEVEN LO IMPLEMENTARA)
    // ================================

    // contrato para comunicacion http entre componentes
    // steven implementaras esto en tecmfs.controller
    public interface IHttpClientService
    {
        // envia request get y deserializa respuesta
        // url: url completa del endpoint
        // returns: objeto deserializado o default si hay error
        Task<T?> SendGetAsync<T>(string url);

        // envia request post con datos y deserializa respuesta
        // url: url completa del endpoint
        // data: objeto a serializar en el body
        // returns: objeto deserializado o default si hay error
        Task<T?> SendPostAsync<T>(string url, object data);

        // envia request put con datos y deserializa respuesta
        // url: url completa del endpoint
        // data: objeto a serializar en el body
        // returns: objeto deserializado o default si hay error
        Task<T?> SendPutAsync<T>(string url, object data);

        // envia request delete
        // url: url completa del endpoint
        // returns: true si fue exitoso
        Task<bool> SendDeleteAsync(string url);

        // verifica si un endpoint responde (health check)
        // baseurl: url base del servicio
        // returns: true si responde correctamente
        Task<bool> CheckHealthAsync(string baseUrl);

        // envia datos binarios (para archivos)
        // url: url completa del endpoint
        // data: contenido binario
        // contenttype: tipo de contenido
        // returns: respuesta deserializada
        Task<T?> SendBinaryDataAsync<T>(string url, byte[] data, string contentType);

        // configura timeout personalizado
        // timeoutms: timeout en milisegundos
        void SetTimeout(int timeoutMs);

        // configura reintentos automaticos
        // maxretries: numero maximo de reintentos
        // delayms: delay entre reintentos
        void SetRetryPolicy(int maxRetries, int delayMs);
    }

    // ================================
    // EVENTOS PARA NOTIFICACIONES
    // ================================

    // argumentos para evento de fallo de nodo
    public class NodeFailureEventArgs : EventArgs
    {
        public int NodeId { get; set; }
        public string Reason { get; set; } = string.Empty;
        public DateTime FailureTime { get; set; } = DateTime.UtcNow;
        public NodeStatus? LastKnownStatus { get; set; }
    }

    // argumentos para evento de recuperacion de nodo
    public class NodeRecoveryEventArgs : EventArgs
    {
        public int NodeId { get; set; }
        public DateTime RecoveryTime { get; set; } = DateTime.UtcNow;
        public TimeSpan DownTime { get; set; }
        public NodeStatus CurrentStatus { get; set; } = new NodeStatus();
    }
}