using DataWarehouse.SDK.Attributes;
using DataWarehouse.SDK.Contracts;

namespace DataWarehouse.Plugins.Storage.Ipfs.Engine
{
    /// <summary>
    /// COSMIC TIER: IPFS Storage Provider.
    /// Writes data to the decentralized web.
    /// </summary>
    [PluginPriority(10, OperatingMode.Hyperscale)]
    public class IpfsStoragePlugin : IFeaturePlugin, IStorageProvider
    {
        public string Id => "ipfs-storage";
        public string Name => "IPFS Provider";
        public string Version => "6.0.0";
        public string Scheme => "ipfs";

        private readonly HttpClient _client = new();
        private IKernelContext? _context;
        // Default IPFS API port
        private string _gatewayUrl = "http://127.0.0.1:5001/api/v0";

        /// <summary>
        /// Handshake protocol handler
        /// </summary>
        public Task<HandshakeResponse> OnHandshakeAsync(HandshakeRequest request)
        {
            _context = request as IKernelContext;
            var envUrl = Environment.GetEnvironmentVariable("IPFS_GATEWAY");
            if (!string.IsNullOrEmpty(envUrl)) _gatewayUrl = envUrl;

            _context?.LogInfo($"[IPFS] Linked to node {_gatewayUrl}");

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

        public void Initialize(IKernelContext context)
        {
            _context = context;
            // Configurable gateway
            var envUrl = Environment.GetEnvironmentVariable("IPFS_GATEWAY");
            if (!string.IsNullOrEmpty(envUrl)) _gatewayUrl = envUrl;

            context.LogInfo($"[IPFS] Linked to node {_gatewayUrl}");
        }

        public Task StartAsync(CancellationToken ct) => Task.CompletedTask;
        public Task StopAsync() => Task.CompletedTask;

        public async Task SaveAsync(Uri uri, Stream data)
        {
            // IPFS add
            using var content = new MultipartFormDataContent();
            content.Add(new StreamContent(data), "file", Path.GetFileName(uri.AbsolutePath));

            var response = await _client.PostAsync($"{_gatewayUrl}/add", content);
            response.EnsureSuccessStatusCode();

            var json = await response.Content.ReadAsStringAsync();
            _context?.LogInfo($"[IPFS] Pinned. Node Response: {json}");
        }

        public async Task<Stream> LoadAsync(Uri uri)
        {
            // ipfs://<CID>
            string cid = uri.Host;
            var response = await _client.GetAsync($"{_gatewayUrl}/cat?arg={cid}", HttpCompletionOption.ResponseHeadersRead);
            response.EnsureSuccessStatusCode();
            return await response.Content.ReadAsStreamAsync();
        }

        public Task DeleteAsync(Uri uri)
        {
            // IPFS is immutable. Unpinning is possible, but data remains on network.
            return Task.FromException(new NotSupportedException("Cannot delete from IPFS/Blockchain."));
        }

        public Task<bool> ExistsAsync(Uri uri) => Task.FromResult(true); // Optimistic
    }
}