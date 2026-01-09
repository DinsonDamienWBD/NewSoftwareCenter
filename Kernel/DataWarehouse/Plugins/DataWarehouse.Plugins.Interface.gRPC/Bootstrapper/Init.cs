using DataWarehouse.Plugins.Interface.gRPC.Engine;
using DataWarehouse.SDK.Contracts;
using DataWarehouse.SDK.Primitives;
using System.Linq;

namespace DataWarehouse.Plugins.Interface.gRPC.Bootstrapper
{
    /// <summary>
    /// AI-Native gRPC Network Interface Plugin.
    /// Provides high-performance node-to-node communication for distributed DataWarehouse clusters.
    /// </summary>
    public class GrpcNetworkInterfacePlugin : IInterfacePlugin
    {
        // Services
        private NetworkStorageProvider? _networkProvider;
        private IKernelContext? _context;
        private string _pluginId = "DataWarehouse.Interface.gRPC";

        /// <summary>
        /// Handshake protocol handler
        /// </summary>
        public async Task<HandshakeResponse> OnHandshakeAsync(HandshakeRequest request)
        {
            _context = new KernelContextFromRequest(request);
            _context.LogInfo($"[{_pluginId}] Initializing gRPC Network Interface...");

            try
            {
                // Initialize Network Storage Provider
                _networkProvider = new NetworkStorageProvider();
                var netRequest = new HandshakeRequest
                {
                    KernelId = request.KernelId,
                    ProtocolVersion = request.ProtocolVersion,
                    Mode = request.Mode,
                    RootPath = request.RootPath,
                    AlreadyLoadedPlugins = request.AlreadyLoadedPlugins
                };

                var netResponse = await _networkProvider.OnHandshakeAsync(netRequest);
                if (!netResponse.Success)
                {
                    return HandshakeResponse.Failure(
                        _pluginId,
                        "gRPC Network Interface",
                        $"Network provider initialization failed: {netResponse.ErrorMessage}");
                }

                _context.LogInfo($"[{_pluginId}] gRPC Network Interface ready. Target: {netResponse.PluginId}");

                return HandshakeResponse.Success(
                    pluginId: _pluginId,
                    name: "gRPC Network Interface",
                    version: new Version(3, 0, 0),
                    category: PluginCategory.Interface);
            }
            catch (Exception ex)
            {
                return HandshakeResponse.Failure(_pluginId, "gRPC Network Interface", ex.Message);
            }
        }

        // Helper class to wrap HandshakeRequest as IKernelContext
        private class KernelContextFromRequest : IKernelContext
        {
            private readonly HandshakeRequest _request;
            public KernelContextFromRequest(HandshakeRequest request) => _request = request;
            public OperatingMode Mode => _request.Mode;
            public string RootPath => _request.RootPath;
            public void LogInfo(string message) => Console.WriteLine($"[INFO] {message}");
            public void LogError(string message, Exception? ex = null) => Console.WriteLine($"[ERROR] {message} {ex?.Message}");
            public void LogWarning(string message) => Console.WriteLine($"[WARN] {message}");
            public void LogDebug(string message) => Console.WriteLine($"[DEBUG] {message}");
            public T? GetPlugin<T>() where T : class, IPlugin => null;
            public System.Collections.Generic.IEnumerable<T> GetPlugins<T>() where T : class, IPlugin => Enumerable.Empty<T>();
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
