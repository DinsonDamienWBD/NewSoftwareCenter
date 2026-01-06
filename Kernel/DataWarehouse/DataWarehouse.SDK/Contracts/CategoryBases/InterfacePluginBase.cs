using System;
using System.Threading;
using System.Threading.Tasks;

namespace DataWarehouse.SDK.Contracts.CategoryBases
{
    /// <summary>
    /// Base class for Interface/Protocol Adapter plugins.
    /// Exposes external APIs/protocols: SQL, REST, GraphQL, gRPC, etc.
    /// Plugins implement protocol-specific listeners and handlers.
    /// </summary>
    public abstract class InterfacePluginBase : PluginBase
    {
        /// <summary>Constructs interface plugin</summary>
        protected InterfacePluginBase(string id, string name, Version version)
            : base(id, name, version, PluginCategory.Interface)
        {
        }

        // Abstract members
        /// <summary>Interface type (e.g., "sql", "rest", "graphql")</summary>
        protected abstract string InterfaceType { get; }
        /// <summary>Initialize protocol handler</summary>
        protected abstract Task InitializeInterfaceAsync(IKernelContext context);
        /// <summary>Start listening on protocol port</summary>
        protected abstract Task StartListeningAsync(CancellationToken ct);
        /// <summary>Stop listener</summary>
        protected abstract Task StopListeningAsync();

        // Virtual members
        /// <summary>Custom initialization</summary>
        protected virtual void InitializeInterface(IKernelContext context) { }

        // Capabilities - Interface plugins define custom capabilities
        /// <summary>Interface capabilities (override to define specific capabilities)</summary>
        protected override PluginCapabilityDescriptor[] Capabilities => Array.Empty<PluginCapabilityDescriptor>();

        // Initialization
        /// <summary>Initializes interface</summary>
        protected override void InitializeInternal(IKernelContext context)
        {
            InitializeInterfaceAsync(context).GetAwaiter().GetResult();
            InitializeInterface(context);

            // Start listener in background
            _ = Task.Run(() => StartListeningAsync(CancellationToken.None));
        }

        // Shutdown
        /// <summary>Stops listener on shutdown</summary>
        protected override async Task OnShutdownAsync()
        {
            await StopListeningAsync();
        }
    }
}
