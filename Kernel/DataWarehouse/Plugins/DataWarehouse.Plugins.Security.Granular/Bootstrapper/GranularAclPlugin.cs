using DataWarehouse.Plugins.Security.Granular.Engine;
using DataWarehouse.SDK.Contracts;
using DataWarehouse.SDK.Security;

namespace DataWarehouse.Plugins.Security.Granular.Bootstrapper
{
    public class GranularAclPlugin : IFeaturePlugin, IAccessControl
    {
        public string Id => "granular-acl";
        public string Name => "Granular Access Control List";
        public string Version => "2.0.0";

        private AclEngine? _engine;

        public void Initialize(IKernelContext context)
        {
            // Initialize Engine
            _engine = new AclEngine(context.RootPath);
            context.LogInfo($"[{Id}] ACL Engine V2 Loaded.");
        }

        public Task StartAsync(CancellationToken ct) => Task.CompletedTask;
        public Task StopAsync() => Task.CompletedTask;

        // --- IAccessControl Forwarding ---

        public void SetPermissions(string resource, string subject, Permission allow, Permission deny)
            => _engine!.SetPermissions(resource, subject, allow, deny);

        public void CreateScope(string resource, string owner)
            => _engine!.CreateScope(resource, owner);

        public bool HasAccess(string resource, string subject, Permission requested)
            => _engine!.HasAccess(resource, subject, requested);
    }
}