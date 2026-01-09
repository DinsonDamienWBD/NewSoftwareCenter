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
        public static GovernanceEngine CreateInstance() => new();
    }
}
