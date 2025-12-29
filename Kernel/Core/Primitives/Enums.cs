namespace Core.Primitives
{
    /// <summary>
    /// Defines the visibility and access rights for a resource or module.
    /// </summary>
    public enum AccessLevel
    {
        /// <summary>Visible to all users and modules.</summary>
        Public = 0,

        /// <summary>Visible only to the owner and explicitly authorized guests.</summary>
        Protected = 1,

        /// <summary>Visible only to the owner module.</summary>
        Private = 2,

        /// <summary>Requires re-authentication (e.g., password/MFA) to access.</summary>
        Sealed = 3
    }

    /// <summary>
    /// Defines the operational priority of a service or message.
    /// </summary>
    public enum Priority
    {
        /// <summary>
        /// Low priority. Non-urgent tasks that can be deferred.
        /// </summary>
        Low = 0,

        /// <summary>
        /// Normal priority. Standard operational tasks.
        /// </summary>
        Normal = 1,

        /// <summary>
        /// High priority. Urgent tasks that require immediate attention.
        /// </summary>
        High = 2,
        /// <summary>System critical. Failures here may trigger emergency shutdowns.</summary>
        Critical = 3
    }

    /// <summary>
    /// Represents the current lifecycle state of a module.
    /// </summary>
    public enum ModuleStatus
    {
        /// <summary>
        /// System is initializing the module.
        /// </summary>
        Starting,

        /// <summary>
        /// Module is fully operational.
        /// </summary>
        Healthy,

        /// <summary>
        /// Module is experiencing issues but remains operational.
        /// </summary>
        Degraded,
        /// <summary>The module has been isolated due to repeated failures.</summary>
        Quarantined,

        /// <summary>
        /// Module is shutting down.
        /// </summary>
        Stopped
    }

    /// <summary>
    /// Represents the result of a system health check.
    /// </summary>
    public enum HealthStatus
    {
        /// <summary>
        /// System is operating normally.
        /// </summary>
        Healthy,

        /// <summary>
        /// System is experiencing minor issues.
        /// </summary>
        Degraded,

        /// <summary>
        /// System is experiencing major issues.
        /// </summary>
        Unhealthy
    }

    /// <summary>
    /// Flags indicating what a specific Storage Driver can handle natively.
    /// Used by the DataWarehouse to optimize the pipeline (e.g., skipping double encryption).
    /// </summary>
    [Flags]
    public enum StorageCapabilities
    {
        /// <summary>
        /// No special capabilities.
        /// </summary>
        None = 0,

        /// <summary>Driver handles encryption at rest (e.g., S3-SSE).</summary>
        NativeEncryption = 1 << 0,

        /// <summary>Driver enforces ACLs natively (e.g., NTFS permissions).</summary>
        NativeAccessControl = 1 << 1,

        /// <summary>Driver supports file versioning/history.</summary>
        NativeVersioning = 1 << 2,

        /// <summary>
        /// Driver supports batch operations natively.
        /// </summary>
        NativeBatching = 1 << 3
    }

    /// <summary>
    /// Severity of validation
    /// </summary>
    public enum ValidationSeverity
    {
        /// <summary>
        /// Error level
        /// </summary>
        Error,

        /// <summary>
        /// Warning Level
        /// </summary>
        Warning,

        /// <summary>
        /// Information Level
        /// </summary>
        Info
    }

    /// <summary>
    /// Category of a failure to determine whether to retry.
    /// </summary>
    public enum FailureCategory
    {
        /// <summary>
        /// Bad Request, Validation Fail (Do not retry)
        /// </summary>
        Logical,

        /// <summary>
        /// Network Blip, Timeout (Retry)
        /// </summary>
        Transient,

        /// <summary>
        /// Auth Fail (Audit and Alert)
        /// </summary>
        Security,

        /// <summary>
        /// NullRef, Crash (Fix bug)
        /// </summary>
        System,

        /// <summary>
        /// Rate limit
        /// </summary>
        Quota
    }

    /// <summary>
    /// GDPR and Privacy Levels
    /// </summary>
    public enum DataClassification
    {
        /// <summary>
        /// No restriction
        /// </summary>
        Public,

        /// <summary>
        /// Organization only
        /// </summary>
        Internal,

        /// <summary>
        /// PII / Sensitive (Mask in logs)
        /// </summary>
        Confidential,

        /// <summary>
        /// Credentials / Secrets (Encrypt field level)
        /// </summary>
        Restricted
    }

    /// <summary>
    /// Multi-Tenancy Scope
    /// </summary>
    public enum TenantScope
    {
        /// <summary>
        /// Applies to one tenant
        /// </summary>
        TenantSpecific,

        /// <summary>
        /// Applies to the whole system
        /// </summary>
        Global
    }
}