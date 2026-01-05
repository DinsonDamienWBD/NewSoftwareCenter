using DataWarehouse.Plugins.Features.EnterpriseStorage.Protos;
using DataWarehouse.SDK.Contracts;
using Grpc.Net.Client;

namespace DataWarehouse.Plugins.Features.EnterpriseStorage.Engine
{
    /// <summary>
    /// A Production-Ready Network Storage Provider.
    /// Uses gRPC Streaming to transfer binary blobs efficiently between Data Warehouse nodes.
    /// </summary>
    public class NetworkStorageProvider : IStorageProvider, IDisposable
    {
        public string Id { get; private set; }
        public string Version => "5.0.0";
        public string Name => "gRPC Network Storage";
        public string Scheme => "grpc";

        private  string _address;
        private  GrpcChannel _channel;
        private StorageTransport.StorageTransportClient _client;
        private IKernelContext _context;

        /// <summary>
        /// [FIX] Default Constructor.
        /// Allows instantiation by the Bootstrapper or PluginRegistry.
        /// </summary>
        public NetworkStorageProvider()
        {
            Id = "network-storage-default";
            _address = "https://localhost:5000"; // Default, overridden in Initialize
        }

        /// <summary>
        /// Initializes the provider for a specific target node.
        /// </summary>
        public NetworkStorageProvider(string nodeId, string address, IKernelContext context)
        {
            Id = $"net-{nodeId}";
            _address = address;
            _context = context;
            InitializeChannel();
        }

        /// <summary>
        /// Initialize
        /// </summary>
        /// <param name="context"></param>
        public void Initialize(IKernelContext context) 
        {
            _context = context;
            if (_channel == null)
            {
                // Real Logic: Configuration or Env Var
                _address = Environment.GetEnvironmentVariable("DW_NETWORK_TARGET") ?? "https://localhost:5000";
                Id = $"net-{Uri.EscapeDataString(_address)}";
                InitializeChannel();
            }
        }

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

        private void EnsureOnline()
        {
            if (_client == null || _channel == null)
                throw new InvalidOperationException($"Network Provider {Id} is not initialized or connected to {_address}.");
        }

        /// <summary>
        /// Save
        /// </summary>
        /// <param name="uri"></param>
        /// <param name="data"></param>
        /// <returns></returns>
        /// <exception cref="IOException"></exception>
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

                    _context.LogInfo($"[Network] Uploading {data.Length} bytes to {_address}...");

                    // gRPC Client Streaming
                    using var call = _client.UploadBlob();

                    // 1. Send Header
                    await call.RequestStream.WriteAsync(new UploadRequest
                    {
                        Metadata = new BlobMetadata { Uri = uri.ToString(), TotalSize = data.Length }
                    });

                    // 2. Stream Data Chunks
                    byte[] buffer = new byte[64 * 1024]; // 64KB chunks
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

                    _context.LogInfo($"[Network] Upload Complete.");
                }
                catch (Exception ex)
                {
                    attempt++;
                    if (attempt >= MaxRetries)
                    {
                        _context?.LogError($"[Network] Critical Failure uploading to {_address} after {MaxRetries} attempts.", ex);
                        throw; // Give up
                    }

                    // Exponential Backoff with Jitter: 200ms, 400ms, 800ms...
                    int delay = (int)(Math.Pow(2, attempt) * 100) + Random.Shared.Next(0, 50);
                    _context?.LogWarning($"[Network] Upload failed. Retrying in {delay}ms... Error: {ex.Message}");
                    await Task.Delay(delay);
                }
            }
        }

        /// <summary>
        /// Load
        /// </summary>
        /// <param name="uri"></param>
        /// <returns></returns>
        public async Task<Stream> LoadAsync(Uri uri)
        {
            _context.LogInfo($"[Network] Downloading {uri} from {_address}...");

            EnsureOnline();

            var call = _client!.DownloadBlob(new DownloadRequest { Uri = uri.ToString() });

            // We return a wrapper stream that reads from the gRPC response stream
            // This enables true end-to-end streaming without buffering the whole file in RAM
            return new GrpcStreamAdapter(call);
        }

        /// <summary>
        /// Delete
        /// </summary>
        /// <param name="uri"></param>
        /// <returns></returns>
        public async Task DeleteAsync(Uri uri)
        {
            EnsureOnline();
            await _client!.DeleteBlobAsync(new DeleteRequest { Uri = uri.ToString() });
        }

        /// <summary>
        /// Exist
        /// </summary>
        /// <param name="uri"></param>
        /// <returns></returns>
        public async Task<bool> ExistsAsync(Uri uri)
        {
            EnsureOnline();
            try
            {
                var resp = await _client!.ExistsBlobAsync(new ExistsRequest { Uri = uri.ToString() });
                return resp.Exists;
            }
            catch { return false; }
        }

        /// <summary>
        /// Start
        /// </summary>
        /// <param name="ct"></param>
        /// <returns></returns>
        public Task StartAsync(System.Threading.CancellationToken ct) => Task.CompletedTask;

        /// <summary>
        /// Stop
        /// </summary>
        /// <returns></returns>
        public Task StopAsync() => Task.CompletedTask;

        /// <summary>
        /// Safely dispose
        /// </summary>
        public void Dispose()
        {
            _channel.Dispose();
            GC.SuppressFinalize(this);
        }
    }
}