using DataWarehouse.SDK.Contracts;
using System.Collections.Concurrent;
using System.Threading.Channels;

namespace DataWarehouse.Kernel.Realtime
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

        /// <summary>
        /// Name
        /// </summary>
        public string Name => "In memory Realtime Provider";

        // Map: SubscriptionID -> Handler
        private readonly ConcurrentDictionary<Guid, Subscription> _subs = new();
        private readonly Channel<StorageEvent> _channel;
        private readonly CancellationTokenSource _cts = new();
        private readonly Task _dispatcher;

        /// <summary>
        /// Constructor
        /// </summary>
        public InMemoryRealTimeProvider()
        {
            // Bounded Channel: Holds 5000 events. If full, publishers wait (Backpressure).
            // This prevents memory explosions during traffic spikes.
            var options = new BoundedChannelOptions(5000)
            {
                FullMode = BoundedChannelFullMode.Wait
            };
            _channel = Channel.CreateBounded<StorageEvent>(options);

            // Start the background dispatcher
            _dispatcher = Task.Run(DispatchLoop);
        }

        /// <summary>
        /// Initialize
        /// </summary>
        /// <param name="context"></param>
        public void Initialize(IKernelContext context)
        {
            // No setup needed for memory bus
        }

        /// <summary>
        /// Publish
        /// </summary>
        /// <param name="evt"></param>
        /// <returns></returns>
        public async Task PublishAsync(StorageEvent evt)
        {
            await _channel.Writer.WriteAsync(evt);
        }

        private async Task DispatchLoop()
        {
            try
            {
                await foreach (var evt in _channel.Reader.ReadAllAsync(_cts.Token))
                {
                    // Dispatch to all matching subscribers sequentially (or parallel limited)
                    // This ensures we process events at a sustainable rate.
                    foreach (var sub in _subs.Values)
                    {
                        if (IsMatch(sub.Pattern, evt.Uri))
                        {
                            try { sub.Handler(evt); } catch { /* Log Error in Prod */ }
                        }
                    }
                }
            }
            catch (OperationCanceledException) { }
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

        private static bool IsMatch(string pattern, string uri)
        {
            // Simple glob matching (production would use Regex or Prefix Tree)
            if (pattern == "*") return true;
            return uri.StartsWith(pattern.TrimEnd('*'));
        }

        private record Subscription(Guid Id, string Pattern, Action<StorageEvent> Handler);

        private class DisposableSubscription(Action action) : IAsyncDisposable
        {
            private readonly Action _action = action;

            public ValueTask DisposeAsync() { _action(); return ValueTask.CompletedTask; }
        }

        /// <summary>
        /// Safely dispose
        /// </summary>
        public void Dispose()
        {
            _cts.Cancel();
            _channel.Writer.TryComplete();
        }
    }
}