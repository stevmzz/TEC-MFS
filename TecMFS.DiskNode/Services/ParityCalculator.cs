using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using TecMFS.Common.Interfaces;

namespace TecMFS.DiskNode.Services
{
    // implementacion de calculo de paridad para raid 5 usando operaciones xor
    public class ParityCalculator : IParityCalculator
    {
        private readonly ILogger<ParityCalculator>? _logger;

        public ParityCalculator(ILogger<ParityCalculator>? logger = null)
        {
            _logger = logger;
        }



        // calcula bloque de paridad usando xor de todos los bloques de datos
        public byte[] CalculateParity(List<byte[]> dataBlocks)
        {
            if (dataBlocks == null || !dataBlocks.Any())
            {
                _logger?.LogWarning("No hay bloques de datos para calcular paridad");
                return Array.Empty<byte>();
            }

            _logger?.LogDebug($"Calculando paridad para {dataBlocks.Count} bloques");

            // obtener el tamaño del bloque mas grande
            var maxSize = dataBlocks.Max(b => b.Length);
            var parityBlock = new byte[maxSize];

            // hacer xor de todos los bloques
            foreach (var dataBlock in dataBlocks)
            {
                for (int i = 0; i < dataBlock.Length; i++)
                {
                    parityBlock[i] ^= dataBlock[i];
                }
            }

            _logger?.LogDebug($"Paridad calculada exitosamente - Tamano: {parityBlock.Length} bytes");
            return parityBlock;
        }



        // recupera un bloque perdido usando los bloques disponibles y la paridad
        public byte[] RecoverDataBlock(List<byte[]> availableBlocks, byte[] parityBlock, int missingBlockIndex)
        {
            if (availableBlocks == null || parityBlock == null)
            {
                _logger?.LogError("Bloques disponibles o paridad son nulos");
                return Array.Empty<byte>();
            }

            _logger?.LogInformation($"Recuperando bloque perdido en indice {missingBlockIndex}");

            // el bloque recuperado sera del mismo tamaño que la paridad
            var recoveredBlock = new byte[parityBlock.Length];

            // copiar la paridad al bloque recuperado
            Array.Copy(parityBlock, recoveredBlock, parityBlock.Length);

            // hacer xor con todos los bloques disponibles
            foreach (var availableBlock in availableBlocks)
            {
                for (int i = 0; i < Math.Min(availableBlock.Length, recoveredBlock.Length); i++)
                {
                    recoveredBlock[i] ^= availableBlock[i];
                }
            }

            _logger?.LogInformation($"Bloque recuperado exitosamente - Tamano: {recoveredBlock.Length} bytes");
            return recoveredBlock;
        }



        // verifica si la paridad calculada coincide con la proporcionada
        public bool VerifyParity(List<byte[]> dataBlocks, byte[] parityBlock)
        {
            if (dataBlocks == null || parityBlock == null)
            {
                _logger?.LogWarning("No se puede verificar paridad - datos nulos");
                return false;
            }

            var calculatedParity = CalculateParity(dataBlocks);

            // comparar la paridad calculada con la proporcionada
            if (calculatedParity.Length != parityBlock.Length)
            {
                _logger?.LogWarning("Verificacion de paridad fallo - tamanos diferentes");
                return false;
            }

            for (int i = 0; i < calculatedParity.Length; i++)
            {
                if (calculatedParity[i] != parityBlock[i])
                {
                    _logger?.LogWarning($"Verificacion de paridad fallo en byte {i}");
                    return false;
                }
            }

            _logger?.LogDebug("Verificacion de paridad exitosa");
            return true;
        }



        // calcula checksum sha256 de un bloque para verificacion de integridad
        public string CalculateChecksum(byte[] blockData)
        {
            if (blockData == null || blockData.Length == 0)
            {
                return string.Empty;
            }

            using var sha256 = SHA256.Create();
            var hash = sha256.ComputeHash(blockData);
            var checksum = Convert.ToHexString(hash);

            _logger?.LogDebug($"Checksum calculado para bloque de {blockData.Length} bytes");
            return checksum;
        }



        // verifica checksum de un bloque contra el valor esperado
        public bool VerifyChecksum(byte[] blockData, string expectedChecksum)
        {
            if (string.IsNullOrEmpty(expectedChecksum))
            {
                _logger?.LogWarning("Checksum esperado esta vacio");
                return false;
            }

            var actualChecksum = CalculateChecksum(blockData);
            var isValid = string.Equals(actualChecksum, expectedChecksum, StringComparison.OrdinalIgnoreCase);

            if (!isValid)
            {
                _logger?.LogWarning($"Verificacion de checksum fallo - Esperado: {expectedChecksum}, Actual: {actualChecksum}");
            }
            else
            {
                _logger?.LogDebug("Verificacion de checksum exitosa");
            }

            return isValid;
        }
    }
}