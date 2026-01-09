using DataWarehouse.SDK.Contracts;

namespace DataWarehouse.Kernel.IO
{
    /// <summary>
    /// Local disk provider
    /// </summary>
    /// <param name="rootPath"></param>
    public class LocalDiskProvider(string rootPath) : IStorageProvider
    {
        /// <summary>
        /// ID
        /// </summary>
        public string Id => "builtin-local-disk";

        /// <summary>
        /// Name
        /// </summary>
        public string Name => "Local Disk Provider";

        /// <summary>
        /// Version
        /// </summary>
        public string Version => "1.0";

        /// <summary>
        /// Scheme
        /// </summary>
        public string Scheme => "file";
        private readonly string _root = rootPath;

        /// <summary>
        /// Handshake implementation for IPlugin
        /// </summary>
        public Task<HandshakeResponse> OnHandshakeAsync(HandshakeRequest request)
        {
            Directory.CreateDirectory(_root);
            return Task.FromResult(HandshakeResponse.Success(
                pluginId: Id,
                name: Name,
                version: new Version(Version),
                category: PluginCategory.Storage,
                capabilities: new List<PluginCapabilityDescriptor>
                {
                    new PluginCapabilityDescriptor
                    {
                        CapabilityId = "storage.local.disk",
                        DisplayName = "Local Disk Storage",
                        Description = "Provides persistent local file system storage",
                        Category = CapabilityCategory.Storage
                    }
                },
                initDuration: TimeSpan.Zero
            ));
        }

        /// <summary>
        /// Message handler (optional for storage providers)
        /// </summary>
        public Task OnMessageAsync(PluginMessage message)
        {
            return Task.CompletedTask;
        }

        /// <summary>
        /// Initialize provider
        /// </summary>
        /// <param name="context"></param>
        public void Initialize(IKernelContext context) { Directory.CreateDirectory(_root); }

        /// <summary>
        /// Save
        /// </summary>
        /// <param name="uri"></param>
        /// <param name="data"></param>
        /// <returns></returns>
        public async Task SaveAsync(Uri uri, Stream data)
        {
            var path = GetPath(uri);
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            using var fs = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.None);
            await data.CopyToAsync(fs);
        }

        /// <summary>
        /// Load
        /// </summary>
        /// <param name="uri"></param>
        /// <returns></returns>
        /// <exception cref="FileNotFoundException"></exception>
        public Task<Stream> LoadAsync(Uri uri)
        {
            var path = GetPath(uri);
            if (!File.Exists(path)) throw new FileNotFoundException(path);
            return Task.FromResult<Stream>(new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read));
        }

        /// <summary>
        /// Delete
        /// </summary>
        /// <param name="uri"></param>
        /// <returns></returns>
        public Task DeleteAsync(Uri uri)
        {
            var path = GetPath(uri);
            if (File.Exists(path)) File.Delete(path);
            return Task.CompletedTask;
        }

        /// <summary>
        /// Exists
        /// </summary>
        /// <param name="uri"></param>
        /// <returns></returns>
        public Task<bool> ExistsAsync(Uri uri) => Task.FromResult(File.Exists(GetPath(uri)));

        private string GetPath(Uri uri)
        {
            // file:///container/blob -> root/container/blob
            string rel = uri.AbsolutePath.TrimStart('/').Replace('/', Path.DirectorySeparatorChar);
            return Path.Combine(_root, rel);
        }
    }
}