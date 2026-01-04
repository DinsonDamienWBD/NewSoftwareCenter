using DataWarehouse.SDK.Security;
using System.Collections.Concurrent;

namespace DataWarehouse.Kernel.Security
{
    /// <summary>
    /// Access control manager
    /// </summary>
    public class AccessControlManager
    {
        /// <summary>
        /// In-Memory ACL Store (In V5, this would persist to disk via MetadataIndex)
        /// ContainerID -> Config
        /// </summary>
        private readonly ConcurrentDictionary<string, ContainerConfig> _policies = new();

        /// <summary>
        /// Constructor
        /// </summary>
        public AccessControlManager()
        {
            // Default "Public" container setup
            var defaultConfig = new ContainerConfig
            {
                ContainerId = "default",
                IsEncrypted = false,
                IsCompressed = false
            };
            // Default: Everyone has Read access to 'default'
            defaultConfig.AccessControlList["Role:Everyone"] = AccessLevel.Read;
            _policies.TryAdd("default", defaultConfig);
        }

        /// <summary>
        /// Create a container
        /// </summary>
        /// <param name="containerId"></param>
        /// <param name="ownerId"></param>
        /// <param name="encrypt"></param>
        /// <param name="compress"></param>
        /// <exception cref="InvalidOperationException"></exception>
        public void CreateContainer(string containerId, string ownerId, bool encrypt, bool compress)
        {
            var config = new ContainerConfig
            {
                ContainerId = containerId,
                IsEncrypted = encrypt,
                IsCompressed = compress
            };

            // The creator gets Full Control
            config.AccessControlList[ownerId] = AccessLevel.FullControl;

            if (!_policies.TryAdd(containerId, config))
            {
                throw new InvalidOperationException($"Container {containerId} already exists.");
            }
        }

        /// <summary>
        /// Check access
        /// </summary>
        /// <param name="context"></param>
        /// <param name="containerId"></param>
        /// <param name="requiredLevel"></param>
        /// <exception cref="FileNotFoundException"></exception>
        /// <exception cref="UnauthorizedAccessException"></exception>
        public void VerifyAccess(ISecurityContext context, string containerId, AccessLevel requiredLevel)
        {
            // 1. God Mode Override
            if (context.IsSystemAdmin) return;

            // 2. Container Existence Check
            if (!_policies.TryGetValue(containerId, out var config))
            {
                // Security by Default: Obscure existence of private containers
                throw new FileNotFoundException($"Container {containerId} not found.");
            }

            AccessLevel effectivePermissions = AccessLevel.None;

            // 3. User-Specific Check (Highest Priority for Deny)
            if (config.AccessControlList.TryGetValue(context.UserId, out var userLevel))
            {
                // Explicit Ban: If user is specifically set to None, block immediately regardless of roles.
                if (userLevel == AccessLevel.None)
                {
                    throw new UnauthorizedAccessException(
                        $"Access Denied: User '{context.UserId}' is explicitly banned from '{containerId}'.");
                }
                effectivePermissions = userLevel;
            }

            // Optimization: If user settings alone are sufficient, skip role iteration
            if (effectivePermissions >= requiredLevel) return;

            // 4. Role-Based Resolution (Additive / Union)
            foreach (var role in context.Roles)
            {
                // Check Role Entry (Key format: "Role:Name")
                if (config.AccessControlList.TryGetValue($"Role:{role}", out var roleLevel))
                {
                    // Accumulate higher permissions
                    if (roleLevel > effectivePermissions)
                    {
                        effectivePermissions = roleLevel;
                    }

                    // Optimization: Short-circuit if we found enough permission
                    if (effectivePermissions >= requiredLevel) return;
                }
            }

            // 5. 'Everyone' / Public Fallback
            if (config.AccessControlList.TryGetValue("Role:Everyone", out var publicLevel))
            {
                if (publicLevel > effectivePermissions)
                    effectivePermissions = publicLevel;
            }

            // 6. Final Verdict
            if (effectivePermissions >= requiredLevel) return;

            throw new UnauthorizedAccessException(
                $"Access Denied: User '{context.UserId}' has insufficient privileges on '{containerId}'. " +
                $"Effective: {effectivePermissions}, Required: {requiredLevel}.");
        }

        /// <summary>
        /// Grant access
        /// </summary>
        /// <param name="containerId"></param>
        /// <param name="targetUser"></param>
        /// <param name="level"></param>
        public void GrantAccess(string containerId, string targetUser, AccessLevel level)
        {
            if (_policies.TryGetValue(containerId, out var config))
            {
                config.AccessControlList[targetUser] = level;
            }
        }

        /// <summary>
        /// Get a container's policy
        /// </summary>
        /// <param name="containerId"></param>
        /// <returns></returns>
        public ContainerConfig GetContainerPolicy(string containerId)
        {
            return _policies.TryGetValue(containerId, out var p) ? p : new ContainerConfig();
        }
    }
}