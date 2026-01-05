using DataWarehouse.Plugins.Features.Governance.Engine;
using DataWarehouse.Plugins.Features.Governance.Services;
using DataWarehouse.SDK.Contracts;
using Microsoft.Extensions.Configuration; // Ensure this using
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions; //
using System.Text;

namespace DataWarehouse.Plugins.Features.Governance.Bootstrapper
{
    /// <summary>
    /// Bootstrapper for Governance
    /// </summary>
    public class GovernancePlugin : IFeaturePlugin
    {
        /// <summary>
        /// ID
        /// </summary>
        public string Id => "DataWarehouse.Features.Governance";

        /// <summary>
        /// Version
        /// </summary>
        public string Version => "1.0.0";

        /// <summary>
        /// Name
        /// </summary>
        public string Name => "Governance";

        // Services
        private WormGovernor? _governor;
        private FlightRecorder? _recorder;
        private LifecyclePolicyEngine? _ilm;
        private AccessTracker? _tracker;

        // Runtime State
        private CancellationTokenSource? _cts;
        private Task? _ilmTask;
        private IKernelContext? _context;

        /// <summary>
        /// Initialize
        /// </summary>
        /// <param name="context"></param>
        public void Initialize(IKernelContext context)
        {
            _context = context;
            context.LogInfo($"[{Id}] Initializing Governance Suite...");

            string metadataPath = Path.Combine(context.RootPath, "Metadata");
            string logsPath = Path.Combine(context.RootPath, "AuditLogs");
            Directory.CreateDirectory(metadataPath);
            Directory.CreateDirectory(logsPath);

            // [FIX] Generate or Load a System Secret for WORM signing
            // In production, fetch this from IKeyStore. For now, derive from Machine/Path
            byte[] systemSecret = Encoding.UTF8.GetBytes(context.RootPath + "_WORM_SECRET");

            // 1. Initialize Independent Engines (WORM & Audit)
            _governor = new WormGovernor(metadataPath, systemSecret);
            _recorder = new FlightRecorder(logsPath);

            // 2. Resolve Critical Dependencies (Metadata Index)
            // Governance requires the Index to track access and enforce policies.
            var metadataIndex = context.GetPlugin<IMetadataIndex>() ?? throw new InvalidOperationException($"[{Id}] Critical Failure: IMetadataIndex not found. Governance cannot function.");

            // 3. Initialize Access Tracker
            // We pass the resolved index and a Logger Adapter
            _tracker = new AccessTracker(metadataIndex, new ContextLoggerAdapter<AccessTracker>(context));

            // 4. Initialize Lifecycle Policy Engine (ILM)
            // ILM needs to delete expired data, so it needs access to Storage Providers.
            // We pass the context so it can resolve specific providers dynamically during execution.
            // [FIX] Create or Fetch Dependencies
            var config = new ConfigurationBuilder()
                .AddJsonFile(Path.Combine(context.RootPath, "appsettings.json"), optional: true)
                .AddEnvironmentVariables()
                .Build();

            var loggerFactory = LoggerFactory.Create(b => b.AddConsole());
            var logger = loggerFactory.CreateLogger<LifecyclePolicyEngine>();

            // [FIX] Pass dependencies
            _ilm = new LifecyclePolicyEngine(metadataIndex, context, config, logger);

            context.LogInfo($"[{Id}] WORM Governor, Flight Recorder, Access Tracker & ILM ready.");
        }

        /// <summary>
        /// Start
        /// </summary>
        /// <param name="ct"></param>
        /// <returns></returns>
        public Task StartAsync(CancellationToken ct)
        {
            _cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            _context?.LogInfo($"[{Id}] Starting Governance Background Agents...");

            // Start the Daily Audit Loop
            if (_ilm != null)
            {
                _ilmTask = Task.Run(() => _ilm.RunLifecycleLoopAsync(_cts.Token), _cts.Token);
            }

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
                if (_ilmTask != null)
                {
                    try { await _ilmTask; } catch (OperationCanceledException) { }
                }
                _cts.Dispose();
            }

            _context?.LogInfo($"[{Id}] Governance Agents stopped.");
        }

        // --- Bridge for ILogger (Helper Class) ---
        /// <summary>
        /// Context logger adapter.
        /// </summary>
        /// <param name="ctx"></param>
        private class ContextLoggerAdapter<T>(IKernelContext ctx) : Microsoft.Extensions.Logging.ILogger<T>, IDisposable
        {
            private readonly IKernelContext _ctx = ctx;

            /// <summary>
            /// Begin scope
            /// </summary>
            /// <typeparam name="TState"></typeparam>
            /// <param name="state"></param>
            /// <returns></returns>
            public IDisposable? BeginScope<TState>(TState state) where TState : notnull => this;

            /// <summary>
            /// Safely dispose
            /// </summary>
            public void Dispose() { }

            /// <summary>
            /// Is enabled
            /// </summary>
            /// <param name="logLevel"></param>
            /// <returns></returns>
            public bool IsEnabled(Microsoft.Extensions.Logging.LogLevel logLevel) => true;

            /// <summary>
            /// Log
            /// </summary>
            /// <typeparam name="TState"></typeparam>
            /// <param name="logLevel"></param>
            /// <param name="eventId"></param>
            /// <param name="state"></param>
            /// <param name="exception"></param>
            /// <param name="formatter"></param>
            public void Log<TState>(Microsoft.Extensions.Logging.LogLevel logLevel, Microsoft.Extensions.Logging.EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
            {
                string msg = $"[{typeof(T).Name}] {formatter(state, exception)}";
                if (logLevel >= Microsoft.Extensions.Logging.LogLevel.Error) _ctx.LogError(msg, exception);
                else _ctx.LogInfo(msg);
            }
        }
    }
}