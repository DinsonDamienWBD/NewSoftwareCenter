using DataWarehouse.SDK.Security;
using DataWarehouse.SDK.Utilities;

namespace DataWarehouse.Plugins.Security.Granular.Engine
{
    public class AclEngine
    {
        // Persistent Store: ResourcePath -> Dictionary<User, Entry>
        private readonly DurableState<Dictionary<string, AclEntry>> _store;

        public AclEngine(string rootPath)
        {
            var dbPath = Path.Combine(rootPath, "Security", "acl_v2.db");
            _store = new DurableState<Dictionary<string, AclEntry>>(dbPath);
        }

        public void CreateScope(string resource, string owner)
        {
            SetPermissions(resource, owner, Permission.FullControl, Permission.None);
        }

        public void SetPermissions(string resource, string subject, Permission allow, Permission deny)
        {
            if (!_store.TryGet(resource, out Dictionary<string, AclEntry>? entries))
            {
                entries = [];
            }

            entries[subject] = new AclEntry { Allow = allow, Deny = deny };
            _store.Set(resource, entries);
        }

        public bool HasAccess(string resource, string subject, Permission requested)
        {
            // 1. Path Expansion (e.g., "users/damien/docs/resume.pdf")
            // Checks: "users", "users/damien", "users/damien/docs", "users/damien/docs/resume.pdf"

            var parts = resource.Split('/');
            string currentPath = "";

            Permission effectiveAllow = Permission.None;
            Permission effectiveDeny = Permission.None;

            foreach (var part in parts)
            {
                currentPath = string.IsNullOrEmpty(currentPath) ? part : $"{currentPath}/{part}";

                if (_store.TryGet(currentPath, out var entries))
                {
                    // User Rule
                    if (entries.TryGetValue(subject, out AclEntry? userRule))
                    {
                        effectiveAllow |= userRule.Allow;
                        effectiveDeny |= userRule.Deny;
                    }
                    // Wildcard Rule
                    if (entries.TryGetValue("*", out var allRule))
                    {
                        effectiveAllow |= allRule.Allow;
                        effectiveDeny |= allRule.Deny;
                    }
                }
            }

            // 2. The Golden Rule: Deny trumps EVERYTHING
            if ((effectiveDeny & requested) != 0) return false;

            // 3. Allow must match requested
            return (effectiveAllow & requested) == requested;
        }

        public class AclEntry
        {
            public Permission Allow { get; set; }
            public Permission Deny { get; set; }
        }
    }
}