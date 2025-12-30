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

        private readonly IFederationNode _client; // gRPC Client

        private readonly string _address;
        private readonly ILogger _logger;

        // Circuit Breaker State
        private bool _isOnline = true;
        private DateTimeOffset _nextRetry = DateTimeOffset.MinValue;
        private const int RetryDelaySeconds = 30;
        private readonly Timer _heartbeat;

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

            var channel = GrpcChannel.ForAddress(address);
            _client = channel.CreateGrpcService<IFederationNode>();

            _heartbeat = new Timer(async _ => await CheckHealthAsync(), null, 10000, 10000);
        }

        private async Task CheckHealthAsync()
        {
            if (_isOnline) return;

            try
            {
                // Ping logic would go here
                // If successful:
                _isOnline = true;

                // FIX: CA2254 - Structured Logging
                _logger.LogInformation("Node {Address} is back ONLINE.", _address);
            }
            catch
            {
                // Still dead
            }
        }

        private void EnsureOnline()
        {
            if (!_isOnline)
            {
                if (DateTimeOffset.UtcNow < _nextRetry)
                    throw new IOException($"Node {_address} is OFFLINE.");

                // Retry interval passed, let one request through (Half-Open)
            }
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
                // Transparently fetch stream from remote peer
                // For efficiency, this returns a stream that buffers chunks over the network
                return await _client.OpenReadStreamAsync(uri.ToString(), 0, -1);
            }
            catch (Exception)
            {
                TripCircuit();
                throw new IOException($"Node {_address} is unreachable.");
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
                // Simulate Network Call
                // await _client.WriteStreamAsync(uri, data);
                await _client.WriteStreamAsync(uri.ToString(), data);
            }
            catch (Exception)
            {
                TripCircuit();
                throw new IOException($"Node {_address} is unreachable. Save failed.");
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
            if (!_isOnline) return false; // Fail fast -> "File doesn't exist" (or UI shows offline icon)

            try
            {
                var manifest = await _client.GetManifestAsync(uri.ToString());
                return manifest != null;
            }
            catch
            {
                TripCircuit();
                return false;
            }
        }

        /// <summary>
        /// Safely dispose
        /// </summary>
        public void Dispose()
        {
            _heartbeat.Dispose();
            GC.SuppressFinalize(this);
        }
    }
}