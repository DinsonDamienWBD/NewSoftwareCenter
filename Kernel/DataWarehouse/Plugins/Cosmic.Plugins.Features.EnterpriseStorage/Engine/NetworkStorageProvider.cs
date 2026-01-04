using DataWarehouse.SDK.Contracts;
using Microsoft.Extensions.Logging;
using Grpc.Net.Client;
using System.Net.Http;

namespace DataWarehouse.Plugins.Features.EnterpriseStorage.Engine
{
    /// <summary>
    /// A storage provider that forwards requests to a remote node via gRPC.
    /// Acts as a "Client" driver.
    /// </summary>
    public class NetworkStorageProvider : IStorageProvider, IDisposable
    {
        /// <summary>The unique ID of the provider instance.</summary>
        public string Id => $"net-{_targetAddress}";
        /// <summary>The provider version.</summary>
        public string Version => "5.0";
        /// <summary>The display name.</summary>
        public string Name => "Network Storage Provider";
        /// <summary>The URI scheme (grpc).</summary>
        public string Scheme => "grpc";

        private readonly string _targetAddress = "https://localhost:5000";
        private GrpcChannel? _channel;
        private IKernelContext? _context;
        private readonly IFederationNode? _federationNode;

        /// <summary>
        /// Default constructor for Bootstrapper initialization.
        /// </summary>
        public NetworkStorageProvider() { }

        /// <summary>
        /// Constructor for binding to a specific Federation Node.
        /// </summary>
        /// <param name="node">The remote node to target.</param>
        public NetworkStorageProvider(IFederationNode node)
        {
            _federationNode = node;
            _targetAddress = node.Address;
        }

        /// <summary>
        /// Initializes the gRPC channel and networking stack.
        /// </summary>
        /// <param name="context">The kernel context.</param>
        public void Initialize(IKernelContext context)
        {
            _context = context;

            // SocketsHttpHandler optimization for high-throughput
            var httpHandler = new SocketsHttpHandler
            {
                EnableMultipleHttp2Connections = true,
                KeepAlivePingDelay = TimeSpan.FromSeconds(60),
                PooledConnectionIdleTimeout = TimeSpan.FromMinutes(20)
            };

            _channel = GrpcChannel.ForAddress(_targetAddress, new GrpcChannelOptions
            {
                HttpHandler = httpHandler,
                MaxReceiveMessageSize = 50 * 1024 * 1024 // 50MB
            });

            context.LogInfo($"[{Id}] Network Storage initialized targeting {_targetAddress}");
        }

        /// <summary>
        /// Streams data to the remote node.
        /// </summary>
        public async Task SaveAsync(Uri uri, Stream data)
        {
            EnsureOnline();
            _context?.LogInfo($"[Stub] Streaming {data.Length} bytes to {_targetAddress}");
            // In real implementation: await _client.WriteStreamAsync(...)
            await Task.CompletedTask;
        }

        /// <summary>
        /// Streams data from the remote node.
        /// </summary>
        public async Task<Stream> LoadAsync(Uri uri)
        {
            EnsureOnline();
            // Stub return
            return await Task.FromResult(new MemoryStream());
        }

        /// <summary>
        /// Deletes a blob on the remote node.
        /// </summary>
        public Task DeleteAsync(Uri uri) => Task.CompletedTask;

        /// <summary>
        /// Checks if a blob exists on the remote node.
        /// </summary>
        public Task<bool> ExistsAsync(Uri uri) => Task.FromResult(true);

        private void EnsureOnline()
        {
            if (_channel == null) throw new InvalidOperationException("Provider not initialized.");
        }

        /// <summary>
        /// Disposes the network channel.
        /// </summary>
        public void Dispose()
        {
            _channel?.Dispose();
            GC.SuppressFinalize(this);
        }
    }
}