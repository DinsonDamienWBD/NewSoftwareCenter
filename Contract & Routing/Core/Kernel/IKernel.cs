using SoftwareCenter.Core.Commands;
using SoftwareCenter.Core.Data;
using SoftwareCenter.Core.Events;
using SoftwareCenter.Core.Jobs;
using SoftwareCenter.Core.Routing;
using System;
using System.Threading.Tasks;

namespace SoftwareCenter.Core.Kernel
{
    /// <summary>
    /// The specific contract exposed to Modules during the Initialize phase.
    /// Aggregates Routing, Event Bus, and Data Store capabilities.
    /// </summary>
    public interface IKernel : IRouter
    {
        // 1. Reactivity
        IEventBus EventBus { get; }

        // 2. Shared Memory
        IGlobalDataStore DataStore { get; }

        IJobScheduler JobScheduler { get; }

        // 3. Registry (The fix for the Registration Paradox)
        /// <summary>
        /// Registers a capability (Command Handler) with the system.
        /// </summary>
        /// <param name="commandName">The unique command key (e.g. "User.Save").</param>
        /// <param name="handler">The function to execute.</param>
        /// <param name="metadata">Service discovery info.</param>
        void Register(string commandName, Func<ICommand, Task<IResult>> handler, RouteMetadata metadata);
    }
}