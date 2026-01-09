using DataWarehouse.SDK.Contracts;
using DataWarehouse.Plugins.Orchestration.Raft.Engine;

namespace DataWarehouse.Plugins.Orchestration.Raft.Bootstrapper
{
    [PluginInfo(
        name: "Raft Consensus Orchestration",
        description: "Distributed consensus using Raft algorithm for leader election and replication",
        author: "DataWarehouse Team",
        version: "1.0.0",
        category: PluginCategory.Orchestration
    )]
    public class RaftOrchestrationPlugin
    {
        public static RaftOrchestrationEngine CreateInstance() => new RaftOrchestrationEngine();
    }
}
