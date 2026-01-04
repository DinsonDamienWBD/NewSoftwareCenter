using DataWarehouse.Kernel.Primitives;
using DataWarehouse.SDK.Primitives;
using System.Collections.Concurrent;
using System.Collections.Generic;

namespace DataWarehouse.Kernel.Engine
{
    /// <summary>
    /// Resolves the processing pipeline for specific data paths.
    /// Supports granular rules (File -> Folder -> Container).
    /// </summary>
    public class PolicyEnforcer
    {
        // Store policies by path pattern
        private readonly ConcurrentDictionary<string, PipelineConfig> _pathPolicies = new();
        private readonly PipelineConfig _defaultPolicy;

        /// <summary>
        /// Initializes the Policy Enforcer with Global Defaults.
        /// </summary>
        public PolicyEnforcer(GlobalPolicyConfig config)
        {
            _defaultPolicy = new PipelineConfig
            {
                // Default Order (fallback to Compress -> Encrypt)
                TransformationOrder = config.DefaultPipelineOrder ?? ["Compression", "Encryption"],

                // Default Booleans
                EnableCompression = config.DefaultEnableCompression,
                EnableEncryption = config.DefaultEnableEncryption,

                // [FIX] Use correct property names matching your PipelineConfig definition
                CompressionProviderId = "standard-gzip",
                CryptoProviderId = "standard-aes"
            };
        }

        /// <summary>
        /// Set policy
        /// </summary>
        /// <param name="pathPattern"></param>
        /// <param name="config"></param>
        public void SetPolicy(string pathPattern, PipelineConfig config)
        {
            _pathPolicies[pathPattern] = config;
        }

        /// <summary>
        /// Resolve pipeline operation order
        /// </summary>
        /// <param name="container"></param>
        /// <param name="blobName"></param>
        /// <returns></returns>
        public PipelineConfig ResolvePipeline(string container, string blobName)
        {
            string fullPath = $"{container}/{blobName}";

            // 1. Exact File Match
            if (_pathPolicies.TryGetValue(fullPath, out var filePolicy)) return filePolicy;

            // 2. Folder Match (Traverse up)
            string current = fullPath;
            while (current.Contains('/'))
            {
                current = current[..current.LastIndexOf('/')];
                if (_pathPolicies.TryGetValue(current, out var folderPolicy)) return folderPolicy;
            }

            // 3. Container Match
            if (_pathPolicies.TryGetValue(container, out var containerPolicy)) return containerPolicy;

            // 4. Default
            return _defaultPolicy;
        }
    }
}