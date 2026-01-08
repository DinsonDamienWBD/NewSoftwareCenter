using DataWarehouse.SDK.Contracts;
using System.Diagnostics;

namespace DataWarehouse.Kernel.Resources
{
    /// <summary>
    /// Monitors memory pressure and automatically responds to prevent OutOfMemory exceptions.
    /// Implements tiered response strategy: cache eviction → batch size reduction → GC → operation throttling.
    /// </summary>
    public class MemoryPressureManager
    {
        private readonly IKernelContext _context;
        private readonly object _lock = new();
        private CancellationTokenSource? _monitorCancellation;

        // Thresholds (percentage of total memory)
        private readonly double _warningThreshold = 0.70;   // 70% - start gentle responses
        private readonly double _criticalThreshold = 0.85;  // 85% - aggressive responses
        private readonly double _severeThreshold = 0.95;    // 95% - emergency measures

        // Configuration
        private readonly TimeSpan _monitorInterval = TimeSpan.FromSeconds(5);
        private readonly long _totalMemoryBytes;

        // State
        private MemoryPressureLevel _currentLevel = MemoryPressureLevel.Normal;
        private DateTime _lastLevelChange = DateTime.UtcNow;
        private int _consecutiveCriticalChecks = 0;

        // Event callbacks
        private readonly List<Action<MemoryPressureLevel>> _pressureChangeHandlers = new();
        private readonly List<Func<long, Task<long>>> _cacheEvictionHandlers = new();

        public MemoryPressureLevel CurrentLevel => _currentLevel;

        public MemoryPressureManager(IKernelContext context)
        {
            _context = context ?? throw new ArgumentNullException(nameof(context));

            // Get total system memory
            var gcMemInfo = GC.GetGCMemoryInfo();
            _totalMemoryBytes = gcMemInfo.TotalAvailableMemoryBytes;

            _context.LogInfo($"[MemoryPressure] Initialized with {FormatBytes(_totalMemoryBytes)} total memory");
        }

        /// <summary>
        /// Start memory pressure monitoring.
        /// </summary>
        public Task StartAsync(CancellationToken cancellationToken)
        {
            _monitorCancellation = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            _context.LogInfo("[MemoryPressure] Started monitoring");

            _ = Task.Run(async () => await MonitorLoopAsync(_monitorCancellation.Token), cancellationToken);

            return Task.CompletedTask;
        }

        /// <summary>
        /// Stop memory pressure monitoring.
        /// </summary>
        public Task StopAsync()
        {
            _monitorCancellation?.Cancel();
            _context.LogInfo("[MemoryPressure] Stopped monitoring");
            return Task.CompletedTask;
        }

        /// <summary>
        /// Register handler for pressure level changes.
        /// </summary>
        public void RegisterPressureChangeHandler(Action<MemoryPressureLevel> handler)
        {
            lock (_lock)
            {
                _pressureChangeHandlers.Add(handler);
            }
        }

        /// <summary>
        /// Register cache eviction handler.
        /// Handler should evict at least the requested bytes and return actual bytes freed.
        /// </summary>
        public void RegisterCacheEvictionHandler(Func<long, Task<long>> handler)
        {
            lock (_lock)
            {
                _cacheEvictionHandlers.Add(handler);
            }
        }

        /// <summary>
        /// Get current memory statistics.
        /// </summary>
        public MemoryStatistics GetStatistics()
        {
            var gcInfo = GC.GetGCMemoryInfo();
            var process = Process.GetCurrentProcess();

            var usedBytes = gcInfo.HeapSizeBytes;
            var availableBytes = _totalMemoryBytes - usedBytes;
            var usagePercent = (double)usedBytes / _totalMemoryBytes;

            return new MemoryStatistics
            {
                Timestamp = DateTime.UtcNow,
                TotalMemoryBytes = _totalMemoryBytes,
                UsedMemoryBytes = usedBytes,
                AvailableMemoryBytes = availableBytes,
                UsagePercent = usagePercent,
                PressureLevel = _currentLevel,
                ProcessWorkingSet = process.WorkingSet64,
                Gen0Collections = GC.CollectionCount(0),
                Gen1Collections = GC.CollectionCount(1),
                Gen2Collections = GC.CollectionCount(2)
            };
        }

