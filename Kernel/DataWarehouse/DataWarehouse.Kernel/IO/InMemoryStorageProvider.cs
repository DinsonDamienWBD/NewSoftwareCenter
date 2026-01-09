using DataWarehouse.SDK.Contracts;
using System.Collections.Concurrent;

namespace DataWarehouse.Kernel.IO
{
    /// <summary>
    /// VOLATILE STORAGE.
    /// Used when no physical storage plugins are loaded.
    /// Data is lost when the process exits.
    /// </summary>
    public class InMemoryStorageProvider : IStorageProvider
    {
        /// <summary>
        /// ID
        /// </summary>
        public static string Id => "builtin-ram-storage";

        /// <summary>
        /// Name
        /// </summary>
        public static string Name => "Volatile RAM Storage";

        /// <summary>
        /// Version
        /// </summary>
        public static string Version => "1.0";

        /// <summary>
        /// Scheme
        /// </summary>
        public string Scheme => "mem"; // mem://container/blob

        // The "Disk": A Dictionary of Byte Arrays
        private readonly ConcurrentDictionary<string, byte[]> _store = new();

        /// <summary>
        /// Handshake implementation for IPlugin
        /// </summary>
        public Task<HandshakeResponse> OnHandshakeAsync(HandshakeRequest request)
        {
            return Task.FromResult(HandshakeResponse.Success(
                pluginId: Id,
                name: Name,
                version: new Version(Version),
                category: PluginCategory.Storage,
                capabilities: new List<PluginCapabilityDescriptor>
                {
                    new PluginCapabilityDescriptor
                    {
                        CapabilityId = "storage.inmemory.volatile",
                        DisplayName = "Volatile RAM Storage",
                        Description = "Provides in-memory storage that is lost on process exit",
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
        /// Initialize
        /// </summary>
        /// <param name="context"></param>
        public static void Initialize(IKernelContext context)
        {
            context.LogWarning($"[{Id}] RUNNING IN VOLATILE MODE. DATA WILL BE LOST ON EXIT.");
        }

        /// <summary>
        /// Save
        /// </summary>
        /// <param name="uri"></param>
        /// <param name="data"></param>
        /// <returns></returns>
        public Task SaveAsync(Uri uri, Stream data)
        {
            // Copy stream to byte array (RAM)
            using var ms = new MemoryStream();
            data.CopyTo(ms);
            _store[uri.ToString()] = ms.ToArray();
            return Task.CompletedTask;
        }

        /// <summary>
        /// Load
        /// </summary>
        /// <param name="uri"></param>
        /// <returns></returns>
        /// <exception cref="FileNotFoundException"></exception>
        public Task<Stream> LoadAsync(Uri uri)
        {
            if (_store.TryGetValue(uri.ToString(), out var bytes))
            {
                return Task.FromResult<Stream>(new MemoryStream(bytes));
            }
            throw new FileNotFoundException($"RAM Blob not found: {uri}");
        }
        /// <summary>
        /// Delete
        /// </summary>
        /// <param name="uri"></param>
        /// <returns></returns>

        public Task DeleteAsync(Uri uri)
        {
            _store.TryRemove(uri.ToString(), out _);
            return Task.CompletedTask;
        }

        /// <summary>
        /// Exists
        /// </summary>
        /// <param name="uri"></param>
        /// <returns></returns>
        public Task<bool> ExistsAsync(Uri uri)
        {
            return Task.FromResult(_store.ContainsKey(uri.ToString()));
        }
    }
}