using DataWarehouse.Contracts;
using Microsoft.Extensions.Logging;

namespace DataWarehouse.Fabric
{
    /// <summary>
    /// Lifecycle policy
    /// </summary>
    /// <param name="index"></param>
    /// <param name="storage"></param>
    /// <param name="logger"></param>
    public class LifecyclePolicyEngine(
        IMetadataIndex index,
        UnifiedStoragePool storage,
        ILogger logger)
    {
        private readonly IMetadataIndex _index = index;
        private readonly UnifiedStoragePool _storage = storage;
        private readonly ILogger _logger = logger;

        /// <summary>
        /// Run daily audit
        /// </summary>
        /// <returns></returns>
        public async Task RunDailyAuditAsync()
        {
            _logger.LogInformation("[ILM] Starting Lifecycle Audit...");
            var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            long movedCount = 0;

            await foreach (var manifest in _index.EnumerateAllAsync())
            {
                // Policy: "Move to Cold if older than 30 days and not accessed in 7 days"
                long ageSeconds = now - manifest.CreatedAt;
                long idleSeconds = now - manifest.LastAccessedAt; // Requires V3 Manifest field

                // Assuming Manifest has CurrentTier (Added in V2 upgrade)
                if (ageSeconds > 30 * 86400 && manifest.CurrentTier == "Hot")
                {
                    // CALL THE FLESHED OUT METHOD
                    string newUri = await _storage.MoveToTierAsync(manifest, StorageTier.Cold);

                    // Update Metadata
                    if (newUri != manifest.BlobUri)
                    {
                        manifest.BlobUri = newUri;
                        manifest.CurrentTier = "Cold";

                        // Persist change to Index
                        await _index.IndexManifestAsync(manifest);

                        movedCount++;
                    }
                }
            }
            // [FIX CA1873]
            _logger.LogInformation("[ILM] Audit Complete. Moved {Count} items.", movedCount);
        }
    }
}