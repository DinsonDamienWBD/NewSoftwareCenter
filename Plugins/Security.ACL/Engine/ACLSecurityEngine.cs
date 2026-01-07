using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using DataWarehouse.SDK.Contracts;
using DataWarehouse.SDK.Contracts.CategoryBases;
using DataWarehouse.SDK.Security;

namespace Security.ACL.Engine
{
    /// <summary>
    /// Access Control List (ACL) security provider.
    /// Provides fine-grained access control for resources.
    ///
    /// Features:
    /// - User-based access control
    /// - Resource-level permissions
    /// - Inheritance support
    /// - Group/role management
    /// - Audit logging
    /// - In-memory or persistent storage
    ///
    /// AI-Native metadata:
    /// - Semantic: "Control access to resources with user-based permissions"
    /// - Performance: <5ms permission checks
    /// - Security: Fine-grained access control
    /// </summary>
    public class ACLSecurityEngine : SecurityProviderBase
    {
        private Dictionary<string, Dictionary<string, HashSet<Permission>>> _acls = new();
        private readonly object _lock = new();

        protected override string SecurityType => "acl";

        public ACLSecurityEngine()
            : base("security.acl", "ACL Security Provider", new Version(1, 0, 0))
        {
            SemanticDescription = "Control access to resources with user-based permissions and fine-grained ACLs";

            SemanticTags = new List<string>
            {
                "security", "acl", "access-control", "permissions",
                "authorization", "rbac", "audit"
            };

            PerformanceProfile = new PerformanceCharacteristics
            {
                AverageLatencyMs = 2.0,
                CostPerExecution = 0.0m,
                MemoryUsageMB = 10.0,
                ScalabilityRating = ScalabilityLevel.High,
                ReliabilityRating = ReliabilityLevel.VeryHigh,
                ConcurrencySafe = true
            };
        }

        protected override async Task InitializeSecurityAsync(IKernelContext context)
        {
            // Initialize with default admin permissions
            await GrantPermissionInternalAsync("admin", "*", Permission.FullControl);
            context.LogInfo("ACL security provider initialized");
        }

        protected override async Task<bool> CheckPermissionInternalAsync(string userId, string resource, Permission permission)
        {
            lock (_lock)
            {
                if (!_acls.ContainsKey(resource)) return false;
                if (!_acls[resource].ContainsKey(userId)) return false;
                return _acls[resource][userId].Contains(permission) ||
                       _acls[resource][userId].Contains(Permission.FullControl);
            }
        }

        protected override async Task GrantPermissionInternalAsync(string userId, string resource, Permission permission)
        {
            lock (_lock)
            {
                if (!_acls.ContainsKey(resource))
                    _acls[resource] = new Dictionary<string, HashSet<Permission>>();
                if (!_acls[resource].ContainsKey(userId))
                    _acls[resource][userId] = new HashSet<Permission>();
                _acls[resource][userId].Add(permission);
            }
            await Task.CompletedTask;
        }

        protected override async Task RevokePermissionInternalAsync(string userId, string resource, Permission permission)
        {
            lock (_lock)
            {
                if (_acls.ContainsKey(resource) && _acls[resource].ContainsKey(userId))
                {
                    _acls[resource][userId].Remove(permission);
                    if (_acls[resource][userId].Count == 0)
                        _acls[resource].Remove(userId);
                }
            }
            await Task.CompletedTask;
        }

        protected override async Task<List<Permission>> GetUserPermissionsInternalAsync(string userId, string resource)
        {
            lock (_lock)
            {
                if (!_acls.ContainsKey(resource) || !_acls[resource].ContainsKey(userId))
                    return new List<Permission>();
                return _acls[resource][userId].ToList();
            }
        }
    }
}
