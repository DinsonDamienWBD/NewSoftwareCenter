using System;
using System.Collections.Generic;
using System.Text;

namespace DataWarehouse.SDK.Primitives
{
    /// <summary>
    /// Specifies the categories of plugins available for extending application functionality, such as data
    /// transformation, storage, metadata indexing, security, and orchestration.
    /// </summary>
    /// <remarks>Use this enumeration to classify plugins by their primary role within the system.
    /// Categorizing plugins enables applications to discover, filter, or configure plugins based on their intended
    /// purpose, supporting modular and extensible architectures.</remarks>
    public enum PluginCategory
    {
        /// <summary>
        /// Anything that mutates data
        /// Includes compression, encryption, data format changes, etc.
        /// </summary>
        DataTransformationProvider,

        /// <summary>
        /// Represents an abstraction for a storage provider that manages data persistence and retrieval.
        /// </summary>
        /// <remarks>Implementations of this interface or class provide mechanisms to store and access
        /// data from various storage backends, such as file systems, databases, or cloud storage services. Use this
        /// type to decouple application logic from specific storage implementations.</remarks>
        StorageProvider,

        /// <summary>
        /// Provides an interface for indexing and retrieving metadata for content items.  
        /// </summary>
        /// <remarks>Implementations of this provider enable efficient searching and filtering of content
        /// based on associated metadata. This type is typically used in scenarios where content needs to be
        /// discoverable or categorized by metadata attributes.</remarks>
        MetadataIndexingProvider,

        /// <summary>
        /// Provides methods and properties for managing security operations such as authentication, authorization, 
        /// ACL, credential management etc.
        /// </summary>
        /// <remarks>Use this class to implement or access security-related functionality within an
        /// application. The specific capabilities and usage patterns depend on the implementation of the security
        /// provider. This class may be used to abstract different security mechanisms, allowing for flexible
        /// integration with various authentication or authorization systems.</remarks>
        SecurityProvider,

        /// <summary>
        /// Represents a provider that manages orchestration operations or services.
        /// </summary>
        OrchestrationProvider
    }

    /// <summary>
    /// Specifies the category of a capability provided by a system or component.
    /// </summary>
    /// <remarks>Use this enumeration to classify capabilities into functional groups such as storage,
    /// security, or diagnostics. This can help organize, filter, or present capabilities based on their primary
    /// purpose.</remarks>
    public enum CapabilityCategory
    {
        /// <summary>
        /// Represents a storage mechanism for persisting and retrieving data.
        /// </summary>
        /// <remarks>Use this class to interact with underlying data storage systems. The specific storage
        /// implementation and supported operations may vary depending on the derived type.</remarks>
        Storage,

        /// <summary>
        /// Gets or sets the metadata associated with the current object.
        /// </summary>
        /// <remarks>Use this property to store or retrieve additional information relevant to the object,
        /// such as descriptive attributes or custom data. The structure and meaning of the metadata depend on the
        /// specific implementation and usage context.</remarks>
        Metadata,

        /// <summary>
        /// Provides security-related functionality and settings for the application.
        /// </summary>
        Security,

        /// <summary>
        /// Gets or sets the transformation matrix that defines the position, rotation, and scale of the object in world
        /// space.
        /// </summary>
        Transform,

        /// <summary>
        /// Represents a workflow or process that coordinates and manages the execution of multiple tasks or activities.
        /// </summary>
        /// <remarks>Use this type to define, control, or monitor the flow of operations that require
        /// coordination between multiple steps or services. The specific behavior and capabilities of the orchestration
        /// depend on the implementation.</remarks>
        Orchestration,

        /// <summary>
        /// Represents a measure of cognitive ability or reasoning capacity.
        /// </summary>
        Intelligence,

        /// <summary>
        /// Represents a query to be executed against a data source.
        /// </summary>
        Query,

        /// <summary>
        /// Represents a maintenance operation or status within the application.
        /// </summary>
        Maintenance,

        /// <summary>
        /// Represents diagnostic information, such as errors, warnings, or informational messages, produced during
        /// processing or analysis.
        /// </summary>
        Diagnostic
    }

    /// <summary>
    /// Specifies the relative priority of an action or operation.
    /// </summary>
    /// <remarks>Use this enumeration to indicate the importance of an action when scheduling, processing, or
    /// displaying tasks. Higher priority values, such as High or Critical, typically represent actions that should be
    /// handled before those with lower priority.</remarks>
    public enum ActionPriority
    {
        Low,
        Normal,
        High,
        Critical
    }

    /// <summary>
    /// Specifies the severity level of an action or event.
    /// </summary>
    /// <remarks>Use this enumeration to indicate the importance or urgency of an action, such as logging
    /// events, notifications, or alerts. The values range from informational messages to critical conditions that may
    /// require immediate attention.</remarks>
    public enum ActionSeverity
    {
        Informational,
        Normal,
        High,
        Critical
    }

    /// <summary>
    /// Defines the operational environment of the Data Warehouse.
    /// Used by the Kernel to automatically select the best plugins (Intelligent Defaults).
    /// </summary>
    public enum OperatingMode
    {
        /// <summary>
        /// Resource-constrained environment (e.g., Battery powered, Low RAM).
        /// Prefers: In-Memory Index, Compression (Save Disk), Single Threading.
        /// </summary>
        Laptop,

