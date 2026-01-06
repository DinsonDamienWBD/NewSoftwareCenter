using System;
using System.Threading;
using System.Threading.Tasks;

namespace DataWarehouse.SDK.Contracts.CategoryBases
{
    /// <summary>
    /// Base class for Feature plugins.
    /// Provides advanced capabilities: deduplication, caching, encryption, governance, etc.
    /// Plugins implement feature-specific logic.
    /// </summary>
    public abstract class FeaturePluginBase : PluginBase
    {
        /// <summary>Constructs feature plugin</summary>
        protected FeaturePluginBase(string id, string name, Version version)
            : base(id, name, version, PluginCategory.Feature)
        {
        }

        // Abstract members
        /// <summary>Feature type (e.g., "deduplication", "caching", "worm")</summary>
        protected abstract string FeatureType { get; }
        /// <summary>Initialize feature</summary>
        protected abstract Task InitializeFeatureAsync(IKernelContext context);
        /// <summary>Start background feature tasks</summary>
        protected abstract Task StartFeatureAsync(CancellationToken ct);
        /// <summary>Stop feature tasks</summary>
        protected abstract Task StopFeatureAsync();

        // Virtual members
        /// <summary>Custom initialization</summary>
        protected virtual void InitializeFeature(IKernelContext context) { }

        // Capabilities - Feature plugins define custom capabilities
        /// <summary>Feature capabilities (override to define specific capabilities)</summary>
        protected override PluginCapabilityDescriptor[] Capabilities => Array.Empty<PluginCapabilityDescriptor>();

        // Initialization
        /// <summary>Initializes feature</summary>
        protected override void InitializeInternal(IKernelContext context)
        {
            InitializeFeatureAsync(context).GetAwaiter().GetResult();
            InitializeFeature(context);

            // Start feature in background
            _ = Task.Run(() => StartFeatureAsync(CancellationToken.None));
        }

        // Shutdown
        /// <summary>Stops feature on shutdown</summary>
        protected override async Task OnShutdownAsync()
        {
            await StopFeatureAsync();
        }
    }
}
