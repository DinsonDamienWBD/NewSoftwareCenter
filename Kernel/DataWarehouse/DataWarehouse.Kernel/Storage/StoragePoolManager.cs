using DataWarehouse.SDK.Contracts;
using DataWarehouse.SDK.Primitives;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace DataWarehouse.Kernel.Storage
{
    /// <summary>
    /// Production-Ready Storage Pool Manager with Multi-Mode Support.
    /// Supports: Independent, Cache (write-through/write-back), Tiered (hot/warm/cold), Pool (RAID-like).
    /// Thread-safe, fault-tolerant, with automatic failover and background migration.
    /// </summary>
    public class StoragePoolManager : IStorageProvider, IDisposable
    {
        public string Id => "kernel-storage-pool";
        public string Version => "1.0.0";
        public string Name => "Storage Pool Manager";
        public string Scheme => "pool";

        private readonly IKernelContext _context;
        private readonly ConcurrentDictionary<string, IStorageProvider> _providers;
        private readonly StoragePoolConfig _config;
        private readonly ConcurrentDictionary<string, TierMetadata> _tierMetadata; // URI -> Tier info
        private readonly Timer? _backgroundWorker;
        private readonly SemaphoreSlim _migrationLock = new(1, 1);
        private readonly RaidEngine? _raidEngine; // Comprehensive RAID support
        private bool _disposed;

        /// <summary>
        /// Storage pool configuration.
        /// </summary>
        public class StoragePoolConfig
        {
            public PoolMode Mode { get; set; } = PoolMode.Independent;
            public string PrimaryProviderId { get; set; } = string.Empty;
            public string CacheProviderId { get; set; } = string.Empty;
            public List<string> TierProviderIds { get; set; } = new(); // Hot -> Warm -> Cold
            public List<string> PoolProviderIds { get; set; } = new(); // For RAID-like
            public CacheStrategy CacheStrategy { get; set; } = CacheStrategy.WriteThrough;
            public int HotTierAccessThreshold { get; set; } = 10; // Access count for hot tier
            public int WarmTierAccessThreshold { get; set; } = 3; // Access count for warm tier
            public TimeSpan MigrationInterval { get; set; } = TimeSpan.FromMinutes(5);
            public int PoolMirrorCount { get; set; } = 2; // For mirroring
            public int PoolStripeSize { get; set; } = 64 * 1024; // 64KB chunks for striping
            public RaidConfiguration? RaidConfig { get; set; } = null; // Comprehensive RAID configuration
        }

        public enum PoolMode
        {
            /// <summary>Use primary provider only</summary>
            Independent,
            /// <summary>Fast cache backed by slower storage</summary>
            Cache,
            /// <summary>Hot/Warm/Cold tiers with automatic migration</summary>
            Tiered,
            /// <summary>RAID-like mirroring or striping across providers</summary>
            Pool
        }

        public enum CacheStrategy
        {
            /// <summary>Write to cache and primary simultaneously</summary>
            WriteThrough,
            /// <summary>Write to cache first, async flush to primary</summary>
            WriteBack
        }

        private class TierMetadata
        {
            public string CurrentTier { get; set; } = "hot"; // hot, warm, cold
            public int AccessCount { get; set; }
            public DateTime LastAccessTime { get; set; } = DateTime.UtcNow;
            public long Size { get; set; }
        }

        /// <summary>
        /// Initialize storage pool manager.
        /// </summary>
        public StoragePoolManager(IKernelContext context, StoragePoolConfig config)
        {
            _context = context;
            _config = config;
            _providers = new ConcurrentDictionary<string, IStorageProvider>();
            _tierMetadata = new ConcurrentDictionary<string, TierMetadata>();

            // Initialize RAID engine if configured
            if (_config.RaidConfig != null && _config.Mode == PoolMode.Pool)
            {
                _raidEngine = new RaidEngine(_config.RaidConfig, context);
                _context.LogInfo($"[StoragePool] RAID engine initialized: {_config.RaidConfig.Level}");
            }

            // Start background migration worker for tiered mode
            if (_config.Mode == PoolMode.Tiered)
            {
                _backgroundWorker = new Timer(
                    async _ => await RunTierMigrationAsync(),
                    null,
                    _config.MigrationInterval,
                    _config.MigrationInterval
                );
            }
        }

        /// <summary>
        /// Register a storage provider with the pool.
        /// </summary>
        public void RegisterProvider(string id, IStorageProvider provider)
        {
            if (_providers.TryAdd(id, provider))
            {
                _context?.LogInfo($"[StoragePool] Registered provider: {id} ({provider.Name})");
            }
            else
            {
                _context?.LogWarning($"[StoragePool] Provider {id} already registered");
            }
        }

        /// <summary>
        /// Unregister a storage provider.
        /// </summary>
        public void UnregisterProvider(string id)
        {
            if (_providers.TryRemove(id, out var provider))
            {
                _context?.LogInfo($"[StoragePool] Unregistered provider: {id}");
                if (provider is IDisposable disposable)
                {
                    disposable.Dispose();
                }
            }
        }

        /// <summary>
        /// Initialize (no-op for pool manager).
        /// </summary>
        public void Initialize(IKernelContext context)
        {
            // Context already set in constructor
        }

        /// <summary>
        /// Save blob to storage pool with mode-specific logic.
        /// </summary>
        public async Task SaveAsync(Uri uri, Stream data)
        {
            switch (_config.Mode)
            {
                case PoolMode.Independent:
                    await SaveIndependentAsync(uri, data);
                    break;

                case PoolMode.Cache:
                    await SaveCacheAsync(uri, data);
                    break;

                case PoolMode.Tiered:
                    await SaveTieredAsync(uri, data);
                    break;

                case PoolMode.Pool:
                    await SavePoolAsync(uri, data);
                    break;

                default:
                    throw new NotSupportedException($"Pool mode {_config.Mode} not supported");
            }
        }

        /// <summary>
        /// Load blob from storage pool with mode-specific logic.
        /// </summary>
        public async Task<Stream> LoadAsync(Uri uri)
        {
            switch (_config.Mode)
            {
                case PoolMode.Independent:
                    return await LoadIndependentAsync(uri);

                case PoolMode.Cache:
                    return await LoadCacheAsync(uri);

                case PoolMode.Tiered:
                    return await LoadTieredAsync(uri);

                case PoolMode.Pool:
                    return await LoadPoolAsync(uri);

                default:
                    throw new NotSupportedException($"Pool mode {_config.Mode} not supported");
            }
        }

        /// <summary>
        /// Delete blob from all providers.
        /// </summary>
        public async Task DeleteAsync(Uri uri)
        {
            var tasks = new List<Task>();

            foreach (var provider in _providers.Values)
            {
                tasks.Add(Task.Run(async () =>
                {
                    try
                    {
                        if (await provider.ExistsAsync(uri))
                        {
                            await provider.DeleteAsync(uri);
                        }
                    }
                    catch (Exception ex)
                    {
                        _context?.LogWarning($"[StoragePool] Failed to delete from {provider.Id}: {ex.Message}");
                    }
                }));
            }

            await Task.WhenAll(tasks);

            // Remove metadata
            _tierMetadata.TryRemove(uri.ToString(), out _);
        }

        /// <summary>
        /// Check if blob exists in any provider.
        /// </summary>
        public async Task<bool> ExistsAsync(Uri uri)
        {
            foreach (var provider in _providers.Values)
            {
                try
                {
                    if (await provider.ExistsAsync(uri))
                        return true;
                }
                catch
                {
                    // Continue checking other providers
                }
            }
            return false;
        }

        // ==================== INDEPENDENT MODE ====================

        private async Task SaveIndependentAsync(Uri uri, Stream data)
        {
            var provider = GetPrimaryProvider();
            await provider.SaveAsync(uri, data);
        }

        private async Task<Stream> LoadIndependentAsync(Uri uri)
        {
            var provider = GetPrimaryProvider();
            return await provider.LoadAsync(uri);
        }

        // ==================== CACHE MODE ====================

        private async Task SaveCacheAsync(Uri uri, Stream data)
        {
            var cache = GetProvider(_config.CacheProviderId);
            var primary = GetProvider(_config.PrimaryProviderId);

            if (_config.CacheStrategy == CacheStrategy.WriteThrough)
            {
                // Write to both simultaneously
                var cacheStream = new MemoryStream();
                var primaryStream = new MemoryStream();

                // Duplicate stream
                await data.CopyToAsync(cacheStream);
                data.Position = 0;
                await data.CopyToAsync(primaryStream);

                cacheStream.Position = 0;
                primaryStream.Position = 0;

                var tasks = new[]
                {
                    cache.SaveAsync(uri, cacheStream),
                    primary.SaveAsync(uri, primaryStream)
                };

                await Task.WhenAll(tasks);
            }
            else // WriteBack
            {
                // Write to cache immediately
                var buffer = new MemoryStream();
                await data.CopyToAsync(buffer);
                buffer.Position = 0;

                await cache.SaveAsync(uri, buffer);

                // Async flush to primary (fire and forget with error handling)
                _ = Task.Run(async () =>
                {
                    try
                    {
                        buffer.Position = 0;
                        await primary.SaveAsync(uri, buffer);
                        _context?.LogInfo($"[StoragePool] WriteBack complete: {uri}");
                    }
                    catch (Exception ex)
                    {
                        _context?.LogError($"[StoragePool] WriteBack failed for {uri}", ex);
                    }
                    finally
                    {
                        buffer.Dispose();
                    }
                });
            }
        }

        private async Task<Stream> LoadCacheAsync(Uri uri)
        {
            var cache = GetProvider(_config.CacheProviderId);
            var primary = GetProvider(_config.PrimaryProviderId);

            try
            {
                // Try cache first
                if (await cache.ExistsAsync(uri))
                {
                    _context?.LogInfo($"[StoragePool] Cache HIT: {uri}");
                    return await cache.LoadAsync(uri);
                }
            }
            catch (Exception ex)
            {
                _context?.LogWarning($"[StoragePool] Cache read failed: {ex.Message}");
            }

            // Cache miss - load from primary
            _context?.LogInfo($"[StoragePool] Cache MISS: {uri}");
            var stream = await primary.LoadAsync(uri);

            // Populate cache asynchronously
            _ = Task.Run(async () =>
            {
                try
                {
                    var buffer = new MemoryStream();
                    await stream.CopyToAsync(buffer);
                    buffer.Position = 0;
                    await cache.SaveAsync(uri, buffer);
                    stream.Position = 0;
                }
                catch (Exception ex)
                {
                    _context?.LogWarning($"[StoragePool] Cache population failed: {ex.Message}");
                }
            });

            return stream;
        }

        // ==================== TIERED MODE ====================

        private async Task SaveTieredAsync(Uri uri, Stream data)
        {
            // New data always goes to hot tier
            var hotProvider = GetTierProvider(0);

            var buffer = new MemoryStream();
            await data.CopyToAsync(buffer);
            buffer.Position = 0;

            await hotProvider.SaveAsync(uri, buffer);

            // Update metadata
            _tierMetadata[uri.ToString()] = new TierMetadata
            {
                CurrentTier = "hot",
                AccessCount = 1,
                LastAccessTime = DateTime.UtcNow,
                Size = buffer.Length
            };
        }

        private async Task<Stream> LoadTieredAsync(Uri uri)
        {
            var uriStr = uri.ToString();

            // Update access metadata
            if (_tierMetadata.TryGetValue(uriStr, out var metadata))
            {
                metadata.AccessCount++;
                metadata.LastAccessTime = DateTime.UtcNow;
            }

            // Try tiers in order: hot -> warm -> cold
            for (int i = 0; i < _config.TierProviderIds.Count; i++)
            {
                var provider = GetTierProvider(i);
                try
                {
                    if (await provider.ExistsAsync(uri))
                    {
                        var stream = await provider.LoadAsync(uri);

                        // If found in cold tier but access count is high, promote to hot
                        if (metadata != null && i > 0 && metadata.AccessCount >= _config.HotTierAccessThreshold)
                        {
                            _ = PromoteBlobAsync(uri, i, 0); // Promote to hot tier
                        }

                        return stream;
                    }
                }
                catch (Exception ex)
                {
                    _context?.LogWarning($"[StoragePool] Tier {i} read failed: {ex.Message}");
                }
            }

            throw new FileNotFoundException($"Blob not found in any tier: {uri}");
        }

        /// <summary>
        /// Background worker for tier migration.
        /// </summary>
        private async Task RunTierMigrationAsync()
        {
            if (!await _migrationLock.WaitAsync(0))
                return; // Already running

            try
            {
                _context?.LogInfo("[StoragePool] Running tier migration...");

                foreach (var entry in _tierMetadata.ToArray())
                {
                    var uri = new Uri(entry.Key);
                    var metadata = entry.Value;

                    // Determine target tier based on access patterns
                    string targetTier;
                    int targetTierIndex;

                    if (metadata.AccessCount >= _config.HotTierAccessThreshold)
                    {
                        targetTier = "hot";
                        targetTierIndex = 0;
                    }
                    else if (metadata.AccessCount >= _config.WarmTierAccessThreshold)
                    {
                        targetTier = "warm";
                        targetTierIndex = Math.Min(1, _config.TierProviderIds.Count - 1);
                    }
                    else
                    {
                        targetTier = "cold";
                        targetTierIndex = _config.TierProviderIds.Count - 1;
                    }

                    // Migrate if tier changed
                    if (metadata.CurrentTier != targetTier)
                    {
                        await MigrateBlobAsync(uri, metadata.CurrentTier, targetTier, targetTierIndex);
                        metadata.CurrentTier = targetTier;
                    }

                    // Decay access count over time
                    metadata.AccessCount = (int)(metadata.AccessCount * 0.9);
                }
            }
            catch (Exception ex)
            {
                _context?.LogError("[StoragePool] Tier migration error", ex);
            }
            finally
            {
                _migrationLock.Release();
            }
        }

        private async Task MigrateBlobAsync(Uri uri, string fromTier, string toTier, int toTierIndex)
        {
            try
            {
                _context?.LogInfo($"[StoragePool] Migrating {uri} from {fromTier} to {toTier}");

                // Find source provider
                IStorageProvider? sourceProvider = null;
                foreach (var provider in _providers.Values)
                {
                    if (await provider.ExistsAsync(uri))
                    {
                        sourceProvider = provider;
                        break;
                    }
                }

                if (sourceProvider == null)
                {
                    _context?.LogWarning($"[StoragePool] Migration failed: source not found for {uri}");
                    return;
                }

                var targetProvider = GetTierProvider(toTierIndex);

                // Copy to target tier
                var stream = await sourceProvider.LoadAsync(uri);
                await targetProvider.SaveAsync(uri, stream);

                // Delete from source tier
                await sourceProvider.DeleteAsync(uri);

                _context?.LogInfo($"[StoragePool] Migration complete: {uri}");
            }
            catch (Exception ex)
            {
                _context?.LogError($"[StoragePool] Migration failed for {uri}", ex);
            }
        }

        private async Task PromoteBlobAsync(Uri uri, int fromTierIndex, int toTierIndex)
        {
            try
            {
                var fromProvider = GetTierProvider(fromTierIndex);
                var toProvider = GetTierProvider(toTierIndex);

                var stream = await fromProvider.LoadAsync(uri);
                await toProvider.SaveAsync(uri, stream);
                await fromProvider.DeleteAsync(uri);

                _context?.LogInfo($"[StoragePool] Promoted {uri} to hot tier");
            }
            catch (Exception ex)
            {
                _context?.LogError($"[StoragePool] Promotion failed for {uri}", ex);
            }
        }

        // ==================== POOL MODE (RAID) ====================

        private async Task SavePoolAsync(Uri uri, Stream data)
        {
            if (_raidEngine != null)
            {
                // Use comprehensive RAID engine
                await _raidEngine.SaveAsync(uri.ToString(), data, GetPoolProviderByIndex);
            }
            else
            {
                // Fallback to basic mirroring (legacy behavior)
                await SavePoolLegacyAsync(uri, data);
            }
        }

        private async Task<Stream> LoadPoolAsync(Uri uri)
        {
            if (_raidEngine != null)
            {
                // Use comprehensive RAID engine
                return await _raidEngine.LoadAsync(uri.ToString(), GetPoolProviderByIndex);
            }
            else
            {
                // Fallback to basic mirroring (legacy behavior)
                return await LoadPoolLegacyAsync(uri);
            }
        }

        /// <summary>
        /// Legacy RAID-1 mirroring implementation (for backward compatibility).
        /// </summary>
        private async Task SavePoolLegacyAsync(Uri uri, Stream data)
        {
            var providers = _config.PoolProviderIds.Select(GetProvider).ToList();

            if (providers.Count < 2)
            {
                throw new InvalidOperationException("Pool mode requires at least 2 providers");
            }

            // RAID-1 Mirroring: Write to multiple providers
            var buffer = new MemoryStream();
            await data.CopyToAsync(buffer);

            var tasks = new List<Task>();
            int mirrorCount = Math.Min(_config.PoolMirrorCount, providers.Count);

            for (int i = 0; i < mirrorCount; i++)
            {
                var providerStream = new MemoryStream();
                buffer.Position = 0;
                await buffer.CopyToAsync(providerStream);
                providerStream.Position = 0;

                var provider = providers[i];
                tasks.Add(provider.SaveAsync(uri, providerStream));
            }

            await Task.WhenAll(tasks);
            _context?.LogInfo($"[StoragePool] Mirrored {uri} to {mirrorCount} providers (legacy)");
        }

        /// <summary>
        /// Legacy RAID-1 mirroring load (for backward compatibility).
        /// </summary>
        private async Task<Stream> LoadPoolLegacyAsync(Uri uri)
        {
            var providers = _config.PoolProviderIds.Select(GetProvider).ToList();

            // Try each provider until one succeeds
            foreach (var provider in providers)
            {
                try
                {
                    if (await provider.ExistsAsync(uri))
                    {
                        return await provider.LoadAsync(uri);
                    }
                }
                catch (Exception ex)
                {
                    _context?.LogWarning($"[StoragePool] Provider {provider.Id} failed: {ex.Message}");
                }
            }

            throw new FileNotFoundException($"Blob not found in any pool provider: {uri}");
        }

        /// <summary>
        /// Get pool provider by index for RAID engine.
        /// </summary>
        private IStorageProvider GetPoolProviderByIndex(int index)
        {
            if (index >= _config.PoolProviderIds.Count)
            {
                throw new IndexOutOfRangeException($"Pool provider index {index} out of range");
            }

            return GetProvider(_config.PoolProviderIds[index]);
        }

        // ==================== HELPER METHODS ====================

        private IStorageProvider GetPrimaryProvider()
        {
            if (string.IsNullOrEmpty(_config.PrimaryProviderId))
            {
                return _providers.Values.FirstOrDefault()
                    ?? throw new InvalidOperationException("No storage providers registered");
            }

            return GetProvider(_config.PrimaryProviderId);
        }

        private IStorageProvider GetProvider(string id)
        {
            if (_providers.TryGetValue(id, out var provider))
                return provider;

            throw new InvalidOperationException($"Storage provider '{id}' not found");
        }

        private IStorageProvider GetTierProvider(int tierIndex)
        {
            if (tierIndex >= _config.TierProviderIds.Count)
            {
                throw new InvalidOperationException($"Tier index {tierIndex} out of range");
            }

            return GetProvider(_config.TierProviderIds[tierIndex]);
        }

        /// <summary>
        /// Get pool statistics.
        /// </summary>
        public Dictionary<string, object> GetStatistics()
        {
            var stats = new Dictionary<string, object>
            {
                ["Mode"] = _config.Mode.ToString(),
                ["RegisteredProviders"] = _providers.Count,
                ["TierMetadataCount"] = _tierMetadata.Count,
                ["CacheStrategy"] = _config.CacheStrategy.ToString()
            };

            // Add RAID statistics if enabled
            if (_raidEngine != null && _config.RaidConfig != null)
            {
                stats["RaidLevel"] = _config.RaidConfig.Level.ToString();
                stats["RaidStripeSize"] = _config.RaidConfig.StripeSize;
                stats["RaidAutoRebuild"] = _config.RaidConfig.AutoRebuild;
                stats["RaidProviderCount"] = _config.RaidConfig.ProviderCount;
            }

            return stats;
        }

        public void Dispose()
        {
            if (_disposed) return;

            _backgroundWorker?.Dispose();
            _migrationLock?.Dispose();
            _raidEngine?.Dispose();

            foreach (var provider in _providers.Values)
            {
                if (provider is IDisposable disposable)
                {
                    disposable.Dispose();
                }
            }

            _providers.Clear();
            _disposed = true;
        }
    }
}
