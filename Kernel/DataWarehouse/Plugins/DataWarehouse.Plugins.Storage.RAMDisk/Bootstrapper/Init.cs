using DataWarehouse.Plugins.Storage.RAMDisk.Engine;
using DataWarehouse.SDK.Contracts;

namespace DataWarehouse.Plugins.Storage.RAMDisk.Bootstrapper
{
    /// <summary>
    /// AI-Native RAMDisk Storage Plugin.
    /// Ultra-high-performance in-memory storage with optional persistence.
    /// </summary>
    [PluginInfo(
        name: "RAMDisk Storage",
        description: "Ultra-high-performance in-memory storage with optional persistence and automatic LRU eviction",
        author: "DataWarehouse Team",
        version: "1.0.0",
        category: PluginCategory.Storage
    )]
    public class RAMDiskStoragePlugin : IStorageProvider
    {
        public static string Id => "DataWarehouse.Storage.RAMDisk";
        public static string Version => "1.0.0";
        public static string Name => "RAMDisk Storage";

        private RAMDiskStorageEngine? _engine;
        private IKernelContext? _context;

        /// <summary>
        /// Handshake implementation for IPlugin.
        /// </summary>
        public async Task<HandshakeResponse> OnHandshakeAsync(HandshakeRequest request)
        {
            var startTime = DateTime.UtcNow;
            _context = request as IKernelContext;

            _context?.LogInfo($"[{Id}] Initializing RAMDisk Storage Plugin...");

            _engine = new RAMDiskStorageEngine();
            _engine.Initialize(_context!);

            var capabilities = new List<PluginCapabilityDescriptor>
            {
                new PluginCapabilityDescriptor
                {
                    CapabilityId = "storage.ramdisk.ultrahigh",
                    DisplayName = "Ultra-High Performance In-Memory Storage",
                    Description = "Sub-microsecond latency in-memory storage with thread-safe concurrent access. " +
                                 "Ideal for high-frequency trading, real-time analytics caching, and performance-critical workloads.",
                    Category = CapabilityCategory.Storage,
                    Tags = ["storage", "in-memory", "ramdisk", "high-performance", "low-latency"]
                },
                new PluginCapabilityDescriptor
                {
                    CapabilityId = "storage.ramdisk.lru",
                    DisplayName = "Automatic LRU Eviction",
                    Description = "Configurable memory limits with automatic least-recently-used eviction when limits are reached.",
                    Category = CapabilityCategory.Storage,
                    Tags = ["lru-eviction", "memory-limit", "cache"]
                },
                new PluginCapabilityDescriptor
                {
                    CapabilityId = "storage.ramdisk.persistence",
                    DisplayName = "Optional Persistence",
                    Description = "Automatic snapshot persistence to disk with configurable auto-save intervals to survive restarts.",
                    Category = CapabilityCategory.Storage,
                    Tags = ["persistence", "auto-save", "snapshot"]
                }
            };

            _context?.LogInfo($"[{Id}] RAMDisk Storage Plugin ready");

            return HandshakeResponse.Success(
                pluginId: Id,
                name: Name,
                version: new Version(Version),
                category: PluginCategory.Storage,
                capabilities: capabilities,
                initDuration: DateTime.UtcNow - startTime
            );
        }

        /// <summary>
        /// Message handler (optional).
        /// </summary>
        public Task OnMessageAsync(PluginMessage message)
        {
            return Task.CompletedTask;
        }

        public string Scheme => "ramdisk";

        /// <summary>
        /// Initialize the RAMDisk plugin.
        /// </summary>
        public void Initialize(IKernelContext context)
        {
            _context = context ?? throw new ArgumentNullException(nameof(context));
            _context.LogInfo($"[{Id}] Initializing RAMDisk Storage Plugin...");

            _engine = new RAMDiskStorageEngine();
            _engine.Initialize(context);

            _context.LogInfo($"[{Id}] RAMDisk Storage Plugin ready");
        }

        /// <summary>
        /// Get the RAMDisk storage engine.
        /// </summary>
        public RAMDiskStorageEngine GetEngine()
        {
            if (_engine == null)
                throw new InvalidOperationException("RAMDisk plugin not initialized");

            return _engine;
        }

        /// <summary>
        /// Start the plugin.
        /// </summary>
        public Task StartAsync(CancellationToken ct)
        {
            _context?.LogInfo($"[{Id}] RAMDisk Storage started");
            return Task.CompletedTask;
        }

        /// <summary>
        /// Stop the plugin.
        /// </summary>
        public Task StopAsync()
        {
            _engine?.Dispose();
            _context?.LogInfo($"[{Id}] RAMDisk Storage stopped");
            return Task.CompletedTask;
        }
    }
}
