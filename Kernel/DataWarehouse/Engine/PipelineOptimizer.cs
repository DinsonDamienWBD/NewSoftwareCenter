using Core.Data;
using DataWarehouse.Contracts;
using DataWarehouse.Primitives;
using Microsoft.Extensions.Configuration;
using System.Linq;

namespace DataWarehouse.Engine
{
    /// <summary>
    /// Optimize pipeline for best performance and effeciency
    /// </summary>
    /// <param name="registry"></param>
    /// <param name="config"></param>
    public class PipelineOptimizer(PluginRegistry registry, IConfiguration config)
    {
        private readonly PluginRegistry _registry = registry;

        private readonly IConfiguration _config = config;

        /// <summary>
        /// Resolve a storage intent
        /// </summary>
        /// <param name="intent"></param>
        /// <returns></returns>
        public PipelineConfig Resolve(StorageIntent intent)
        {
            var pipeline = new PipelineConfig();

            // 1. Resolve Compression
            if (intent.Compression != CompressionLevel.None)
            {
                // Prefer user config algo, else fallback to best match
                string preferred = _config["Defaults:Compression"] ?? "";

                var algo = _registry.CompressionAlgos
                    .OrderByDescending(x => x.Id.Equals(preferred, StringComparison.OrdinalIgnoreCase)) // Priority 1: Config Match
                    .ThenByDescending(x => x.Level == intent.Compression)                               // Priority 2: Level Match
                    .ThenByDescending(x => x.Level)                                                     // Priority 3: Highest Level
                    .FirstOrDefault();

                pipeline.CompressionAlgo = algo?.Id ?? "None";
            }

            // 2. Resolve Encryption
            if (intent.Security != SecurityLevel.None)
            {
                string preferred = _config["Defaults:Encryption"] ?? "";

                var algo = _registry.CryptoAlgos
                    .OrderByDescending(x => x.Id.Equals(preferred, StringComparison.OrdinalIgnoreCase))
                    .ThenByDescending(x => x.Level == intent.Security)
                    .ThenByDescending(x => x.Level) // Default to strongest if exact match fails
                    .FirstOrDefault();

                pipeline.CryptoAlgo = algo?.Id ?? "AES-GCM"; // Hard fallback if registry empty
            }

            // 3. Resolve Order (Runtime Configurable)
            // appsettings.json: { "Pipeline": { "Order": ["Encryption", "Compression"] } }
            var configuredOrder = _config.GetSection("Pipeline:Order").Get<List<string>>();

            if (configuredOrder != null && configuredOrder.Count > 0)
            {
                pipeline.TransformationOrder = configuredOrder;
            }
            else
            {
                // Default: Compress -> Encrypt
                pipeline.TransformationOrder = ["Compression", "Encryption"];
            }

            return pipeline;
        }
    }
}