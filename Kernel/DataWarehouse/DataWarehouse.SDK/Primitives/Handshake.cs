using DataWarehouse.SDK.Utilities;
using System;
using System.Collections.Generic;
using System.Text;

namespace DataWarehouse.SDK.Primitives
{
    // SDK/Messaging/HandshakeRequest.cs

    /// <summary>
    /// Represents a request to initiate a handshake between a client and a kernel, providing protocol version, kernel
    /// identity, operating mode, and related context information.
    /// </summary>
    /// <remarks>This type is typically used during the initial connection phase to establish compatibility
    /// and exchange essential metadata between communicating components. The properties of this request convey the
    /// kernel's identity, supported protocol version, current operating mode, and any plugins that have already been
    /// loaded, which may affect dependency resolution.</remarks>
    public class HandshakeRequest
    {
        public string KernelId { get; init; } = string.Empty;
        public string ProtocolVersion { get; init; } = "1.0";
        public DateTime Timestamp { get; init; }
        public OperatingMode Mode { get; init; }
        public string RootPath { get; init; } = string.Empty;

        // For dependency resolution
        public List<PluginDescriptor> AlreadyLoadedPlugins { get; init; } = [];
    }

    // SDK/Messaging/HandshakeResponse.cs

    /// <summary>
    /// Represents the response returned by a plugin during the handshake process, providing identity, readiness,
    /// capabilities, dependencies, and metadata information.
    /// </summary>
    /// <remarks>This class is typically used to convey the result of a plugin's initialization and readiness
    /// check to a host application. It includes details such as the plugin's unique identifier, version, supported
    /// capabilities, dependencies, and any error information if initialization was unsuccessful. The handshake response
    /// enables the host to determine whether the plugin is ready for use and to understand its requirements and
    /// features.</remarks>
    public class HandshakeResponse
    {
        // Identity
        public string PluginId { get; init; } = string.Empty;
        public string Name { get; init; } = string.Empty;
        public Version Version { get; init; } = new Version(1, 0, 0);
        public PluginCategory Category { get; init; }

        // Readiness
        public bool Success { get; init; }
        public PluginReadyState ReadyState { get; init; }
        public string? ErrorMessage { get; init; }

        // Capabilities
        public List<PluginCapabilityDescriptor> Capabilities { get; init; } = [];

        // Dependencies
        public List<PluginDependency> Dependencies { get; init; } = [];

        // Metadata
        public Dictionary<string, object> Metadata { get; init; } = [];

        // Initialization time (for performance tracking)
        public TimeSpan InitializationDuration { get; init; }

        // Health check endpoint (optional)
        public TimeSpan? HealthCheckInterval { get; init; }
    }
}
