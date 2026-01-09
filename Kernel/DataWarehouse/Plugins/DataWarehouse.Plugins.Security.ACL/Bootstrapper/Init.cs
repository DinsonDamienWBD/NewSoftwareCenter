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
            var startTime = DateTime.UtcNow;
            _context = request as IKernelContext;

            // Initialize engine
            _engine = new ACLSecurityEngine(request.RootPath);

            _context?.LogInfo("[ACL] Enhanced ACL Security Engine initialized with hierarchical permissions, wildcards, and deny rules");

            var capabilities = new List<PluginCapabilityDescriptor>
            {
                new() {
                    CapabilityId = "security.acl.hierarchical",
                    DisplayName = "Hierarchical Path Permissions",
                    Description = "Grant permissions on parent paths that automatically apply to children. " +
                                 "Supports fine-grained resource-level permissions with parent path inheritance.",
                    Category = CapabilityCategory.Security,
                    RequiredPermission = Permission.FullControl,
                    Tags = ["security", "acl", "hierarchical", "permissions"]
                },
                new() {
                    CapabilityId = "security.acl.wildcards",
                    DisplayName = "Wildcard User Permissions",
                    Description = "Use '*' wildcard to grant permissions to all users on specific resources.",
                    Category = CapabilityCategory.Security,
                    RequiredPermission = Permission.FullControl,
                    Tags = ["security", "acl", "wildcards"]
                },
                new() {
                    CapabilityId = "security.acl.deny",
                    DisplayName = "Explicit Deny Rules",
                    Description = "Deny-trumps-allow logic for maximum security. Explicit deny overrides any allow permissions.",
                    Category = CapabilityCategory.Security,
                    RequiredPermission = Permission.FullControl,
                    Tags = ["security", "acl", "deny-rules"]
                },
                new() {
                    CapabilityId = "security.acl.isolation",
                    DisplayName = "Multi-Tenant Resource Isolation",
                    Description = "Create isolated scopes for different tenants/projects with separate permission boundaries.",
                    Category = CapabilityCategory.Security,
                    RequiredPermission = Permission.FullControl,
                    Tags = ["security", "acl", "multi-tenant", "isolation"]
                }
            };

            return HandshakeResponse.Success(
                pluginId: "DataWarehouse.Security.ACL",
                name: "Enhanced ACL Security Provider",
                version: new Version("3.0.0"),
                category: PluginCategory.Security,
                capabilities: capabilities,
                initDuration: DateTime.UtcNow - startTime
            );
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
