using System;

namespace DataWarehouse.Fabric
{
    /// <summary>
    /// The effeciency engine. Uses hash to prevent duplicate storage
    /// </summary>
    /// <param name="rootPath"></param>
    public class DeduplicationTable(string rootPath) : IDisposable
    {
        private readonly DurableState<string> _store = new(rootPath, "dedupe");

        /// <summary>
        /// Try to get existing state
        /// </summary>
        /// <param name="hash"></param>
        /// <param name="existingUri"></param>
        /// <returns></returns>
        public bool TryGetExisting(string hash, out string existingUri)
        {
            return _store.TryGet(hash, out existingUri!);
        }

        /// <summary>
        /// Register a new hash
        /// </summary>
        /// <param name="hash"></param>
        /// <param name="uri"></param>
        public void Register(string hash, string uri)
        {
            _store.Set(hash, uri);
        }

        /// <summary>
        /// Safely dispose
        /// </summary>
        public void Dispose() => _store.Dispose();
    }
}