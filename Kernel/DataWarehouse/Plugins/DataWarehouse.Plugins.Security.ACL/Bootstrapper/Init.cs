using DataWarehouse.Plugins.Security.ACL.Engine;
using DataWarehouse.SDK.Contracts;
using DataWarehouse.SDK.Security;

namespace DataWarehouse.Plugins.Security.ACL.Bootstrapper
{
    /// <summary>
    /// Enhanced ACL Security Plugin with hierarchical permissions, wildcards, and deny rules.
    /// Combines features from both basic ACL and granular security for production-grade access control.
    /// </summary>
    public class ACLSecurityPlugin : IFeaturePlugin, IAccessControl
    {
        private ACLSecurityEngine? _engine;
        private IKernelContext? _context;

        /// <summary>
        /// Handshake protocol handler - initializes plugin and returns capabilities.
        /// </summary>
        public async Task<HandshakeResponse> OnHandshakeAsync(HandshakeRequest request)
        {
            _context = request as IKernelContext;

            // Initialize engine
            _engine = new ACLSecurityEngine(request.RootPath);

            _context?.LogInfo("[ACL] Enhanced ACL Security Engine initialized with hierarchical permissions, wildcards, and deny rules");

            return new HandshakeResponse
            {
                PluginId = "DataWarehouse.Security.ACL",
                Name = "Enhanced ACL Security Provider",
                Version = "3.0.0",
                ProtocolVersion = request.ProtocolVersion,
                State = PluginState.Ready,
                Capabilities = new List<string>
                {
                    "access-control",
                    "hierarchical-permissions",
                    "wildcard-users",
                    "deny-rules",
                    "persistent-storage",
                    "role-based-access",
                    "resource-isolation"
                },
                Dependencies = new List<string>(),
                SemanticDescription = "Production-grade access control with hierarchical path permissions, wildcard user support, " +
                                     "explicit deny rules, and persistent storage. Supports fine-grained resource-level permissions " +
                                     "with parent path inheritance and deny-trumps-allow logic for maximum security.",
                SemanticTags = new List<string>
                {
                    "security", "acl", "access-control", "permissions",
                    "authorization", "rbac", "hierarchical", "deny-rules"
                },
                PerformanceProfile = new PerformanceProfile
                {
                    Category = "Security",
                    Latency = "< 5ms (permission check)",
                    Throughput = "10,000+ checks/sec",
                    MemoryFootprint = "Low (persistent to disk)",
                    CpuUsage = "Very Low"
                },
                ConfigurationSchema = new Dictionary<string, string>
                {
                    ["DW_ACL_STORAGE_PATH"] = "Path to ACL database file (default: {RootPath}/Security/acl.db)"
                },
                UsageExamples = new List<PluginUsageExample>
                {
                    new() {
                        Title = "Hierarchical Path Permissions",
                        Description = "Grant permissions on parent path, automatically applies to children",
                        Code = @"
// Grant read access to entire 'users/damien' subtree
_acl.SetPermissions(""users/damien"", ""damien"", Permission.Read, Permission.None);

// Check access to nested file - automatically checks parent paths
bool canRead = _acl.HasAccess(""users/damien/docs/resume.pdf"", ""damien"", Permission.Read);
// Returns: true (inherited from parent path)"
                    },
                    new() {
                        Title = "Wildcard User Permissions",
                        Description = "Use '*' to grant permissions to all users",
                        Code = @"
// Grant read access to all users
_acl.SetPermissions(""public/announcements"", ""*"", Permission.Read, Permission.None);

// Any user can read
bool canRead = _acl.HasAccess(""public/announcements/notice.txt"", ""alice"", Permission.Read);
// Returns: true (matches wildcard)"
                    },
                    new() {
                        Title = "Deny Rules (Deny Trumps Allow)",
                        Description = "Explicit deny overrides any allow permissions",
                        Code = @"
// Allow write, but deny delete
_acl.SetPermissions(""users/damien/docs"", ""damien"", Permission.Write, Permission.Delete);

// Can write
bool canWrite = _acl.HasAccess(""users/damien/docs/file.txt"", ""damien"", Permission.Write);
// Returns: true

// Cannot delete (deny trumps allow)
bool canDelete = _acl.HasAccess(""users/damien/docs/file.txt"", ""damien"", Permission.Delete);
// Returns: false (explicit deny)"
                    },
                    new() {
                        Title = "Multi-Tenant Isolation",
                        Description = "Create isolated scopes for different tenants/projects",
                        Code = @"
// Create isolated scope for ProjectA
_acl.CreateScope(""projects/project-a"", ""user-alice"");
_acl.SetPermissions(""projects/project-a"", ""user-alice"", Permission.FullControl, Permission.None);
_acl.SetPermissions(""projects/project-a"", ""user-bob"", Permission.Read, Permission.None);

// Create isolated scope for ProjectB
_acl.CreateScope(""projects/project-b"", ""user-charlie"");
_acl.SetPermissions(""projects/project-b"", ""user-charlie"", Permission.FullControl, Permission.None);

// user-bob cannot access ProjectB
bool canAccess = _acl.HasAccess(""projects/project-b/data.txt"", ""user-bob"", Permission.Read);
// Returns: false (no permissions granted)"
                    }
                },
                HealthStatus = new HealthStatus
                {
                    Status = "Healthy",
                    LastCheck = DateTime.UtcNow,
                    Details = new Dictionary<string, string>
                    {
                        ["engine"] = "initialized",
                        ["storage"] = "persistent"
                    }
                }
            };
        }

        /// <summary>
        /// Message handler for runtime communication.
        /// </summary>
        public Task OnMessageAsync(PluginMessage message)
        {
            // Handle health checks, configuration updates, etc.
            return Task.CompletedTask;
        }

        /// <summary>
        /// Start the ACL security feature.
        /// </summary>
        public Task StartAsync(CancellationToken ct)
        {
            _context?.LogInfo("[ACL] ACL Security feature started");
            return Task.CompletedTask;
        }

        /// <summary>
        /// Stop the ACL security feature.
        /// </summary>
        public Task StopAsync()
        {
            _context?.LogInfo("[ACL] ACL Security feature stopped");
            return Task.CompletedTask;
        }

        // ==================== IAccessControl Implementation ====================

        /// <summary>
        /// Set permissions for a user on a resource.
        /// </summary>
        public void SetPermissions(string resource, string subject, Permission allow, Permission deny)
        {
            _engine?.SetPermissions(resource, subject, allow, deny);
        }

        /// <summary>
        /// Create an ACL scope with an owner.
        /// </summary>
        public void CreateScope(string resource, string owner)
        {
            _engine?.CreateScope(resource, owner);
        }

        /// <summary>
        /// Check if a user has the requested permission on a resource.
        /// Uses hierarchical path checking and deny-trumps-allow logic.
        /// </summary>
        public bool HasAccess(string resource, string subject, Permission requested)
        {
            return _engine?.HasAccess(resource, subject, requested) ?? false;
        }
    }
}