        /// <summary>
        /// Check if operation should be throttled based on memory pressure.
        /// </summary>
        public bool ShouldThrottle()
        {
            return _currentLevel >= MemoryPressureLevel.Severe;
        }

        /// <summary>
        /// Get recommended batch size based on current memory pressure.
        /// </summary>
        public int GetRecommendedBatchSize(int defaultBatchSize)
        {
            return _currentLevel switch
            {
                MemoryPressureLevel.Normal => defaultBatchSize,
                MemoryPressureLevel.Warning => defaultBatchSize / 2,
                MemoryPressureLevel.Critical => defaultBatchSize / 4,
                MemoryPressureLevel.Severe => defaultBatchSize / 8,
                _ => defaultBatchSize
            };
        }

        private async Task MonitorLoopAsync(CancellationToken cancellationToken)
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                try
                {
                    await Task.Delay(_monitorInterval, cancellationToken);
                    await CheckMemoryPressureAsync();
                }
                catch (OperationCanceledException)
                {
                    break;
                }
                catch (Exception ex)
                {
                    _context.LogError("[MemoryPressure] Monitor loop error", ex);
                }
            }
        }

        private async Task CheckMemoryPressureAsync()
        {
            var stats = GetStatistics();
            var newLevel = DeterminePressureLevel(stats.UsagePercent);

            if (newLevel != _currentLevel)
            {
                var oldLevel = _currentLevel;
                _currentLevel = newLevel;
                _lastLevelChange = DateTime.UtcNow;

                _context.LogWarning($"[MemoryPressure] Level changed: {oldLevel} → {newLevel} (Usage: {stats.UsagePercent:P1})");

                // Notify handlers
                NotifyPressureChange(newLevel);

                // Take action based on new level
                await RespondToPressureAsync(newLevel, stats);
            }

            // Track consecutive critical checks
            if (newLevel >= MemoryPressureLevel.Critical)
            {
                _consecutiveCriticalChecks++;
            }
            else
            {
                _consecutiveCriticalChecks = 0;
            }

            // Log periodic status
            if (_consecutiveCriticalChecks > 0 || newLevel >= MemoryPressureLevel.Warning)
            {
                _context.LogDebug($"[MemoryPressure] Status: {newLevel}, Usage: {stats.UsagePercent:P1}, Available: {FormatBytes(stats.AvailableMemoryBytes)}");
            }
        }

        private MemoryPressureLevel DeterminePressureLevel(double usagePercent)
        {
            if (usagePercent >= _severeThreshold)
                return MemoryPressureLevel.Severe;
            if (usagePercent >= _criticalThreshold)
                return MemoryPressureLevel.Critical;
            if (usagePercent >= _warningThreshold)
                return MemoryPressureLevel.Warning;
            return MemoryPressureLevel.Normal;
        }

        private async Task RespondToPressureAsync(MemoryPressureLevel level, MemoryStatistics stats)
        {
            switch (level)
            {
                case MemoryPressureLevel.Warning:
                    await HandleWarningLevelAsync(stats);
                    break;

                case MemoryPressureLevel.Critical:
                    await HandleCriticalLevelAsync(stats);
                    break;

                case MemoryPressureLevel.Severe:
                    await HandleSevereLevelAsync(stats);
                    break;

                case MemoryPressureLevel.Normal:
                    // Recovery - no action needed
                    _context.LogInfo("[MemoryPressure] Memory pressure returned to normal");
                    break;
            }
        }

        private async Task HandleWarningLevelAsync(MemoryStatistics stats)
        {
            _context.LogInfo($"[MemoryPressure] WARNING level response (Usage: {stats.UsagePercent:P1})");

            // Phase 1: Gentle cache eviction (10% of cache)
            var targetEviction = stats.UsedMemoryBytes / 10;
            await EvictCacheAsync(targetEviction);
        }

        private async Task HandleCriticalLevelAsync(MemoryStatistics stats)
        {
            _context.LogWarning($"[MemoryPressure] CRITICAL level response (Usage: {stats.UsagePercent:P1})");

            // Phase 1: Aggressive cache eviction (25% of cache)
            var targetEviction = stats.UsedMemoryBytes / 4;
            var evicted = await EvictCacheAsync(targetEviction);

            // Phase 2: Force garbage collection
            _context.LogInfo("[MemoryPressure] Forcing GC.Collect(2)");
            GC.Collect(2, GCCollectionMode.Aggressive, blocking: true, compacting: true);
            GC.WaitForPendingFinalizers();

            _context.LogInfo($"[MemoryPressure] Critical response complete, evicted {FormatBytes(evicted)}");
        }

        private async Task HandleSevereLevelAsync(MemoryStatistics stats)
        {
            _context.LogError($"[MemoryPressure] SEVERE level response - EMERGENCY (Usage: {stats.UsagePercent:P1})", null);

            // Phase 1: Emergency cache flush (50% of cache)
            var targetEviction = stats.UsedMemoryBytes / 2;
            var evicted = await EvictCacheAsync(targetEviction);

            // Phase 2: Aggressive GC
            _context.LogWarning("[MemoryPressure] Emergency GC collection");
            GC.Collect(2, GCCollectionMode.Forced, blocking: true, compacting: true);
            GC.WaitForPendingFinalizers();
            GC.Collect(2, GCCollectionMode.Forced, blocking: true, compacting: true);

            // Phase 3: Reduce large object heap fragmentation
            System.Runtime.GCSettings.LargeObjectHeapCompactionMode = System.Runtime.GCLargeObjectHeapCompactionMode.CompactOnce;

            _context.LogWarning($"[MemoryPressure] Severe response complete, evicted {FormatBytes(evicted)}");

            // If still in severe state after 3 consecutive checks, consider shutting down non-critical operations
            if (_consecutiveCriticalChecks > 3)
            {
                _context.LogError("[MemoryPressure] CRITICAL: Memory pressure remains severe after 3 checks. Consider increasing system memory.", null);
            }
        }

        private async Task<long> EvictCacheAsync(long targetBytes)
        {
            long totalEvicted = 0;

            List<Func<long, Task<long>>> handlers;
            lock (_lock)
            {
                handlers = _cacheEvictionHandlers.ToList();
            }

            foreach (var handler in handlers)
            {
                try
                {
                    var evicted = await handler(targetBytes - totalEvicted);
                    totalEvicted += evicted;

                    if (totalEvicted >= targetBytes)
                        break;
                }
                catch (Exception ex)
                {
                    _context.LogError("[MemoryPressure] Cache eviction handler failed", ex);
                }
            }

            if (totalEvicted > 0)
            {
                _context.LogInfo($"[MemoryPressure] Evicted {FormatBytes(totalEvicted)} from caches");
            }

            return totalEvicted;
        }

        private void NotifyPressureChange(MemoryPressureLevel newLevel)
        {
            List<Action<MemoryPressureLevel>> handlers;
            lock (_lock)
            {
                handlers = _pressureChangeHandlers.ToList();
            }

            foreach (var handler in handlers)
            {
                try
                {
                    handler(newLevel);
                }
                catch (Exception ex)
                {
                    _context.LogError("[MemoryPressure] Pressure change handler failed", ex);
                }
            }
        }

        private static string FormatBytes(long bytes)
        {
            string[] sizes = ["B", "KB", "MB", "GB", "TB"];
            double len = bytes;
            int order = 0;
            while (len >= 1024 && order < sizes.Length - 1)
            {
                order++;
                len /= 1024;
            }
            return $"{len:0.##} {sizes[order]}";
        }
    }

    public enum MemoryPressureLevel
    {
        Normal,    // < 70% - all systems go
        Warning,   // 70-85% - gentle cache eviction
        Critical,  // 85-95% - aggressive cache eviction + GC
        Severe     // > 95% - emergency measures + throttling
    }

    public class MemoryStatistics
    {
        public DateTime Timestamp { get; init; }
        public long TotalMemoryBytes { get; init; }
        public long UsedMemoryBytes { get; init; }
        public long AvailableMemoryBytes { get; init; }
        public double UsagePercent { get; init; }
        public MemoryPressureLevel PressureLevel { get; init; }
        public long ProcessWorkingSet { get; init; }
        public int Gen0Collections { get; init; }
        public int Gen1Collections { get; init; }
        public int Gen2Collections { get; init; }
    }
}
