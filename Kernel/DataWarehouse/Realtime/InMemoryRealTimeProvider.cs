using System.Collections.Concurrent;
using DataWarehouse.Contracts;

namespace DataWarehouse.Realtime
{
    /// <summary>
    /// In memory realtime provider
    /// The default "Laptop Mode" implementation.
    /// </summary>
    public class InMemoryRealTimeProvider : IRealTimeProvider
    {
        /// <summary>
        /// ID
        /// </summary>
        public string Id => "MemBus";

        /// <summary>
        /// Version
        /// </summary>
        public string Version => "1.0";

        // Map: SubscriptionID -> Handler
        private readonly ConcurrentDictionary<Guid, Subscription> _subs = new();

        /// <summary>
        /// Publish
        /// </summary>
        /// <param name="evt"></param>
        /// <returns></returns>
        public Task PublishAsync(StorageEvent evt)
        {
            foreach (var sub in _subs.Values)
            {
                if (IsMatch(sub.Pattern, evt.Uri))
                {
                    // Fire and forget to avoid blocking the writer
                    Task.Run(() => sub.Handler(evt));
                }
            }
            return Task.CompletedTask;
        }

        /// <summary>
        /// Subscribe
        /// </summary>
        /// <param name="pattern"></param>
        /// <param name="handler"></param>
        /// <returns></returns>
        public Task<IAsyncDisposable> SubscribeAsync(string pattern, Action<StorageEvent> handler)
        {
            var id = Guid.NewGuid();
            var sub = new Subscription(id, pattern, handler);
            _subs[id] = sub;

            return Task.FromResult<IAsyncDisposable>(new DisposableSubscription(() => _subs.TryRemove(id, out _)));
        }

        private bool IsMatch(string pattern, string uri)
        {
            // Simple glob matching (production would use Regex or Prefix Tree)
            if (pattern == "*") return true;
            return uri.StartsWith(pattern.TrimEnd('*'));
        }

        private record Subscription(Guid Id, string Pattern, Action<StorageEvent> Handler);

        private class DisposableSubscription : IAsyncDisposable
        {
            private readonly Action _action;
            public DisposableSubscription(Action action) => _action = action;
            public ValueTask DisposeAsync() { _action(); return ValueTask.CompletedTask; }
        }
    }
}