using System;
using Core.Infrastructure; // Requires IDataWarehouse to be defined here

namespace Core.Contracts
{
    /// <summary>
    /// The System Kernel interface.
    /// Acts as the bridge between Modules and the Core System.
    /// Modules use this to access storage, bus, and AI services.
    /// </summary>
    public interface ISystemKernel
    {
        /// <summary>
        /// Access to the Global Service Provider (DI Container).
        /// Allows resolving services like IEventBus, ILogger, etc.
        /// </summary>
        IServiceProvider Services { get; }

        /// <summary>
        /// Access to the Data Warehouse (Storage and Memory).
        /// </summary>
        IDataWarehouse Data { get; }
    }
}