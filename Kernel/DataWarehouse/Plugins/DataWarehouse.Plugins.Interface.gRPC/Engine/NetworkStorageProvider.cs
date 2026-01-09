using DataWarehouse.Plugins.Interface.gRPC.Protos;
using DataWarehouse.SDK.Contracts;
using Grpc.Net.Client;

namespace DataWarehouse.Plugins.Interface.gRPC.Engine
{
    /// <summary>
    /// Production-Ready Network Storage Provider using gRPC Streaming.
    /// Efficiently transfers binary blobs between DataWarehouse nodes over HTTP/2.
    /// </summary>
    public class NetworkStorageProvider : IStorageProvider, IDisposable
    {
        public string Id { get; private set; }
        public string Version => "3.0.0";
        public string Name => "gRPC Network Storage";
        public string Scheme => "grpc";

        private string _address;
        private GrpcChannel? _channel;
        private StorageTransport.StorageTransportClient? _client;
        private IKernelContext? _context;

        /// <summary>
        /// Default Constructor for plugin instantiation.
        /// </summary>
        public NetworkStorageProvider()
        {
            Id = "network-storage-default";
            _address = "https://localhost:5000"; // Default, overridden in Initialize
        }

        /// <summary>
        /// Constructor for specific target node.
        /// </summary>
        public NetworkStorageProvider(string nodeId, string address, IKernelContext context)
        {
            Id = $"net-{nodeId}";
            _address = address;
            _context = context;
            InitializeChannel();
        }

        /// <summary>
        /// Handshake implementation for IPlugin.
        /// </summary>
        public Task<HandshakeResponse> OnHandshakeAsync(HandshakeRequest request)
        {
            _context = request as IKernelContext;

            if (_channel == null)
            {
                // Get target address from environment or config
                _address = Environment.GetEnvironmentVariable("DW_NETWORK_TARGET") ?? "https://localhost:5000";
                Id = $"net-{Uri.EscapeDataString(_address)}";
                InitializeChannel();
            }

            return Task.FromResult(HandshakeResponse.Success(
                pluginId: Id,
                name: Name,
                version: new Version(Version),
                category: PluginCategory.Storage
            ));
        }

        /// <summary>
        /// Message handler (optional).
        /// </summary>
        public Task OnMessageAsync(PluginMessage message)
        {
            return Task.CompletedTask;
        }

        /// <summary>
        /// Initialize the provider with kernel context.
        /// </summary>
        public void Initialize(IKernelContext context)
        {
            _context = context;
            if (_channel == null)
            {
                // Get target address from environment or config
                _address = Environment.GetEnvironmentVariable("DW_NETWORK_TARGET") ?? "https://localhost:5000";
                Id = $"net-{Uri.EscapeDataString(_address)}";
                InitializeChannel();
            }
        }

        /// <summary>
        /// Initialize gRPC channel with HTTP/2 optimizations.
        /// </summary>
        private void InitializeChannel()
        {
            try
            {
                var httpHandler = new SocketsHttpHandler
                {
                    EnableMultipleHttp2Connections = true,
                    KeepAlivePingDelay = TimeSpan.FromSeconds(60),
                    KeepAlivePingTimeout = TimeSpan.FromSeconds(30)
                };

                _channel = GrpcChannel.ForAddress(_address, new GrpcChannelOptions
                {
                    HttpHandler = httpHandler,
                    MaxReceiveMessageSize = null,
                    MaxSendMessageSize = null
                });

                _client = new StorageTransport.StorageTransportClient(_channel);
            }
            catch (Exception ex)
            {
                _context?.LogError($"[{Id}] Channel Init Failed", ex);
            }
        }

        /// <summary>
        /// Ensure the provider is online.
        /// </summary>
        private void EnsureOnline()
        {
            if (_client == null || _channel == null)
                throw new InvalidOperationException($"Network Provider {Id} is not initialized or connected to {_address}.");
        }

        /// <summary>
        /// Save data to remote node via gRPC streaming.
        /// </summary>
        public async Task SaveAsync(Uri uri, Stream data)
        {
            EnsureOnline();

            const int MaxRetries = 5;
            int attempt = 0;

            while (true)
            {
                try
                {
                    // Reset stream position for retry
                    if (data.CanSeek) data.Position = 0;

                    _context?.LogInfo($"[Network] Uploading {data.Length} bytes to {_address}...");

                    // gRPC Client Streaming
                    using var call = _client?.UploadBlob();

                    // 1. Send Header
                    await call.RequestStream.WriteAsync(new UploadRequest
                    {
                        Metadata = new BlobMetadata { Uri = uri?.ToString(), TotalSize = data.Length }
                    });

                    // 2. Stream Data Chunks (64KB)
                    byte[] buffer = new byte[64 * 1024];
                    int read;
                    while ((read = await data.ReadAsync(buffer)) > 0)
                    {
                        await call.RequestStream.WriteAsync(new UploadRequest
                        {
                            Chunk = Google.Protobuf.ByteString.CopyFrom(buffer, 0, read)
                        });
                    }

                    // 3. Complete
                    await call.RequestStream.CompleteAsync();
                    var response = await call.ResponseAsync;

                    if (!response.Success)
                    {
                        throw new IOException($"Remote write failed: {response.Message}");
                    }

                    _context?.LogInfo($"[Network] Upload Complete.");
                    return;
                }
                catch (Exception ex)
                {
                    attempt++;
                    if (attempt >= MaxRetries)
                    {
                        _context?.LogError($"[Network] Critical Failure uploading to {_address} after {MaxRetries} attempts.", ex);
                        throw;
                    }

                    // Exponential Backoff with Jitter: 200ms, 400ms, 800ms...
                    int delay = (int)(MathUtils.Pow(2, attempt) * 100) + Random.Shared.Next(0, 50);
                    _context?.LogWarning($"[Network] Upload failed. Retrying in {delay}ms... Error: {ex.Message}");
                    await Task.Delay(delay);
                }
            }
        }

        /// <summary>
        /// Load data from remote node via gRPC streaming.
        /// </summary>
        public async Task<Stream> LoadAsync(Uri uri)
        {
            _context?.LogInfo($"[Network] Downloading {uri} from {_address}...");

            EnsureOnline();

            var call = _client!.DownloadBlob(new DownloadRequest { Uri = uri?.ToString() });

            // Return wrapper stream for end-to-end streaming (no buffering)
            return new GrpcStreamAdapter(call);
        }

        /// <summary>
        /// Delete blob from remote node.
        /// </summary>
        public async Task DeleteAsync(Uri uri)
        {
            EnsureOnline();
            await _client!.DeleteBlobAsync(new DeleteRequest { Uri = uri?.ToString() });
        }

        /// <summary>
        /// Check if blob exists on remote node.
        /// </summary>
        public async Task<bool> ExistsAsync(Uri uri)
        {
            EnsureOnline();
            try
            {
                var resp = await _client!.ExistsBlobAsync(new ExistsRequest { Uri = uri?.ToString() });
                return resp.Exists;
            }
            catch { return false; }
        }

        /// <summary>
        /// Start async (no-op for this provider).
        /// </summary>
        public static Task StartAsync(System.Threading.CancellationToken ct) => Task.CompletedTask;

        /// <summary>
        /// Stop async (no-op for this provider).
        /// </summary>
        public static Task StopAsync() => Task.CompletedTask;

        /// <summary>
        /// Dispose gRPC channel.
        /// </summary>
        public void Dispose()
        {
            _channel?.Dispose();
            GC.SuppressFinalize(this);
        }
    }
}
