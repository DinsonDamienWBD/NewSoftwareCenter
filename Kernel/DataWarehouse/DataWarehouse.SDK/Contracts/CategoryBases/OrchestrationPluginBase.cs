using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace DataWarehouse.SDK.Contracts.CategoryBases
{
    /// <summary>
    /// Base class for Orchestration plugins.
    /// Handles distributed coordination: consensus, leader election, federation, tiering, replication.
    /// Plugins implement protocol-specific orchestration logic.
    /// </summary>
    public abstract class OrchestrationPluginBase : PluginBase
    {
        /// <summary>Constructs orchestration plugin</summary>
        protected OrchestrationPluginBase(string id, string name, Version version)
            : base(id, name, version, PluginCategory.Orchestration)
        {
        }

        // Abstract members
        /// <summary>Orchestration type (e.g., "raft", "paxos", "tiering")</summary>
        protected abstract string OrchestrationType { get; }
        /// <summary>Initialize orchestration</summary>
        protected abstract Task InitializeOrchestrationAsync(IKernelContext context);
        /// <summary>Start background orchestration tasks</summary>
        protected abstract Task StartOrchestrationAsync(CancellationToken ct);
        /// <summary>Stop orchestration</summary>
        protected abstract Task StopOrchestrationAsync();

        // Virtual members
        /// <summary>Custom initialization</summary>
        protected virtual void InitializeOrchestration(IKernelContext context) { }

        // Capabilities - Orchestration plugins define custom capabilities
        /// <summary>Orchestration capabilities (override to define specific capabilities)</summary>
        protected override PluginCapabilityDescriptor[] Capabilities => Array.Empty<PluginCapabilityDescriptor>();

        // Initialization
        /// <summary>Initializes orchestration</summary>
        protected override void InitializeInternal(IKernelContext context)
        {
            InitializeOrchestrationAsync(context).GetAwaiter().GetResult();
            InitializeOrchestration(context);

            // Start orchestration in background
            _ = Task.Run(() => StartOrchestrationAsync(CancellationToken.None));
        }

        // Shutdown
        /// <summary>Stops orchestration on shutdown</summary>
        protected override async Task OnShutdownAsync()
        {
            await StopOrchestrationAsync();
        }
    }
}
