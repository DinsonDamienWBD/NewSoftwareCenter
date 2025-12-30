using DataWarehouse.Contracts;
using DataWarehouse.Primitives;
using Microsoft.Extensions.Logging;

namespace DataWarehouse.Fabric
{
    /// <summary>
    /// The Hypervisor. It turns 10 different drives into 1 massive, tiered drive.
    /// </summary>
    public class UnifiedStoragePool
    {
        // The physical locations (Nodes)
        private readonly List<StorageNode> _nodes = new();
        private readonly ILogger _logger;

        /// <summary>
        /// FAST CHECK: Used by Kernel to skip logic
        /// </summary>
        public bool IsSingleNode => _nodes.Count == 1;

        /// <summary>
        /// Logger for the storage pool
        /// </summary>
        /// <param name="logger"></param>
        public UnifiedStoragePool(ILogger logger) => _logger = logger;

        /// <summary>
        /// Attach a DW node
        /// </summary>
        /// <param name="provider"></param>
        /// <param name="tier"></param>
        public void AttachNode(IStorageProvider provider, StorageTier tier)
        {
            _nodes.Add(new StorageNode(provider, tier));
            _logger.LogInformation($"Attached {provider.Scheme} to Tier {tier}");
        }

        /// <summary>
        /// The "Zero-Overhead" Path.
        /// Bypasses Tiering, Load Balancing, and Intent Parsing.
        /// </summary>
        public async Task<string> WriteDirectAsync(Stream data)
        {
            var node = _nodes[0]; // No lookup
            string id = Guid.NewGuid().ToString("N");
            var uri = new Uri($"{node.Provider.Scheme}://pool/{id}");

            await node.Provider.SaveAsync(uri, data);
            return uri.ToString();
        }

        /// <summary>
        /// Intelligent Write: Routes data to the correct device based on Intent.
        /// </summary>
        public async Task<Uri> WriteBlobAsync(Stream data, StorageIntent intent)
        {
            // 1. Select Tier based on Performance/Availability Intent
            var targetTier = SelectTier(intent);

            // 2. Select Best Node (Load Balancing / Free Space)
            var node = _nodes
                .Where(n => n.Tier == targetTier)
                .OrderByDescending(n => n.FreeSpace) // Simple load balancing
                .FirstOrDefault()
                ?? _nodes.First(); // Fallback

            // 3. Generate Content-Addressable URI (CAS)
            // We use the HASH of the content as the ID. This enables native Deduplication.
            // (Note: In production, we'd hash the stream while writing. Here we assume pre-calc or seekable)
            string hash = "temp-" + Guid.NewGuid(); // Placeholder for Stream Hash

            var blobUri = new Uri($"{node.Provider.Scheme}://pool/{hash}");

            await node.Provider.SaveAsync(blobUri, data);

            return blobUri;
        }

        private StorageTier SelectTier(StorageIntent intent)
        {
            if (intent.Compression == CompressionLevel.None) return StorageTier.Hot; // Assume Low Latency needed
            if (intent.Availability == AvailabilityLevel.GeoRedundant) return StorageTier.Cold; // Assume Archive
            return StorageTier.Warm;
        }

        // Inner class to track device metadata
        private record StorageNode(IStorageProvider Provider, StorageTier Tier)
        {
            public long FreeSpace { get; set; } = 1024L * 1024 * 1024 * 1024; // Mock 1TB
        }
    }

    /// <summary>
    /// Storage tier
    /// </summary>
    public enum StorageTier 
    { 
        /// <summary>
        /// Hot tier
        /// </summary>
        Hot, 

        /// <summary>
        /// Warm tier
        /// </summary>
        Warm, 

        /// <summary>
        /// Cold tier
        /// </summary>
        Cold 
    }
}