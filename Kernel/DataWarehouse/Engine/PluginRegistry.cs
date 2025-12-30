using DataWarehouse.Contracts;

namespace DataWarehouse.Engine
{
    /// <summary>
    /// The registry that holds loaded plugins
    /// </summary>
    public class PluginRegistry
    {
        private readonly List<IStorageProvider> _storage = new();
        private readonly List<ICryptoProvider> _crypto = new();
        private readonly List<ICompressionProvider> _compression = new();
        private readonly List<IPlugin> _allPlugins = new();

        /// <summary>
        /// List of crypto algorithms
        /// </summary>
        public IEnumerable<ICryptoProvider> CryptoAlgos => _crypto;

        /// <summary>
        /// List of compression algorithms
        /// </summary>
        public IEnumerable<ICompressionProvider> CompressionAlgos => _compression;

        /// <summary>
        /// Register a plugin
        /// </summary>
        /// <param name="plugin"></param>
        /// <exception cref="NotSupportedException"></exception>
        public void Register(IPlugin plugin)
        {
            _allPlugins.Add(plugin); // Register in general list

            switch (plugin)
            {
                case IStorageProvider s: _storage.Add(s); break;
                case ICryptoProvider c: _crypto.Add(c); break;
                case ICompressionProvider z: _compression.Add(z); break;
                    // Other types just sit in _allPlugins
            }
        }

        /// <summary>
        /// Get storage
        /// </summary>
        /// <param name="scheme"></param>
        /// <returns></returns>
        /// <exception cref="InvalidOperationException"></exception>
        public IStorageProvider GetStorage(string scheme)
            => _storage.FirstOrDefault(s => s.Scheme.Equals(scheme, StringComparison.OrdinalIgnoreCase))
               ?? throw new InvalidOperationException($"No storage provider found for scheme: {scheme}");

        /// <summary>
        /// Get crypto
        /// </summary>
        /// <param name="id"></param>
        /// <returns></returns>
        /// <exception cref="InvalidOperationException"></exception>
        public ICryptoProvider GetCrypto(string id)
            => _crypto.FirstOrDefault(c => c.Id.Equals(id, StringComparison.OrdinalIgnoreCase))
               ?? throw new InvalidOperationException($"No crypto provider found for ID: {id}");

        /// <summary>
        /// Get compression
        /// </summary>
        /// <param name="id"></param>
        /// <returns></returns>
        /// <exception cref="InvalidOperationException"></exception>
        public ICompressionProvider GetCompression(string id)
            => _compression.FirstOrDefault(c => c.Id.Equals(id, StringComparison.OrdinalIgnoreCase))
               ?? throw new InvalidOperationException($"No compression provider found for ID: {id}");

        /// <summary>
        /// Get plugin
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <returns></returns>
        public T? GetPlugin<T>() where T : class
        {
            return _allPlugins.OfType<T>().FirstOrDefault();
        }

        /// <summary>
        /// Get all plugins
        /// </summary>
        /// <returns></returns>
        public IEnumerable<string> GetAllPluginIds() => _allPlugins.Select(p => p.Id);
    }
}