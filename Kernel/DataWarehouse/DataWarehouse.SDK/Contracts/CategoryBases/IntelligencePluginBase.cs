using System;
using System.Threading;
using System.Threading.Tasks;

namespace DataWarehouse.SDK.Contracts.CategoryBases
{
    /// <summary>
    /// Base class for Intelligence/AI/Governance plugins.
    /// Handles AI-driven governance, policy enforcement, anomaly detection, self-healing.
    /// Plugins implement AI/ML logic and governance rules.
    /// </summary>
    public abstract class IntelligencePluginBase : PluginBase
    {
        /// <summary>Constructs intelligence plugin</summary>
        protected IntelligencePluginBase(string id, string name, Version version)
            : base(id, name, version, PluginCategory.Intelligence)
        {
        }

        // Abstract members
        /// <summary>Intelligence type (e.g., "neural-sentinel", "anomaly-detector")</summary>
        protected abstract string IntelligenceType { get; }
        /// <summary>Initialize AI/governance engine</summary>
        protected abstract Task InitializeIntelligenceAsync(IKernelContext context);
        /// <summary>Start background intelligence tasks</summary>
        protected abstract Task StartIntelligenceAsync(CancellationToken ct);
        /// <summary>Stop intelligence tasks</summary>
        protected abstract Task StopIntelligenceAsync();

        // Virtual members
        /// <summary>Custom initialization</summary>
        protected virtual void InitializeIntelligence(IKernelContext context) { }

        // Capabilities - Intelligence plugins define custom capabilities
        /// <summary>Intelligence capabilities (override to define specific capabilities)</summary>
        protected override PluginCapabilityDescriptor[] Capabilities => Array.Empty<PluginCapabilityDescriptor>();

        // Initialization
        /// <summary>Initializes intelligence</summary>
        protected override void InitializeInternal(IKernelContext context)
        {
            InitializeIntelligenceAsync(context).GetAwaiter().GetResult();
            InitializeIntelligence(context);

            // Start intelligence in background
            _ = Task.Run(() => StartIntelligenceAsync(CancellationToken.None));
        }

        // Shutdown
        /// <summary>Stops intelligence on shutdown</summary>
        protected override async Task OnShutdownAsync()
        {
            await StopIntelligenceAsync();
        }
    }
}
