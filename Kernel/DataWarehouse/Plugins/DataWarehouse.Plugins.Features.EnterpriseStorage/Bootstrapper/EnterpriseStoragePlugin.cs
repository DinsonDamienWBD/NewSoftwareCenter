using DataWarehouse.Plugins.Features.EnterpriseStorage.Engine;
using DataWarehouse.Plugins.Features.EnterpriseStorage.Services;
using DataWarehouse.SDK.Contracts;
using DataWarehouse.SDK.Primitives;

namespace DataWarehouse.Plugins.Features.EnterpriseStorage.Bootstrapper
{
    /// <summary>
    /// Bootstrapper for Enterprise storage plugin
    /// </summary>
    public class EnterpriseStoragePlugin : IFeaturePlugin, ITieredStorage
    {
        /// <summary>
        /// ID
        /// </summary>
        public string Id => "DataWarehouse.Features.Enterprise";

        /// <summary>
        /// ID
        /// </summary>
        public string Version => "2.1.0";

        /// <summary>
        /// Name
        /// </summary>
        public string Name => "Enterprise Storage";

        // Services
        private DeduplicationTable? _dedupeTable;
        private UnifiedStoragePool? _storagePool;

        // Runtime State
        private IKernelContext? _context;
        private CancellationTokenSource? _cts;
        private Task? _optimizationTask;

        /// <summary>
        /// Initialize
        /// </summary>
        /// <param name="context"></param>
        public void Initialize(IKernelContext context)
        {
            _context = context;
            context.LogInfo($"[{Id}] Initializing Enterprise Storage Suite...");

            string metadataPath = Path.Combine(context.RootPath, "Metadata");
            string tierConfigPath = Path.Combine(context.RootPath, "Config", "tiering.json");

            // 1. Initialize Deduplication Service
            // Manages the hash table for content-addressable storage
            _dedupeTable = new DeduplicationTable(metadataPath);

            context.LogInfo($"[{Id}] Deduplication & Tiering Services ready.");

            // 2. Initialize Unified Storage Pool (Tiering Manager)
            // This service orchestrates movement between Hot (NVMe) and Cold (S3/HDD) tiers.
            // It relies on the Kernel to provide the list of available raw IStorageProviders.
            _storagePool = new UnifiedStoragePool(context);

            // 3. Register Network Storage (Optional: Only if config exists)
            // In V5, we might check if this node is configured as a Client to another node
            var netProvider = new NetworkStorageProvider();
            netProvider.Initialize(context);

            // Explicitly add to the Pool? 
            // Usually, the Kernel finds it via IStorageProvider interface scanning.
            // If you want to force it into the UnifiedPool immediately:
            _storagePool.RegisterNode(netProvider);

            context.LogInfo($"[{Id}] Network Storage Provider registered.");
        }

        /// <summary>
        /// Move to tier
        /// </summary>
        /// <param name="manifest"></param>
        /// <param name="targetTier"></param>
        /// <returns></returns>
        /// <exception cref="InvalidOperationException"></exception>
        public async Task<string> MoveToTierAsync(Manifest manifest, StorageTier targetTier)
        {
            if (_storagePool == null) throw new InvalidOperationException("Storage Pool not initialized.");
            return await _storagePool.MoveToTierAsync(manifest, targetTier);
        }

        /// <summary>
        /// Start
        /// </summary>
        /// <param name="ct"></param>
        /// <returns></returns>
        public Task StartAsync(CancellationToken ct)
        {
            _cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            _context?.LogInfo($"[{Id}] Starting Storage Optimization Agents...");

            // Start the Background Optimization Loop
            // This loop runs periodically to:
            // 1. Scan for duplicate blobs and consolidate them.
            // 2. Check blob ages and move them to colder tiers (Tiering).
            if (_storagePool != null && _dedupeTable != null)
            {
                _optimizationTask = Task.Run(() => RunOptimizationLoopAsync(_cts.Token), _cts.Token);
            }

            return Task.CompletedTask;
        }

        /// <summary>
        /// Stop
        /// </summary>
        /// <returns></returns>
        public async Task StopAsync()
        {
            if (_cts != null)
            {
                _cts.Cancel();
                if (_optimizationTask != null)
                {
                    try { await _optimizationTask; } catch (OperationCanceledException) { }
                }
                _cts.Dispose();
            }
            _context?.LogInfo($"[{Id}] Storage Optimization Agents stopped.");
        }

        private async Task RunOptimizationLoopAsync(CancellationToken token)
        {
            while (!token.IsCancellationRequested)
            {
                try
                {
                    // Run every 1 hour (adjustable via config in a real scenario)
                    await Task.Delay(TimeSpan.FromHours(1), token);

                    _context?.LogInfo($"[{Id}] Running Deduplication Scan...");
                    // await _dedupeTable.RunScanAsync(); // Placeholder for actual service call

                    _context?.LogInfo($"[{Id}] Running Tiering Analysis...");
                    // await _storagePool.RunTieringAuditAsync(); // Placeholder for actual service call
                }
                catch (OperationCanceledException)
                {
                    break;
                }
                catch (Exception ex)
                {
                    _context?.LogError($"[{Id}] Optimization Loop Error", ex);
                }
            }
        }
    }
}