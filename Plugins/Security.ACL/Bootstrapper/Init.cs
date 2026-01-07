using System;
using Security.ACL.Engine;
using DataWarehouse.SDK.Contracts;

namespace Security.ACL.Bootstrapper
{
    public class ACLSecurityPlugin
    {
        public static PluginInfo PluginInfo => new()
        {
            Id = "security.acl",
            Name = "ACL Security Provider",
            Version = new Version(1, 0, 0),
            Author = "DataWarehouse SDK",
            Description = "Fine-grained access control with user-based permissions",
            Category = PluginCategory.Security,
            Tags = new[] { "security", "acl", "access-control", "permissions" }
        };

        public static ACLSecurityEngine CreateInstance() => new ACLSecurityEngine();
    }
}