        /// <summary>
        /// Standard workstation (e.g., Desktop PC).
        /// Prefers: SQLite Index, Folder Storage, Balanced Threading.
        /// </summary>
        Workstation,

        /// <summary>
        /// High-performance environment (e.g., Dedicated Server).
        /// Prefers: Postgres Index, VDI Storage, High Concurrency, Background Optimization.
        /// </summary>
        Server,

        /// <summary>
        /// Containerized or Cloud Cluster (e.g., Docker, Kubernetes).
        /// Prefers: Network Storage, Raft Consensus, Stateless Operation.
        /// </summary>
        Hyperscale
    }

    /// <summary>
    /// Level of security to be used
    /// </summary>
    public enum SecurityLevel
    {
        /// <summary>
        /// No security
        /// </summary>
        None,

        /// <summary>
        /// Standard security level
        /// </summary>
        Standard,

        /// <summary>
        /// High level of security
        /// </summary>
        High,

        /// <summary>
        /// Top level security
        /// </summary>
        Quantum
    }

    /// <summary>
    /// Level of compression to b∈ used
    /// </summary>
    public enum CompressionLevel
    {
        /// <summary>
        /// No compression
        /// </summary>
        None,

        /// <summary>
        /// Fast but less compression
        /// </summary>
        Fast,

        /// <summary>
        /// Optimal balance between compression and performance
        /// </summary>
        Optimal,

        /// <summary>
        /// Slow but more compression
        /// </summary>
        High
    }

    /// <summary>
    /// Level of availability
    /// </summary>
    public enum AvailabilityLevel
    {
        /// <summary>
        /// Single copy, no redundancy
        /// </summary>
        Single,

        /// <summary>
        /// Redundancy applied
        /// </summary>
        Redundant,

        /// <summary>
        /// Geo level redundancy applied
        /// </summary>
        GeoRedundant,

        /// <summary>
        /// Global redundancy
        /// </summary>
        Global
    }

    /// <summary>
    /// Represents the performance characteristic of a storage node.
    /// </summary>
    public enum StorageTier
    {
        /// <summary>
        /// Hot
        /// </summary>
        Hot,

        /// <summary>
        /// Warm
        /// </summary>
        Warm,

        /// <summary>
        /// Cold
        /// </summary>
        Cold
    }

    /// <summary>
    /// Defines when the Sentinel was invoked.
    /// </summary>
    public enum TriggerType
    {
        /// <summary>
        /// Invoked just before data is written to disk.
        /// </summary>
        OnWrite,

        /// <summary>
        /// Invoked just before data is returned to the user.
        /// </summary>
        OnRead,

        /// <summary>
        /// Invoked during a background scan (Idle time).
        /// </summary>
        OnSchedule,

        /// <summary>
        /// Invoked just before data is deleted.
        /// </summary>
        OnDelete
    }

    /// <summary>
    /// Severity levels for governance alerts.
    /// </summary>
    public enum AlertSeverity
    {
        Info,
        Warning,
        Error,
        Critical
    }

    /// <summary>
    /// Access level
    /// </summary>
    public enum AccessLevel
    {
        /// <summary>
        /// None
        /// </summary>
        None = 0,

        /// <summary>
        /// Read
        /// </summary>
        Read = 1,

        /// <summary>
        /// Write
        /// </summary>
        Write = 2,

        /// <summary>
        /// Full control
        /// </summary>
        FullControl = 3
    }

    /// <summary>
    /// Defines whether a policy (Encryption/Compression) is forced globally or set per-container.
    /// </summary>
    public enum PolicyScope
    {
        /// <summary>
        /// Applied to everything, overrides granular settings
        /// </summary>
        GlobalEnforced,

        /// <summary>
        /// Applied only if the container config requests it
        /// </summary>
        ContainerSpecific,

        /// <summary>
        /// Feature is globally disabled
        /// </summary>
        Disabled
    }

    /// <summary>
    /// Granular ACL
    /// </summary>
    [Flags]
    public enum Permission
    {
        /// <summary>
        /// No permission
        /// </summary>
        None = 0,

        /// <summary>
        /// Read
        /// </summary>
        Read = 1,      // 0001

        /// <summary>
        /// Write
        /// </summary>
        Write = 2,     // 0010

        /// <summary>
        /// Execute
        /// </summary>
        Execute = 4,   // 0100

        /// <summary>
        /// Delete
        /// </summary>
        Delete = 8,    // 1000

        /// <summary>
        /// Full control
        /// </summary>
        FullControl = Read | Write | Execute | Delete
    }

    /// <summary>
    /// Specifies the operational readiness state of a plugin.
    /// </summary>
    /// <remarks>Use this enumeration to determine whether a plugin is fully operational, partially available,
    /// initializing, degraded, or not ready due to initialization failure. The state can be used to control feature
    /// availability or to provide user feedback about plugin status.</remarks>
    public enum PluginReadyState
    {
        NotReady,           // Initialization failed
        Initializing,       // Still warming up (respond later)
        PartiallyReady,     // Some capabilities available, others pending
        Ready,              // Fully operational
        Degraded            // Working but with reduced capabilities
    }
}
