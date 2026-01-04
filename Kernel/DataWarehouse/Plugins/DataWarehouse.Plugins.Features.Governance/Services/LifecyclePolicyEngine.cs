using DataWarehouse.SDK.Contracts;
using DataWarehouse.SDK.Extensions;
using DataWarehouse.SDK.Primitives;
using Microsoft.Extensions.Logging;

namespace DataWarehouse.Plugins.Features.Governance.Services
{
    /// <summary>
    /// Lifecycle policy
    /// </summary>
    /// <param name="index"></param>
    /// <param name="storage"></param>
    /// <param name="logger"></param>
    public class LifecyclePolicyEngine(
        IMetadataIndex index,
        IKernelContext kernel,
        ILogger logger)
    {
        private readonly IMetadataIndex _index = index;
        private readonly IKernelContext _kernel = kernel;
        private readonly ILogger _logger = logger;

        // Fix: Implement the loop
        public async Task RunLifecycleLoopAsync(CancellationToken ct)
        {
            while (!ct.IsCancellationRequested)
            {
                try
                {
                    _logger.LogInformation("Running Daily Retention Audit...");

                    // 1. Iterate ALL Manifests
                    // (Assuming EnumerateAllAsync is implemented in Index)
                    await foreach (var manifest in _index.EnumerateAllAsync(ct))
                    {
                        // 2. Check WORM Retention
                        if (IsExpired(manifest))
                        {
                            _kernel.LogInfo("Blob {manifestId} expired. Purging...", manifest.Id);
                            await PurgeBlobAsync(manifest);
                        }
                    }

                    // Run every 24 hours
                    await Task.Delay(TimeSpan.FromHours(24), ct);
                }
                catch (OperationCanceledException) { break; }
                catch (Exception ex)
                {
                    _kernel.LogError(ex, "Lifecycle Audit Failed.");
                    // Backoff on error
                    await Task.Delay(TimeSpan.FromMinutes(5), ct);
                }
            }
        }

        private static bool IsExpired(Manifest m)
        {
            // Simple Logic: Look for "RetentionUntil" tag
            if (m.Tags.TryGetValue("RetentionUntil", out var expiryStr) &&
                long.TryParse(expiryStr, out var expiryTicks))
            {
                return DateTime.UtcNow.Ticks > expiryTicks;
            }
            return false;
        }

        private async Task PurgeBlobAsync(Manifest m)
        {
            try
            {
                // Parse the BlobUri to find the correct provider
                // e.g., "file:///default/blob1" -> Scheme: file
                var uri = new Uri(m.BlobUri);

                // Find a provider that supports this scheme
                var providers = _kernel.GetPlugins<IStorageProvider>();
                var provider = providers.FirstOrDefault(p => p.Scheme == uri.Scheme);

                if (provider != null)
                {
                    await provider.DeleteAsync(uri);
                    _kernel.LogInfo("Deleted physical blob: {uri}", uri);
                }
                else
                {
                    _kernel.LogWarning("No provider found for scheme {uriScheme}. Cannot delete {uri}", uri.Scheme, uri);
                }
            }
            catch (Exception ex)
            {
                _kernel.LogError(ex, "Failed to purge blob {mId}", m.Id);
            }
        }

        /// <summary>
        /// Run daily audit
        /// </summary>
        /// <returns></returns>
        public async Task RunDailyAuditAsync()
        {
            _logger.LogInformation("[ILM] Starting Lifecycle Audit...");
            var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            long movedCount = 0;

            // Resolve Storage Plugin dynamically (Lazy Resolution)
            var storage = _kernel.GetPlugins<ITieredStorage>().FirstOrDefault();

            if (storage == null)
            {
                _logger.LogWarning("[ILM] No ITieredStorage plugin found. Skipping Tiering audit.");
                return;
            }

            await foreach (var manifest in _index.EnumerateAllAsync())
            {
                // Policy: "Move to Cold if older than 30 days and not accessed in 7 days"
                long ageSeconds = now - manifest.CreatedAt;
                long idleSeconds = now - manifest.LastAccessedAt; // Requires V3 Manifest field

                // Assuming Manifest has CurrentTier (Added in V2 upgrade)
                if (ageSeconds > 30 * 86400 && manifest.CurrentTier == "Hot")
                {
                    // CALL THE FLESHED OUT METHOD
                    string newUri = await storage.MoveToTierAsync(manifest, StorageTier.Cold);

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
            _kernel.LogInfo("[ILM] Audit Complete. Moved {Count} items.", movedCount);
        }
    }
}