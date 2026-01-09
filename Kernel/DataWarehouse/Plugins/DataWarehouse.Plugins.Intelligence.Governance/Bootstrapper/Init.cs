using DataWarehouse.SDK.Contracts;
using DataWarehouse.Plugins.Intelligence.Governance.Engine;

namespace DataWarehouse.Plugins.Intelligence.Governance.Bootstrapper
{
    [PluginInfo(
        name: "Governance & Compliance Engine",
        description: "AI-driven governance, policy enforcement, and compliance monitoring",
        author: "DataWarehouse Team",
        version: "1.0.0",
        category: PluginCategory.Intelligence
    )]
    public class GovernancePlugin
    {
        public static PluginInfo PluginInfo => new()
        {
            Id = "intelligence.governance",
            Name = "Governance & Compliance Engine",
            Version = new Version(1, 0, 0),
            Author = "DataWarehouse SDK",
            Description = "AI-driven governance, policy enforcement, and compliance monitoring",
            Category = PluginCategory.Intelligence,
            Tags = new[] { "intelligence", "governance", "compliance", "policy", "audit" }
        };

        public static GovernanceEngine CreateInstance() => new();
    }
}
