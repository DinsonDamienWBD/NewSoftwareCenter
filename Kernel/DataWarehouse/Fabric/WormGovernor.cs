using System.Collections.Concurrent;

namespace DataWarehouse.Fabric
{
    /// <summary>
    /// The Compliance Officer. Enforces "Write Once, Read Many".
    /// Now backed by Disk. Compliance Locks persist forever.
    /// </summary>
    public class WormGovernor(string rootPath) : System.IDisposable
    {
        // Store Expiration Ticks (long) instead of DateTimeOffset for easier serialization
        private readonly DurableState<long> _store = new DurableState<long>(rootPath, "worm_locks");

        // Map: BlobURI -> ExpirationDate
        private readonly ConcurrentDictionary<string, DateTimeOffset> _locks = new();

        /// <summary>
        /// Lock a BLOB for write
        /// </summary>
        /// <param name="uri"></param>
        /// <param name="retention"></param>
        public void LockBlob(string uri, TimeSpan retention)
        {
            var newExpiry = DateTimeOffset.UtcNow.Add(retention).Ticks;

            if (_store.TryGet(uri, out long currentTicks))
            {
                // WORM Rule: Can only Extend, never Shorten
                if (newExpiry > currentTicks) _store.Set(uri, newExpiry);
            }
            else
            {
                _store.Set(uri, newExpiry);
            }
        }

        /// <summary>
        /// Assert blob lock
        /// </summary>
        /// <param name="uri"></param>
        /// <param name="isDelete"></param>
        /// <exception cref="UnauthorizedAccessException"></exception>
        public void AssertAccess(string uri, bool isDelete)
        {
            if (!isDelete) return;

            if (_store.TryGet(uri, out long expiryTicks))
            {
                var expiry = new DateTimeOffset(expiryTicks, TimeSpan.Zero);
                if (DateTimeOffset.UtcNow < expiry)
                {
                    throw new UnauthorizedAccessException($"WORM LOCK: Blob {uri} is protected until {expiry}");
                }
            }
        }

        /// <summary>
        /// Safely dispose durablestate store
        /// </summary>
        public void Dispose() => _store.Dispose();
    }
}