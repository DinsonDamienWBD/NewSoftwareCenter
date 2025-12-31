using DataWarehouse.Contracts;
using DataWarehouse.Drivers;
using DataWarehouse.Primitives;
using Microsoft.Extensions.Logging;

namespace DataWarehouse.Fabric
{
    /// <summary>
    /// The Hypervisor. It turns 10 different drives into 1 massive, tiered drive.
    /// </summary>
    /// <remarks>
    /// Logger for the storage pool
    /// </remarks>
    /// <param name="logger"></param>
    public class UnifiedStoragePool(ILogger logger)
    {
        /// <summary>
        /// ID
        /// </summary>
        public static string Id => "UnifiedStoragePool";

        /// <summary>
        /// Version
        /// </summary>
        public static string Version => "1.0";

        // The physical locations (Nodes)
        private readonly List<StorageNode> _nodes = [];
        private readonly ILogger _logger = logger;

        /// <summary>
        /// FAST CHECK: Used by Kernel to skip logic
        /// </summary>
        public bool IsSingleNode => _nodes.Count == 1;

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
        /// Attach a mirrored node
        /// </summary>
        /// <param name="primary"></param>
        /// <param name="secondary"></param>
        /// <param name="tier"></param>
        public void AttachMirroredNode(IStorageProvider primary, IStorageProvider secondary, StorageTier tier)
        {
            // Wrap them in the Governor
            var mirror = new SelfHealingMirror(primary, secondary, _logger);
            _nodes.Add(new StorageNode(mirror, tier));
            _logger.LogInformation($"Attached MIRROR ({primary.Id}+{secondary.Id}) to Tier {tier}");
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

        private static StorageTier SelectTier(StorageIntent intent)
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

        /// <summary>
        /// Moves data from one tier to another (e.g., NVMe -> S3) and returns the new URI.
        /// </summary>
        public async Task<string> MoveToTierAsync(Manifest manifest, StorageTier targetTier)
        {
            var sourceUri = new Uri(manifest.BlobUri);

            // 1. Find Source Provider
            // We match based on the URI Scheme (e.g., "file", "s3")
            var sourceNode = _nodes.FirstOrDefault(n => n.Provider.Scheme.Equals(sourceUri.Scheme, StringComparison.OrdinalIgnoreCase)) ?? throw new InvalidOperationException($"Source provider for scheme '{sourceUri.Scheme}' not attached to pool.");

            // 2. Find Target Provider
            var targetNode = _nodes.FirstOrDefault(n => n.Tier == targetTier);
            if (targetNode == null)
            {
                _logger.LogWarning("No storage node found for tier {Tier}. Move cancelled.", targetTier);
                return manifest.BlobUri; // No-op
            }

            if (sourceNode == targetNode) return manifest.BlobUri; // Already there

            // 3. Construct New URI
            // Preserve the path structure: file:///bucket/key -> s3:///bucket/key
            // Note: Some providers might need bucket mapping logic here. 
            // For V1 Kernel, we assume path compatibility.
            var relativePath = sourceUri.AbsolutePath.TrimStart('/');
            var targetUriString = $"{targetNode.Provider.Scheme}://{relativePath}";
            var targetUri = new Uri(targetUriString);

            _logger.LogInformation("[Tiering] Moving {Id} from {Source} to {Target}", manifest.Id, sourceNode.Tier, targetTier);

            // 4. Copy (Load -> Save)
            using (var stream = await sourceNode.Provider.LoadAsync(sourceUri))
            {
                await targetNode.Provider.SaveAsync(targetUri, stream);
            }

            // 5. Verify & Delete Old
            // (Paranoid check: Ensure exists before deleting old)
            if (await targetNode.Provider.ExistsAsync(targetUri))
            {
                await sourceNode.Provider.DeleteAsync(sourceUri);
                return targetUriString;
            }
            else
            {
                throw new IOException("Tier move failed: Target write unverified.");
            }
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