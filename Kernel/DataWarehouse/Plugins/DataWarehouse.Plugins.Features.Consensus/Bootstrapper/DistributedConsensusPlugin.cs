using DataWarehouse.SDK.Contracts;
using DataWarehouse.Plugins.Features.Consensus.Services;

namespace DataWarehouse.Plugins.Features.Consensus.Bootstrapper
{
    /// <summary>
    /// Wraps the RaftEngine and exposes it as the Consensus Provider.
    /// </summary>
    public class DistributedConsensusPlugin : IConsensusEngine
    {
        /// <summary>
        /// ID
        /// </summary>
        public string Id => "DataWarehouse.Consensus.Raft";

        /// <summary>
        /// Version
        /// </summary>
        public string Version => "4.1.0";

        /// <summary>
        /// Name
        /// </summary>
        public string Name => "Distributed Consensus";

        /// <summary>
        /// Is Leader
        /// </summary>
        public bool IsLeader => _raftEngine?.IsLeader ?? false;

        private RaftEngine? _raftEngine;
        private FederationManager? _federation;
        private IKernelContext? _context;

        /// <summary>
        /// Initialize
        /// </summary>
        /// <param name="context"></param>
        public void Initialize(IKernelContext context)
        {
            _context = context;
            context.LogInfo($"[{Id}] Initializing Raft Consensus...");

            // 1. Initialize Federation (Peer Discovery)
            // Reads 'peers.json' or similar from root path
            _federation = new FederationManager(context.RootPath);
            var peers = _federation.GetPeers();

            // 2. Initialize Raft Engine
            // Assuming the context provides a way to get the local Node ID, or we generate one
            string nodeId = string.Concat("Node-", Guid.NewGuid().ToString("N").AsSpan(0, 6));

            _raftEngine = new RaftEngine(nodeId, peers, context);
        }

        /// <summary>
        /// Propose
        /// </summary>
        /// <param name="proposal"></param>
        /// <returns></returns>
        /// <exception cref="InvalidOperationException"></exception>
        public async Task<bool> ProposeAsync(SDK.Contracts.Proposal proposal)
        {
            if (_raftEngine == null)
                throw new InvalidOperationException("Raft engine not initialized.");

            return await _raftEngine.ProposeAsync(proposal);
        }

        /// <summary>
        /// On commit
        /// </summary>
        /// <param name="handler"></param>
        /// <exception cref="InvalidOperationException"></exception>
        public void OnCommit(Action<SDK.Contracts.Proposal> handler)
        {
            if (_raftEngine == null)
                throw new InvalidOperationException("Raft engine not initialized.");

            _raftEngine.OnCommit(handler);
        }
    }
}