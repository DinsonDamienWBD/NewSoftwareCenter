using Core.AI;
using Core.Diagnostics;
using Core.Registry;
using Microsoft.Extensions.Configuration;

namespace Core.Contracts
{
    /// <summary>
    /// The Lifecycle Contract for a Plugin Module.
    /// </summary>
    public interface IModule
    {
        // Identity

        /// <summary>
        /// Identifier of the module.
        /// </summary>
        string ModuleId { get; }

        /// <summary>
        /// Version of the module.
        /// </summary>
        string Version { get; }

        /// <summary>
        /// Version of the kernel required by the module.
        /// </summary>
        string MinKernelVersion { get; }

        /// <summary>
        /// Order
        /// </summary>
        int Order { get; }

        /// <summary>
        /// Gets the registry records provided by the module.
        /// 1. Handshake: Return the manifest of capabilities.
        /// </summary>
        /// <returns></returns>
        IEnumerable<RegistryRecord> GetRegistry();

        /// <summary>
        /// Prepare the module with configuration.
        /// 2. Prepare: Initialize heavy resources (DB, sockets).
        /// </summary>
        /// <param name="config"></param>
        /// <returns></returns>
        Task PrepareAsync(IConfiguration config);

        /// <summary>
        /// Activate the module to start processing.
        /// 3. Activate: Connect to the bus and start processing.
        /// </summary>
        /// <returns></returns>
        Task ActivateAsync();

        /// <summary>
        /// Shutdown the module gracefully.
        /// 4. Shutdown: Graceful cleanup.
        /// </summary>
        /// <returns></returns>
        Task ShutdownAsync();

        /// <summary>
        /// Module health probes.
        /// Monitoring.
        /// </summary>
        /// <returns></returns>
        IEnumerable<IProbe> GetProbes();

        /// <summary>
        /// Initialize Module
        /// </summary>
        /// <param name="kernel"></param>
        void Initialize(ISystemKernel kernel);

        /// <summary>
        /// Modules explicitly register "Skills" or "Tools" that the AI Agent can use.
        /// e.g., CalendarModule registers "BookMeeting", EmailModule registers "SendEmail".
        /// </summary>
        /// <returns></returns>
        IEnumerable<AiToolDefinition> GetAiTools();
    }
}