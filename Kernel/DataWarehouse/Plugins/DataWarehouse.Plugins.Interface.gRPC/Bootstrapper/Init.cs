using DataWarehouse.Plugins.Interface.gRPC.Engine;
using DataWarehouse.SDK.Contracts;
using DataWarehouse.SDK.Primitives;

namespace DataWarehouse.Plugins.Interface.gRPC.Bootstrapper
{
    /// <summary>
    /// AI-Native gRPC Network Interface Plugin.
    /// Provides high-performance node-to-node communication for distributed DataWarehouse clusters.
    /// </summary>
    public class GrpcNetworkInterfacePlugin : IInterfacePlugin
    {
        public string Id => "DataWarehouse.Interface.gRPC";
        public string Version => "3.0.0";
        public string Name => "gRPC Network Interface";

        // AI Metadata
        public string SemanticDescription =>
            "High-performance gRPC-based network interface for distributed DataWarehouse clusters. " +
            "Enables efficient node-to-node binary data streaming with HTTP/2, automatic retries, " +
            "exponential backoff, and connection pooling. Supports upload, download, exists, and delete operations.";

        public string[] SemanticTags => new[]
        {
            "network", "grpc", "distributed", "streaming", "http2", "node-to-node",
            "cluster", "binary-transfer", "storage-provider", "interface"
        };

        public PerformanceProfile PerformanceProfile => new()
        {
            Category = "Network I/O",
            Latency = "10-500ms (network dependent)",
            Throughput = "100MB/s+ (64KB chunks)",
            MemoryFootprint = "Low (streaming, no buffering)",
            CpuUsage = "Low",
            ScalabilityNotes = "Scales with network bandwidth and HTTP/2 connections"
        };

        // Services
        private NetworkStorageProvider? _networkProvider;
        private IKernelContext? _context;

        /// <summary>
        /// Initialize the gRPC network interface
        /// </summary>
        public void Initialize(IKernelContext context)
        {
            _context = context;
            context.LogInfo($"[{Id}] Initializing gRPC Network Interface...");

            // Initialize Network Storage Provider
            _networkProvider = new NetworkStorageProvider();
            _networkProvider.Initialize(context);

            context.LogInfo($"[{Id}] gRPC Network Interface ready. Target: {_networkProvider.Id}");
        }

        /// <summary>
        /// Get the network storage provider instance
        /// </summary>
        public NetworkStorageProvider GetNetworkProvider()
        {
            if (_networkProvider == null)
                throw new InvalidOperationException("gRPC plugin not initialized");

            return _networkProvider;
        }

        /// <summary>
        /// Start the gRPC interface
        /// </summary>
        public Task StartAsync(CancellationToken ct)
        {
            _context?.LogInfo($"[{Id}] gRPC Network Interface started.");
            return Task.CompletedTask;
        }

        /// <summary>
        /// Stop the gRPC interface
        /// </summary>
        public Task StopAsync()
        {
            _networkProvider?.Dispose();
            _context?.LogInfo($"[{Id}] gRPC Network Interface stopped.");
            return Task.CompletedTask;
        }
    }
}
