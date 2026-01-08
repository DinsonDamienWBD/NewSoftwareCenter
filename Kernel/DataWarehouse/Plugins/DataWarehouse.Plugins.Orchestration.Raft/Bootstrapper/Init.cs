using DataWarehouse.SDK.Contracts;
using DataWarehouse.Plugins.Orchestration.Raft.Engine;

namespace DataWarehouse.Plugins.Orchestration.Raft.Bootstrapper
{
    public class RaftOrchestrationPlugin
    {
        public static PluginInfo PluginInfo => new()
        {
            Id = "orchestration.raft",
            Name = "Raft Consensus Orchestration",
            Version = new Version(1, 0, 0),
            Author = "DataWarehouse SDK",
            Description = "Distributed consensus using Raft algorithm for leader election and replication",
            Category = PluginCategory.Orchestration,
            Tags = new[] { "orchestration", "raft", "consensus", "distributed", "replication" }
        };

        public static RaftOrchestrationEngine CreateInstance() => new RaftOrchestrationEngine();
    }
}
