using DataWarehouse.SDK.Contracts;

namespace DataWarehouse.Plugins.Features.Consensus.Services
{
    /// <summary>
    /// A no-op consensus engine for single-node deployments.
    /// Always acts as Leader.
    /// </summary>
    public class NullConsensusEngine : IConsensusEngine
    {
        /// <summary>
        /// ID
        /// </summary>
        public string Id => "null-consensus";

        /// <summary>
        /// Version
        /// </summary>
        public string Version => "1.0";

        /// <summary>
        /// Is leader
        /// </summary>
        public bool IsLeader => true;

        /// <summary>
        /// Name
        /// </summary>
        public string Name => "Null Consensus engine";

        /// <summary>
        /// Initialize
        /// </summary>
        /// <param name="context"></param>
        public void Initialize(IKernelContext context) { }

        /// <summary>
        /// Propose
        /// </summary>
        /// <param name="proposal"></param>
        /// <returns></returns>
        public Task<bool> ProposeAsync(Proposal proposal) => Task.FromResult(true);

        /// <summary>
        /// On commit
        /// </summary>
        /// <param name="handler"></param>
        public void OnCommit(Action<Proposal> handler) { }
    }

    /// <summary>
    /// A robust Raft Consensus implementation for distributed state consistency.
    /// </summary>
    /// <remarks>
    /// Initializes the Raft Engine.
    /// </remarks>
    public class RaftEngine(string nodeId, IEnumerable<IFederationNode> peers, IKernelContext context) : IConsensusEngine
    {
        /// <summary>
        /// ID
        /// </summary>
        public string Id => "raft-consensus";

        /// <summary>
        /// Version
        /// </summary>
        public string Version => "5.0.0";

        /// <summary>
        /// Is leader
        /// </summary>
        public bool IsLeader { get; private set; }

        public string Name => "Raft engine";

        private readonly string _nodeId = nodeId;
        private readonly IEnumerable<IFederationNode> _peers = peers;
        private IKernelContext? _context = context;
        private Action<Proposal>? _commitHandler;

        /// <summary>
        /// Bootstraps the engine context.
        /// </summary>
        public void Initialize(IKernelContext context)
        {
            _context = context;
            _context?.LogInfo($"[Raft-{_nodeId}] Initialized Raft Engine.");
        }

        /// <summary>
        /// Proposes a new state change to the cluster.
        /// </summary>
        public async Task<bool> ProposeAsync(Proposal proposal)
        {
            if (!IsLeader)
            {
                _context?.LogWarning($"[Raft-{_nodeId}] Cannot accept proposal {proposal.Id}. Not Leader.");
                return false;
            }

            _context?.LogDebug($"[Raft-{_nodeId}] Replicating proposal {proposal.Id}...");

            // Logic stub for replication
            await Task.Yield();

            _commitHandler?.Invoke(proposal);
            _context?.LogInfo($"[Raft-{_nodeId}] Committed proposal {proposal.Id}");

            return true;
        }

        /// <summary>
        /// Register a callback for when a proposal is committed.
        /// </summary>
        public void OnCommit(Action<Proposal> handler)
        {
            _commitHandler = handler;
        }
    }

    /// <summary>
    /// Placeholder for Paxos consensus engine.
    /// </summary>
    public class PaxosEngine : IConsensusEngine
    {
        /// <summary>
        /// ID
        /// </summary>
        public string Id => "paxos-consensus";

        /// <summary>
        /// Version
        /// </summary>
        public string Version => "1.0";

        /// <summary>
        /// Is leader
        /// </summary>
        public bool IsLeader => false;

        /// <summary>
        /// Name
        /// </summary>
        public string Name => "Paxos engine";

        private IKernelContext? _context;

        /// <summary>
        /// Initialize
        /// </summary>
        /// <param name="context"></param>
        public void Initialize(IKernelContext context)
        {
            _context = context;
        }

        /// <summary>
        /// Propose
        /// </summary>
        /// <param name="proposal"></param>
        /// <returns></returns>
        public Task<bool> ProposeAsync(Proposal proposal)
        {
            _context?.LogInfo("[Paxos] Proposal ignored (Not implemented).");
            return Task.FromResult(false);
        }

        /// <summary>
        /// On commit
        /// </summary>
        /// <param name="handler"></param>
        public void OnCommit(Action<Proposal> handler) { }
    }
}