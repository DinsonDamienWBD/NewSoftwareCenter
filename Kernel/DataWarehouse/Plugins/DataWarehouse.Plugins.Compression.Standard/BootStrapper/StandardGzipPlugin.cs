using DataWarehouse.SDK.Contracts;
using DataWarehouse.SDK.IO;
using System.IO.Compression;

namespace DataWarehouse.Plugins.Compression.Standard.Bootstrapper
{
    /// <summary>
    /// Standard GZip compression plugin.
    /// Migrated to handshake protocol for message-based initialization.
    /// </summary>
    public class StandardGzipPlugin : IFeaturePlugin, IDataTransformation
    {
        private IKernelContext? _context;
        private bool _isReady;

        /// <summary>
        /// ID (DEPRECATED - use HandshakeResponse)
        /// </summary>
        public string Id => "DataWarehouse.Compression.GZip";

        /// <summary>
        /// Name (DEPRECATED - use HandshakeResponse)
        /// </summary>
        public string Name => "GZip Compression";

        /// <summary>
        /// Version (DEPRECATED - use HandshakeResponse)
        /// </summary>
        public string Version => "1.0.0";

        /// <summary>
        /// Compression level
        /// </summary>
        public SDK.Contracts.CompressionLevel Level => SDK.Contracts.CompressionLevel.Fast;

        public string Category => "Compression";
        public int QualityLevel => 80; // High compression

        /// <summary>
        /// Initialize (DEPRECATED - backward compatibility only)
        /// </summary>
        /// <param name="context"></param>
        public void Initialize(IKernelContext context)
        {
            _context = context;
            _isReady = true;
        }

        /// <summary>
        /// Handshake protocol handler - PRIMARY initialization method.
        /// </summary>
        public async Task<HandshakeResponse> OnHandshakeAsync(HandshakeRequest request)
        {
            var startTime = DateTime.UtcNow;

            try
            {
                // Self-initialize
                _context = CreateKernelContext(request);
                _isReady = true;

                // Build capability descriptors
                var capabilities = new List<PluginCapabilityDescriptor>
                {
                    new()
                    {
                        CapabilityId = "transform.compress.gzip",
                        DisplayName = "GZip Compression",
                        Description = "Compress data streams using the GZip algorithm (RFC 1952). Optimized for speed with good compression ratio.",
                        Category = CapabilityCategory.Transform,
                        RequiredPermission = Permission.Read,
                        RequiresApproval = false,
                        ParameterSchemaJson = """
                        {
                            "type": "object",
                            "properties": {
                                "stream": {
                                    "type": "object",
                                    "description": "Input stream to compress"
                                },
                                "level": {
                                    "type": "string",
                                    "enum": ["fastest", "optimal", "nocompression"],
                                    "default": "fastest",
                                    "description": "Compression level"
                                }
                            },
                            "required": ["stream"]
                        }
                        """,
                        Tags = new List<string> { "compression", "gzip", "rfc1952", "fast" }
                    },
                    new()
                    {
                        CapabilityId = "transform.decompress.gzip",
                        DisplayName = "GZip Decompression",
                        Description = "Decompress GZip-compressed data streams",
                        Category = CapabilityCategory.Transform,
                        RequiredPermission = Permission.Read,
                        RequiresApproval = false,
                        ParameterSchemaJson = """
                        {
                            "type": "object",
                            "properties": {
                                "stream": {
                                    "type": "object",
                                    "description": "Compressed stream to decompress"
                                }
                            },
                            "required": ["stream"]
                        }
                        """,
                        Tags = new List<string> { "decompression", "gzip" }
                    }
                };

                var initDuration = DateTime.UtcNow - startTime;

                // Return successful handshake response
                return HandshakeResponse.Success(
                    pluginId: "DataWarehouse.Compression.GZip",
                    name: "GZip Compression",
                    version: new Version(1, 0, 0),
                    category: PluginCategory.Pipeline,
                    capabilities: capabilities,
                    initDuration: initDuration
                );
            }
            catch (Exception ex)
            {
                return HandshakeResponse.Failure(
                    pluginId: "DataWarehouse.Compression.GZip",
                    name: "GZip Compression",
                    errorMessage: $"Initialization failed: {ex.Message}"
                );
            }
        }

        /// <summary>
        /// Message handler for runtime communication.
        /// </summary>
        public Task OnMessageAsync(PluginMessage message)
        {
            // Handle health checks, configuration updates, etc.
            if (message.MessageType == "HealthCheck")
            {
                // Could return health status
                _context?.LogDebug("GZip plugin health check: OK");
            }

            return Task.CompletedTask;
        }

        /// <summary>
        /// Helper to create kernel context from handshake request.
        /// </summary>
        private IKernelContext CreateKernelContext(HandshakeRequest request)
        {
            // In a real implementation, this would create a proper context
            // For now, return a minimal implementation
            return new SimpleKernelContext(request);
        }

        /// <summary>
        /// Simple kernel context for handshake-based initialization.
        /// </summary>
        private class SimpleKernelContext : IKernelContext
        {
            private readonly HandshakeRequest _request;

            public SimpleKernelContext(HandshakeRequest request)
            {
                _request = request;
            }

            public OperatingMode Mode => _request.Mode;
            public string RootPath => _request.RootPath;

            public void LogInfo(string message) { }
            public void LogError(string message, Exception? ex = null) { }
            public void LogWarning(string message) { }
            public void LogDebug(string message) { }

            public T? GetPlugin<T>() where T : class, IPlugin => null;
            public IEnumerable<T> GetPlugins<T>() where T : class, IPlugin => Enumerable.Empty<T>();
        }

        public Task StartAsync(CancellationToken c) => Task.CompletedTask;
        public Task StopAsync() => Task.CompletedTask;

        /// <summary>
        /// Create compression stream
        /// </summary>
        /// <param name="output"></param>
        /// <returns></returns>
        public Stream CreateCompressionStream(Stream output)
        {
            return new GZipStream(output, System.IO.Compression.CompressionLevel.Optimal, leaveOpen: true);
        }

        /// <summary>
        /// Create decompression stream
        /// </summary>
        /// <param name="input"></param>
        /// <returns></returns>
        public Stream CreateDecompressionStream(Stream input)
        {
            return new GZipStream(input, CompressionMode.Decompress, leaveOpen: true);
        }

        public Stream OnWrite(Stream input, IKernelContext context, Dictionary<string, object> args)
        {
            // On Write, we compress. GZip is Push, so we use Adapter.
            return new PushToPullStreamAdapter(input, (dest) =>
                new GZipStream(dest, System.IO.Compression.CompressionLevel.Fastest, leaveOpen: true));
        }

        public Stream OnRead(Stream stored, IKernelContext context, Dictionary<string, object> args)
        {
            // On Read, we decompress. GZipStream supports Pull (Read) natively.
            return new GZipStream(stored, CompressionMode.Decompress, leaveOpen: true);
        }
    }
}