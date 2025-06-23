using System;
using System.IO;
using System.Xml;
using TecMFS.Common.Constants;

namespace TecMFS.DiskNode.Services
{
    // maneja la configuracion xml de los disknodes leyendo parametros de red y almacenamiento
    public class ConfigManager
    {
        private readonly ILogger<ConfigManager>? _logger;

        public string IpAddress { get; private set; } = "localhost";
        public int Port { get; private set; } = SystemConstants.DISKNODE_BASE_PORT;
        public string StoragePath { get; private set; } = SystemConstants.DEFAULT_STORAGE_PATH;
        public int NodeId { get; private set; } = 1;

        public ConfigManager(ILogger<ConfigManager>? logger = null)
        {
            _logger = logger;
        }



        // carga configuracion desde archivo xml con validacion y creacion automatica
        public bool LoadConfiguration(string configFilePath)
        {
            try
            {
                if (!File.Exists(configFilePath))
                {
                    _logger?.LogWarning($"Archivo de configuracion no encontrado: {configFilePath}, usando valores por defecto");
                    CreateDefaultConfig(configFilePath);
                    return LoadConfiguration(configFilePath);
                }

                _logger?.LogInformation($"Cargando configuracion desde: {configFilePath}");

                var doc = new XmlDocument();
                doc.Load(configFilePath);

                var root = doc.DocumentElement;
                if (root?.Name != "DiskNodeConfig")
                {
                    _logger?.LogError("Formato de configuracion XML invalido");
                    return false;
                }

                // leer nodeid
                if (int.TryParse(root.GetAttribute("nodeId"), out int nodeId))
                {
                    NodeId = nodeId;
                }

                // leer ip y puerto
                var networkNode = root.SelectSingleNode("Network");
                if (networkNode != null)
                {
                    IpAddress = networkNode.SelectSingleNode("IP")?.InnerText ?? IpAddress;

                    if (int.TryParse(networkNode.SelectSingleNode("Port")?.InnerText, out int port))
                    {
                        Port = port;
                    }
                }

                // leer path de almacenamiento
                var storageNode = root.SelectSingleNode("Storage");
                if (storageNode != null)
                {
                    StoragePath = storageNode.SelectSingleNode("Path")?.InnerText ?? StoragePath;
                }

                // crear directorio si no existe
                if (!Directory.Exists(StoragePath))
                {
                    Directory.CreateDirectory(StoragePath);
                    _logger?.LogInformation($"Directorio de almacenamiento creado: {StoragePath}");
                }

                _logger?.LogInformation($"Configuracion cargada exitosamente - NodeId: {NodeId}, IP: {IpAddress}, Puerto: {Port}, Path: {StoragePath}");
                return true;
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, $"Error cargando configuracion desde {configFilePath}");
                return false;
            }
        }



        // crea archivo de configuracion xml por defecto con valores basicos
        private void CreateDefaultConfig(string configFilePath)
        {
            try
            {
                var defaultConfig = $@"<?xml version=""1.0"" encoding=""utf-8""?>
                    <DiskNodeConfig nodeId=""1"">
                        <Network>
                            <IP>localhost</IP>
                            <Port>{SystemConstants.DISKNODE_BASE_PORT}</Port>
                        </Network>
                        <Storage>
                            <Path>{SystemConstants.DEFAULT_STORAGE_PATH}/node1</Path>
                        </Storage>
                    </DiskNodeConfig>";

                // crear directorio si no existe
                var directory = Path.GetDirectoryName(configFilePath);
                if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
                {
                    Directory.CreateDirectory(directory);
                }

                File.WriteAllText(configFilePath, defaultConfig);
                _logger?.LogInformation($"Archivo de configuracion por defecto creado: {configFilePath}");
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, $"Error creando configuracion por defecto: {configFilePath}");
            }
        }



        // valida que la configuracion cargada sea correcta y completa
        public bool IsValidConfiguration()
        {
            var isValid = !string.IsNullOrWhiteSpace(IpAddress) &&
                         Port > 0 && Port <= 65535 &&
                         !string.IsNullOrWhiteSpace(StoragePath) &&
                         NodeId >= 1 && NodeId <= SystemConstants.RAID_TOTAL_NODES;

            if (!isValid)
            {
                _logger?.LogError($"Configuracion invalida - IP: {IpAddress}, Puerto: {Port}, Path: {StoragePath}, NodeId: {NodeId}");
            }

            return isValid;
        }



        // obtiene la url base completa del nodo para comunicacion http
        public string GetBaseUrl()
        {
            return $"http://{IpAddress}:{Port}";
        }
    }
}