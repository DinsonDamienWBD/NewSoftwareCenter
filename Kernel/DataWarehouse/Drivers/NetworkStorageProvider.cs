using DataWarehouse.Contracts;
using Microsoft.Extensions.Logging;
using Grpc.Net.Client;
using ProtoBuf.Grpc.Client;

namespace DataWarehouse.Drivers
{
    /// <summary>
    /// This is the driver you "Plug" into your local DW to see the Server. It acts like a local disk driver, but forwards all calls over the network.
    /// </summary>
    public class NetworkStorageProvider : IStorageProvider, IDisposable
    {
        /// <summary>
        /// ID
        /// </summary>
        public string Id => "GrpcClient";

        /// <summary>
        /// Version
        /// </summary>
        public string Version => "1.0";

        /// <summary>
        /// Scheme
        /// </summary>
        public string Scheme => "net"; // URI: net://server-ip/bucket/key

        private readonly string _address;
        private readonly ILogger _logger;
        private readonly GrpcChannel _channel;
        private readonly IFederationNode _client;

        private readonly Timer _heartbeat;
        private volatile bool _isOnline = true;
        private DateTimeOffset _nextRetry = DateTimeOffset.MinValue;
        private const int RetryDelaySeconds = 30;

        /// <summary>
        /// Check if DW is online
        /// </summary>
        public bool IsOnline => _isOnline;

        /// <summary>
        /// Constructor
        /// </summary>
        /// <param name="address"></param>
        /// <param name="logger"></param>
        public NetworkStorageProvider(string address, ILogger logger)
        {

            _address = address;
            _logger = logger;

            // 1. Configure SocketsHttpHandler for KeepAlive and Performance
            var httpHandler = new SocketsHttpHandler
            {
                PooledConnectionIdleTimeout = TimeSpan.FromMinutes(1),
                KeepAlivePingDelay = TimeSpan.FromSeconds(60),
                KeepAlivePingTimeout = TimeSpan.FromSeconds(30),
                EnableMultipleHttp2Connections = true
            };

            // 2. Create Channel
            var options = new GrpcChannelOptions { HttpHandler = httpHandler };
            _channel = GrpcChannel.ForAddress(address, options);

            // 3. Create Client Proxy (Code-First)
            _client = _channel.CreateGrpcService<IFederationNode>();

            // 4. Start Health Check
            _heartbeat = new Timer(async _ => await CheckHealthAsync(), null, 10000, 10000);
        }

        private async Task CheckHealthAsync()
        {
            if (_isOnline) return; // Only actively check if we think it's offline
            if (DateTimeOffset.UtcNow < _nextRetry) return;

            try
            {
                // Simple handshake to verify connectivity
                await _client.HandshakeAsync("PROBE");
                _isOnline = true;
                _logger.LogInformation($"[NetworkStorage] Node {_address} restored.");
            }
            catch
            {
                _nextRetry = DateTimeOffset.UtcNow.AddSeconds(RetryDelaySeconds);
            }
        }

        private void EnsureOnline()
        {
            if (!_isOnline) throw new IOException($"Node {_address} is OFFLINE. Next retry at {_nextRetry}");
        }

        private void TripCircuit()
        {
            if (_isOnline)
            {
                _isOnline = false;
                _nextRetry = DateTimeOffset.UtcNow.AddSeconds(RetryDelaySeconds);
                _logger.LogWarning("Node {Address} went OFFLINE. Circuit broken.", _address);
            }
        }

        /// <summary>
        /// Load data
        /// </summary>
        /// <param name="uri"></param>
        /// <returns></returns>
        public async Task<Stream> LoadAsync(Uri uri)
        {
            EnsureOnline();
            try
            {
                // Returns a stream that reads from the network
                return await _client.OpenReadStreamAsync(uri.ToString(), 0, -1);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"[NetworkStorage] Read failed from {_address}");
                _isOnline = false;
                throw;
            }
        }


        /// <summary>
        /// Save data
        /// </summary>
        /// <param name="uri"></param>
        /// <param name="data"></param>
        /// <returns></returns>
        public async Task SaveAsync(Uri uri, Stream data)
        {
            EnsureOnline();
            try
            {
                // gRPC Streaming Call
                // protobuf-net.Grpc handles Stream serialization efficiently
                await _client.WriteStreamAsync(uri.ToString(), data);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"[NetworkStorage] Write failed to {_address}");
                _isOnline = false;
                throw;
            }
        }

        /// <summary>
        /// Delete data
        /// </summary>
        /// <param name="uri"></param>
        /// <returns></returns>
        /// <exception cref="UnauthorizedAccessException"></exception>
        public Task DeleteAsync(Uri uri) => throw new UnauthorizedAccessException("Remote delete disabled by default.");


        /// <summary>
        /// Check if data exists
        /// </summary>
        /// <param name="uri"></param>
        /// <returns></returns>
        public async Task<bool> ExistsAsync(Uri uri)
        {
            if (!_isOnline) return false;
            try
            {
                var manifest = await _client.GetManifestAsync(uri.ToString());
                return manifest != null;
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// Safely dispose
        /// </summary>
        public void Dispose()
        {
            _heartbeat.Dispose();
            _channel.Dispose();
            GC.SuppressFinalize(this);
        }
    }
}