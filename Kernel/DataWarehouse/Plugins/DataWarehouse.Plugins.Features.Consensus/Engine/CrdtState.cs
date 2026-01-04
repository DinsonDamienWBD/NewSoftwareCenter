using System.Collections.Concurrent;

namespace DataWarehouse.Plugins.Features.Consensus.Engine
{
    /// <summary>
    /// Last-Write-Wins Register (LWW-Register).
    /// Converges to the latest timestamp. secure against clock skew logic if using Vector Clocks (simplified here).
    /// </summary>
    public class CrdtState<T>
    {
        private readonly ConcurrentDictionary<string, Entry<T>> _store = new();

        private record Entry<TVal>(TVal Value, long Timestamp);

        /// <summary>
        /// Set
        /// </summary>
        /// <param name="key"></param>
        /// <param name="value"></param>
        public void Set(string key, T value)
        {
            var now = DateTime.UtcNow.Ticks;
            _store.AddOrUpdate(key,
                new Entry<T>(value, now), // Inner T matches Outer T, so this usage is fine, but definition was ambiguous
                (k, existing) => now > existing.Timestamp ? new Entry<T>(value, now) : existing);
        }

        /// <summary>
        /// Get
        /// </summary>
        /// <param name="key"></param>
        /// <returns></returns>
        public T? Get(string key)
        {
            return _store.TryGetValue(key, out var entry) ? entry.Value : default;
        }

        /// <summary>
        /// Merge state from another node (Gossip Protocol).
        /// </summary>
        public void Merge(Dictionary<string, (T Val, long Time)> remoteState)
        {
            foreach (var item in remoteState)
            {
                _store.AddOrUpdate(item.Key,
                    new Entry<T>(item.Value.Val, item.Value.Time),
                    (k, existing) => item.Value.Time > existing.Timestamp
                        ? new Entry<T>(item.Value.Val, item.Value.Time)
                        : existing);
            }
        }
    }
}