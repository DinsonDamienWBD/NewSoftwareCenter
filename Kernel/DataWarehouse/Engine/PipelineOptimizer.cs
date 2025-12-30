using System.Linq;
using DataWarehouse.Contracts;
using DataWarehouse.Primitives;
using Core.Data;

namespace DataWarehouse.Engine
{
    /// <summary>
    /// Optimize pipeline for best performance and effeciency
    /// </summary>
    /// <param name="registry"></param>
    public class PipelineOptimizer(PluginRegistry registry)
    {
        private readonly PluginRegistry _registry = registry;

        /// <summary>
        /// Resolve a storage intent
        /// </summary>
        /// <param name="intent"></param>
        /// <returns></returns>
        public PipelineConfig Resolve(StorageIntent intent)
        {
            var config = new PipelineConfig();

            // 1. Compression
            if (intent.Compression != CompressionLevel.None)
            {
                var algo = _registry.CompressionAlgos
                    .OrderByDescending(x => x.Level == intent.Compression)
                    .ThenByDescending(x => x.Level)
                    .FirstOrDefault();
                config.CompressionAlgo = algo?.Id ?? "None";
            }

            // 2. Security
            if (intent.Security != SecurityLevel.None)
            {
                var algo = _registry.CryptoAlgos
                    .OrderByDescending(x => x.Level == intent.Security)
                    .ThenByDescending(x => x.Level)
                    .FirstOrDefault();
                config.CryptoAlgo = algo?.Id ?? "AES-GCM";
            }

            return config;
        }
    }
}