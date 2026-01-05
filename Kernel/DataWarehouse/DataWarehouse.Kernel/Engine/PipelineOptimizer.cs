using DataWarehouse.Kernel.Primitives;
using DataWarehouse.SDK.Contracts;
using DataWarehouse.SDK.Primitives;
using DataWarehouse.SDK.Services;
using Microsoft.Extensions.Configuration;
using System;
using System.Collections.Generic;
using System.Linq;

namespace DataWarehouse.Kernel.Engine
{
    /// <summary>
    /// Translates high-level User Intent (StorageIntent) into low-level Pipeline Configuration.
    /// Selects the best available plugins based on Category and Quality.
    /// </summary>
    public class PipelineOptimizer(PluginRegistry registry, IConfiguration config)
    {
        private readonly PluginRegistry _registry = registry;
        private readonly IConfiguration _config = config;

        /// <summary>
        /// Resolve storage intent
        /// </summary>
        /// <param name="intent"></param>
        /// <returns></returns>
        public PipelineConfig Resolve(StorageIntent intent)
        {
            var pipeline = new PipelineConfig();

            // 1. Resolve Compression
            if (intent.Compression != CompressionLevel.None)
            {
                pipeline.EnableCompression = true;
                string preferred = _config["Defaults:Compression"] ?? "";

                // V8 Logic: Find any IDataTransformation with Category = "Compression"
                var algo = _registry.GetPlugins<IDataTransformation>()
                    .Where(p => p.Category.Equals("Compression", StringComparison.OrdinalIgnoreCase))
                    .OrderByDescending(x => x.Id.Equals(preferred, StringComparison.OrdinalIgnoreCase)) // 1. Config Match
                    .OrderByDescending(x => x.QualityLevel) // 2. Best Quality
                    .FirstOrDefault();

                pipeline.CompressionProviderId = algo?.Id ?? "standard-gzip";
            }

            // 2. Resolve Encryption
            if (intent.Security != SecurityLevel.None)
            {
                pipeline.EnableEncryption = true;
                string preferred = _config["Defaults:Encryption"] ?? "";

                // V8 Logic: Find any IDataTransformation with Category = "Encryption"
                var algo = _registry.GetPlugins<IDataTransformation>()
                    .Where(p => p.Category.Equals("Encryption", StringComparison.OrdinalIgnoreCase))
                    .OrderByDescending(x => x.Id.Equals(preferred, StringComparison.OrdinalIgnoreCase))
                    .OrderByDescending(x => x.QualityLevel)
                    .FirstOrDefault();

                pipeline.CryptoProviderId = algo?.Id ?? "standard-aes";
            }

            // 3. Resolve Order
            // We read the generic "TransformationOrder" from config. 
            // This enables users to inject "MyCustomPlugin" into the JSON list without code changes.
            var configuredOrder = _config.GetSection("Pipeline:Order").Get<List<string>>();

            pipeline.TransformationOrder = (configuredOrder != null && configuredOrder.Count > 0)
                ? configuredOrder
                : ["Compression", "Encryption"]; // Default V8 Order

            return pipeline;
        }
    }
}