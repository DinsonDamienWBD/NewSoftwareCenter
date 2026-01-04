using DataWarehouse.SDK.Contracts;

namespace DataWarehouse.SDK.Security
{
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
    /// Defines the contract for an Authorization Engine.
    /// </summary>
    public interface IAccessControl : IPlugin
    {
        /// <summary>
        /// Registers a permission rule.
        /// </summary>
        /// <param name="resource">The partition, container, or blob path.</param>
        /// <param name="subject">The User ID or Role.</param>
        /// <param name="allow">Permissions to explicitly Allow.</param>
        /// <param name="deny">Permissions to explicitly Deny (Overrides Allow).</param>
        void SetPermissions(string resource, string subject, Permission allow, Permission deny);

        /// <summary>
        /// Checks if the subject has the requested permissions.
        /// </summary>
        bool HasAccess(string resource, string subject, Permission requested);

        /// <summary>
        /// Creates a protected container/partition scope.
        /// </summary>
        void CreateScope(string resource, string owner);
    }
}