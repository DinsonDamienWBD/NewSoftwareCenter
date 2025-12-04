using SoftwareCenter.Core.Diagnostics;
using System.Collections.Generic;

namespace SoftwareCenter.Core.Commands
{
    /// <summary>
    /// The universal envelope for all actions in the system.
    /// Used to decouple the Host from the Kernel and Modules.
    /// </summary>
    public interface ICommand
    {
        /// <summary>
        /// The unique identifier for the action (e.g., "Module.Install", "Weather.Get").
        /// </summary>
        string Name { get; }

        /// <summary>
        /// The payload for the command.
        /// Key: Parameter name (e.g., "FilePath").
        /// Value: The data (e.g., "C:/Temp/file.zip").
        /// </summary>
        Dictionary<string, object> Parameters { get; }

        /// <summary>
        /// TRACING
        /// </summary>
        Guid TraceId { get; }

        /// <summary>
        /// The Audit Trail.
        /// The Kernel's Proxy automatically adds to this list.
        /// Modules generally do not touch this.
        /// </summary>
        List<TraceHop> History { get; }
    }
}