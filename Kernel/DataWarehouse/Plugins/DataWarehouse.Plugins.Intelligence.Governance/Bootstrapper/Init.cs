using System;
using Intelligence.Governance.Engine;
using DataWarehouse.SDK.Contracts;

namespace Intelligence.Governance.Bootstrapper
{
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

        public static GovernanceEngine CreateInstance() => new GovernanceEngine();
    }
}
