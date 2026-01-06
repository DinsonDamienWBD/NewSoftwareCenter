using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using DataWarehouse.SDK.Security;

namespace DataWarehouse.SDK.Contracts.CategoryBases
{
    /// <summary>
    /// Base class for Security/ACL plugins.
    /// Handles access control, permissions, authentication, key management.
    /// Plugins implement backend-specific security logic.
    /// </summary>
    public abstract class SecurityProviderBase : PluginBase
    {
        /// <summary>Constructs security provider</summary>
        protected SecurityProviderBase(string id, string name, Version version)
            : base(id, name, version, PluginCategory.Security)
        {
        }

        // Abstract members - plugin implements
        /// <summary>Security type (e.g., "acl", "rbac", "oauth")</summary>
        protected abstract string SecurityType { get; }
        /// <summary>Initialize security backend</summary>
        protected abstract Task InitializeSecurityAsync(IKernelContext context);
        /// <summary>Check if user has permission</summary>
        protected abstract Task<bool> CheckPermissionInternalAsync(string userId, string resource, Permission permission);
        /// <summary>Grant permission</summary>
        protected abstract Task GrantPermissionInternalAsync(string userId, string resource, Permission permission);
        /// <summary>Revoke permission</summary>
        protected abstract Task RevokePermissionInternalAsync(string userId, string resource, Permission permission);
        /// <summary>Get user's permissions for resource</summary>
        protected abstract Task<List<Permission>> GetUserPermissionsInternalAsync(string userId, string resource);

        // Virtual members
        /// <summary>Custom security initialization</summary>
        protected virtual void InitializeSecurity(IKernelContext context) { }

        // Capabilities
        /// <summary>Declares security capabilities</summary>
        protected override PluginCapabilityDescriptor[] Capabilities => new[]
        {
            new PluginCapabilityDescriptor
            {
                CapabilityId = $"security.{SecurityType}.check",
                DisplayName = "Check Permission",
                Description = $"Check user permissions using {SecurityType}",
                Category = CapabilityCategory.Security,
                RequiredPermission = Permission.Read,
                Tags = new List<string> { "security", "check", SecurityType }
            },
            new PluginCapabilityDescriptor
            {
                CapabilityId = $"security.{SecurityType}.grant",
                DisplayName = "Grant Permission",
                Description = $"Grant permissions using {SecurityType}",
                Category = CapabilityCategory.Security,
                RequiredPermission = Permission.FullControl,
                RequiresApproval = true,
                Tags = new List<string> { "security", "grant", SecurityType }
            },
            new PluginCapabilityDescriptor
            {
                CapabilityId = $"security.{SecurityType}.revoke",
                DisplayName = "Revoke Permission",
                Description = $"Revoke permissions using {SecurityType}",
                Category = CapabilityCategory.Security,
                RequiredPermission = Permission.FullControl,
                RequiresApproval = true,
                Tags = new List<string> { "security", "revoke", SecurityType }
            }
        };

        // Initialization
        /// <summary>Initializes and registers handlers</summary>
        protected override void InitializeInternal(IKernelContext context)
        {
            InitializeSecurityAsync(context).GetAwaiter().GetResult();

            RegisterCapability($"security.{SecurityType}.check", async (parameters) =>
            {
                var userId = (string)parameters["userId"];
                var resource = (string)parameters["resource"];
                var permission = (Permission)parameters["permission"];
                var hasPermission = await CheckPermissionInternalAsync(userId, resource, permission);
                return new { hasPermission, userId, resource, permission = permission.ToString() };
            });

            RegisterCapability($"security.{SecurityType}.grant", async (parameters) =>
            {
                var userId = (string)parameters["userId"];
                var resource = (string)parameters["resource"];
                var permission = (Permission)parameters["permission"];
                await GrantPermissionInternalAsync(userId, resource, permission);
                return new { success = true, userId, resource, permission = permission.ToString() };
            });

            RegisterCapability($"security.{SecurityType}.revoke", async (parameters) =>
            {
                var userId = (string)parameters["userId"];
                var resource = (string)parameters["resource"];
                var permission = (Permission)parameters["permission"];
                await RevokePermissionInternalAsync(userId, resource, permission);
                return new { success = true, userId, resource, permission = permission.ToString() };
            });

            InitializeSecurity(context);
        }
    }
}
