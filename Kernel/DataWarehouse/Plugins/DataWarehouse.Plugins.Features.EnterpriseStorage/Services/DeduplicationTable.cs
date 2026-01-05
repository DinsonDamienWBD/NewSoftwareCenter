using DataWarehouse.SDK.Utilities;

namespace DataWarehouse.Plugins.Features.EnterpriseStorage.Services
{
    /// <summary>
    /// Manages the mapping between Content Hashes and Blob URIs.
    /// Enables Instance-Single Storage (Deduplication).
    /// </summary>
    public class DeduplicationTable : IDisposable
    {
        private readonly DurableState<string> _store;

        /// <summary>
        /// Initializes the table with persistent storage.
        /// </summary>
        /// <param name="rootPath">The root directory for metadata.</param>
        public DeduplicationTable(string rootPath)
        {
            // [Fix] Combine path manually and pass 1 argument to DurableState constructor
            string dbPath = Path.Combine(rootPath, "dedupe.json");
            _store = new DurableState<string>(dbPath);
        }

        /// <summary>
        /// Attempts to find an existing URI for a given content hash.
        /// </summary>
        /// <param name="hash">The SHA256 hash.</param>
        /// <param name="existingUri">The URI if found.</param>
        /// <returns>True if found.</returns>
        public bool TryGetExisting(string hash, out string existingUri)
        {
            return _store.TryGet(hash, out existingUri!);
        }

        /// <summary>
        /// Registers a new Hash -> URI mapping.
        /// </summary>
        public void Register(string hash, string uri)
        {
            _store.Set(hash, uri);
        }

        /// <summary>
        /// Disposes the underlying storage state.
        /// </summary>
        public void Dispose() => _store.Dispose();
    }
}