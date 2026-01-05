namespace DataWarehouse.SDK.Security
{
    /// <summary>
    /// Represents the caller (User, Module, or System Service).
    /// </summary>
    public interface ISecurityContext
    {
        /// <summary>
        /// Name
        /// </summary>
        string UserId { get; }

        /// <summary>
        /// Tenant ID
        /// </summary>
        string? TenantId { get; }

        /// <summary>
        /// Roles
        /// </summary>
        IEnumerable<string> Roles { get; }

        /// <summary>
        /// Is system admin
        /// </summary>
        bool IsSystemAdmin { get; }
    }
}
