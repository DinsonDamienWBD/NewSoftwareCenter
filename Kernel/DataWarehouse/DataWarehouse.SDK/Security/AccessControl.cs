namespace DataWarehouse.SDK.Security
{
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
    /// Container configuration
    /// </summary>
    public class ContainerConfig
    {
        /// <summary>
        /// Container ID
        /// </summary>
        public string ContainerId { get; set; } = "default";

        /// <summary>
        /// Is encrypted
        /// </summary>
        public bool IsEncrypted { get; set; } = false;

        /// <summary>
        /// Is compressed
        /// </summary>
        public bool IsCompressed { get; set; } = false;

        /// <summary>
        /// Granular ACLs: UserId -> Level
        /// </summary>
        public Dictionary<string, AccessLevel> AccessControlList { get; set; } = [];
    }
}