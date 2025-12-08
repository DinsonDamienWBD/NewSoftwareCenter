using SoftwareCenter.Core.Discovery;
using System;
using System.Collections.Generic;

namespace SoftwareCenter.Kernel.Services
{
    /// <summary>
    /// Defines the contract for a registry that tracks available service handlers
    /// and their priority for smart routing decisions.
    /// </summary>
    public interface IServiceRoutingRegistry
    {
        /// <summary>
        /// Registers a handler with its associated contract type and priority.
        /// </summary>
        /// <param name="contractType">The type of the command, event, or job contract.</param>
        /// <param name="handlerType">The concrete type of the handler.</param>
        /// <param name="handlerInterfaceType">The specific generic handler interface type (e.g., ICommandHandler&lt;T&gt;).</param>
        /// <param name="priority">The priority of the handler. Higher values mean higher priority.</param>
        /// <param name="owningModuleId">The ID of the module that owns this handler.</param>
        void RegisterHandler(Type contractType, Type handlerType, Type handlerInterfaceType, int priority, string owningModuleId);

        /// <summary>
        /// Gets the handler registration information for the highest priority handler
        /// for a given contract type.
        /// </summary>
        /// <param name="contractType">The type of the command, event, or job contract.</param>
        /// <returns>A HandlerRegistration object if found, otherwise null.</returns>
        HandlerRegistration GetHighestPriorityHandler(Type contractType);

        /// <summary>
        /// Gets all handler registration information for a given contract type, ordered by priority.
        /// </summary>
        /// <param name="contractType">The type of the command, event, or job contract.</param>
        /// <returns>A list of HandlerRegistration objects, ordered from highest to lowest priority.</returns>
        IEnumerable<HandlerRegistration> GetAllHandlers(Type contractType);
    }

    /// <summary>
    /// Represents a registered handler's metadata for routing purposes.
    /// </summary>
    public class HandlerRegistration
    {
        public Type ContractType { get; }
        public Type HandlerType { get; }
        public Type HandlerInterfaceType { get; }
        public int Priority { get; }
        public string OwningModuleId { get; }

        public HandlerRegistration(Type contractType, Type handlerType, Type handlerInterfaceType, int priority, string owningModuleId)
        {
            ContractType = contractType;
            HandlerType = handlerType;
            HandlerInterfaceType = handlerInterfaceType;
            Priority = priority;
            OwningModuleId = owningModuleId;
        }
    }
}
