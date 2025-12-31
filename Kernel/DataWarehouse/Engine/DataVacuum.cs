using DataWarehouse.Contracts;
using Microsoft.Extensions.Logging;
using System.Diagnostics;

namespace DataWarehouse.Engine
{
    /// <summary>
    /// The Garbage Collector.
    /// Scans for "Zombie Data" (Blobs with no Manifest) and removes them to save cost/space.
    /// </summary>
    public class DataVacuum(
        IMetadataIndex index,
        PluginRegistry registry,
        ILogger<DataVacuum> logger)
    {
        private readonly IMetadataIndex _index = index;
        private readonly PluginRegistry _registry = registry;
        private readonly ILogger _logger = logger;

        /// <summary>
        /// Runs a full Garbage Collection cycle.
        /// Recommended to run this daily or weekly via a BackgroundService.
        /// </summary>
        public async Task RunCycleAsync(CancellationToken ct = default)
        {
            var sw = Stopwatch.StartNew();
            _logger.LogInformation("[Vacuum] Starting Garbage Collection Cycle...");

            try
            {
                // 1. Build Allow List
                var validBlobUris = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                long manifestCount = 0;

                await foreach (var manifest in _index.EnumerateAllAsync().WithCancellation(ct))
                {
                    validBlobUris.Add(manifest.BlobUri);
                    manifestCount++;
                }

                // [FIX CA1873/CA2254] Structured Logging
                _logger.LogInformation("[Vacuum] Found {Count} active manifests.", manifestCount);

                long deletedCount = 0;
                long reclaimedBytes = 0; // [FEATURE] Now we track this!

                // Reflection access to providers (as discussed in previous turn)

                if (_registry.GetType()
                    .GetField("_storage", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
                    ?.GetValue(_registry) is List<IStorageProvider> storageProviders)
                {
                    foreach (var provider in storageProviders)
                    {
                        if (provider is IListableStorage listable)
                        {
                            // [FIX CA1873/CA2254]
                            _logger.LogInformation("[Vacuum] Scanning provider: {ProviderId}...", provider.Id);

                            // [CHANGED] Now iterating StorageListItem (Uri + Size)
                            await foreach (var item in listable.ListFilesAsync().WithCancellation(ct))
                            {
                                string uriString = item.Uri.ToString();

                                if (!validBlobUris.Contains(uriString))
                                {
                                    try
                                    {
                                        await provider.DeleteAsync(item.Uri);

                                        deletedCount++;
                                        reclaimedBytes += item.SizeBytes; // [FEATURE] Math

                                        // [FIX CA1873/CA2254]
                                        _logger.LogDebug("[Vacuum] Deleted orphan: {Uri} ({Size} bytes)", uriString, item.SizeBytes);
                                    }
                                    catch (Exception ex)
                                    {
                                        // [FIX CA1873/CA2254]
                                        _logger.LogWarning(ex, "[Vacuum] Failed to delete orphan {Uri}", uriString);
                                    }
                                }
                            }
                        }
                    }
                }

                sw.Stop();

                // [FIX CA1873/CA2254] & [FEATURE] Report bytes
                _logger.LogInformation(
                    "[Vacuum] Cycle Complete. Deleted {Deleted} orphans ({Bytes} bytes) in {Duration}s.",
                    deletedCount,
                    reclaimedBytes,
                    sw.Elapsed.TotalSeconds);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[Vacuum] Critical failure during garbage collection.");
                throw;
            }
        }
    }
}