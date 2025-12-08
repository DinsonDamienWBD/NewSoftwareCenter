using System.Collections.Generic;

namespace SoftwareCenter.Core.Discovery
{
    /// <summary>
    /// Provides a detailed, runtime description of a single capability (e.g., a command, event, or job)
    /// registered with the Kernel.
    /// </summary>
    public class CapabilityDescriptor
    {
        /// <summary>
        /// Gets the display name of the capability.
        /// </summary>
        public string Name { get; }

        /// <summary>
        /// Gets the description of what the capability does, typically sourced from XML documentation.
        /// </summary>
        public string Description { get; }

        /// <summary>
        /// Gets the type of the capability.
        /// </summary>
        public CapabilityType Type { get; }

        /// <summary>
        /// Gets the current runtime status of the capability.
        /// </summary>
        public CapabilityStatus Status { get; }

        /// <summary>
        /// Gets the priority of the handler associated with this capability.
        /// Higher values indicate higher priority.
        /// </summary>
        public int Priority { get; }

        /// <summary>
        /// Gets the full .NET type name of the primary contract (e.g., the ICommand class).
        /// </summary>
        public string ContractTypeName { get; }

        /// <summary>
        /// Gets the full .NET type name of the handler that executes the capability.
        /// </summary>
        public string HandlerTypeName { get; }

        /// <summary>
        /// Gets the ID of the module that owns and registered this capability.
        /// </summary>
        public string OwningModuleId { get; }

        /// <summary>
        /// Gets a list of descriptors for the parameters associated with this capability.
        /// </summary>
        public IReadOnlyList<ParameterDescriptor> Parameters { get; }

        /// <summary>
        /// Initializes a new instance of the <see cref="CapabilityDescriptor"/> class.
        /// </summary>
        public CapabilityDescriptor(
            string name,
            string description,
            CapabilityType type,
            CapabilityStatus status,
            int priority,
            string contractTypeName,
            string handlerTypeName,
            string owningModuleId,
            IReadOnlyList<ParameterDescriptor> parameters)
        {
            Name = name;
            Description = description;
            Type = type;
            Status = status;
            Priority = priority;
            ContractTypeName = contractTypeName;
            HandlerTypeName = handlerTypeName;
            OwningModuleId = owningModuleId;
            Parameters = parameters;
        }
    }
}
