using DataWarehouse.Plugins.Features.AI.Engine;
using DataWarehouse.SDK.Contracts;

namespace DataWarehouse.Plugins.Features.AI.Bootstrapper
{
    /// <summary>
    /// Bootstrapper for AI Search services
    /// </summary>
    public class NeuralSearchPlugin : IFeaturePlugin
    {
        /// <summary>
        /// ID
        /// </summary>
        public string Id => "DataWarehouse.Features.Neural";

        /// <summary>
        /// Version
        /// </summary>
        public string Version => "5.0.0-GodTier";

        /// <summary>
        /// Name
        /// </summary>
        public string Name => "AI Search";

        private GraphVectorIndex? _vectorIndex;
        private NeuralHydrator? _hydrator;
        private IKernelContext? _context;
        private Task? _hydrationTask;
        private CancellationTokenSource? _cts;

        /// <summary>
        /// Initialize
        /// </summary>
        /// <param name="context"></param>
        public void Initialize(IKernelContext context)
        {
            _context = context;
            context.LogInfo($"[{Id}] Initializing HNSW Neural Engine...");

            // 1. Create the Logger Bridge
            // This satisfies the GraphVectorIndex(ILogger<T>) requirement
            var engineLogger = new ContextLoggerAdapter<GraphVectorIndex>(context);

            // 2. Initialize the Engine (HNSW Graph)
            _vectorIndex = new GraphVectorIndex(engineLogger);

            // 3. Initialize the Service (Hydrator)
            // Passes the engine and context to the manager
            _hydrator = new NeuralHydrator(_vectorIndex, context);

            context.LogInfo($"[{Id}] Engine & Hydrator linked.");
        }

        /// <summary>
        /// Start
        /// </summary>
        /// <param name="ct"></param>
        /// <returns></returns>
        public Task StartAsync(CancellationToken ct)
        {
            if (_hydrator == null || _context == null)
            {
                throw new InvalidOperationException("NeuralSearchPlugin not initialized.");
            }

            _cts = CancellationTokenSource.CreateLinkedTokenSource(ct);

            _context.LogInfo($"[{Id}] Starting Asynchronous Hydration...");

            // Fire-and-forget hydration task protected by CTS
            _hydrationTask = Task.Run(() => _hydrator.HydrateAsync(_cts.Token), _cts.Token);

            return Task.CompletedTask;
        }

        /// <summary>
        /// Stop
        /// </summary>
        /// <returns></returns>
        public async Task StopAsync()
        {
            if (_cts != null)
            {
                _cts.Cancel();
                if (_hydrationTask != null)
                {
                    try
                    {
                        await _hydrationTask;
                    }
                    catch (OperationCanceledException) { /* Expected during shutdown */ }
                }
                _cts.Dispose();
            }
            _context?.LogInfo($"[{Id}] Neural Engine Stopped.");
        }

        /// <summary>
        /// Public API for other plugins/Kernel to use (if casted)
        /// </summary>
        /// <param name="vector"></param>
        /// <param name="limit"></param>
        /// <returns></returns>
        public Task<string[]> SearchAsync(float[] vector, int limit)
        {
            if (_vectorIndex == null) return Task.FromResult(Array.Empty<string>());

            // Helper to bridge Engine's List<string> to Array
            var results = _vectorIndex.Search(vector, limit);
            return Task.FromResult(results.ToArray());
        }
    }
}