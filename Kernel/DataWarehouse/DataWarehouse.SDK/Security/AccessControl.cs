using DataWarehouse.SDK.Primitives;

namespace DataWarehouse.SDK.Security
{
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
        public Dictionary<string, AccessLevel> AccessControlList { get; set; } = new();
    }
}