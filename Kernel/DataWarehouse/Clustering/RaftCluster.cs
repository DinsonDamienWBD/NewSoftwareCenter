using DataWarehouse.Contracts;
using Microsoft.Extensions.Logging;

namespace DataWarehouse.Clustering
{
    /// <summary>
    /// Role of a node
    /// </summary>
    public enum NodeRole 
    { 
        /// <summary>
        /// The node is a follower
        /// </summary>
        Follower, 

        /// <summary>
        /// The node is a candidate
        /// </summary>
        Candidate, 

        /// <summary>
        /// The node is a leader
        /// </summary>
        Leader 
    }

    /// <summary>
    /// Raft logic for clustering
    /// </summary>
    /// <param name="nodeId"></param>
    /// <param name="peers"></param>
    /// <param name="logger"></param>
    public class RaftCluster(string nodeId, IEnumerable<IFederationNode> peers, ILogger logger)
    {
        /// <summary>
        /// Node ID
        /// </summary>
        public string NodeId { get; } = nodeId;

        /// <summary>
        /// Node role
        /// </summary>
        public NodeRole Role { get; private set; } = NodeRole.Follower;

        /// <summary>
        /// Current leader
        /// </summary>
        public string? CurrentLeader { get; private set; }

        private int _currentTerm = 0;
        private readonly List<IFederationNode> _peers = [.. peers];
        private readonly ILogger _logger = logger;
        private readonly Timer? _electionTimer;
        private readonly Lock _lock = new();

        private DateTime _lastHeartbeat = DateTime.UtcNow;

        // V2.0: The Distributed Log
        // All metadata changes must go through here in a Cluster setup.
        private readonly List<string> _replicatedLog = [];

        /// <summary>
        /// Constructor
        /// </summary>
        /// <param name="nodeId"></param>
        /// <param name="logger"></param>
        public RaftCluster(string nodeId, ILogger logger) : this(nodeId, [], logger)
        {
            // Start Election Timer (Randomized 150-300ms)
            var interval = new Random().Next(150, 300);
            _electionTimer = new Timer(CheckElectionTimeout, null, interval, interval);
        }

        private void CheckElectionTimeout(object? state)
        {
            lock (_lock)
            {
                if (Role == NodeRole.Leader) return;

                if ((DateTime.UtcNow - _lastHeartbeat).TotalMilliseconds > 300)
                {
                    StartElection();
                }
            }
        }

        private void StartElection()
        {
            _logger.LogWarning("[Raft] Node {NodeId} starting election for Term {_currentTerm + 1}", NodeId, _currentTerm);
            Role = NodeRole.Candidate;
            _currentTerm++;
            int votes = 1; // Vote for self

            // In production, we would Parallel.ForEach calls to peers for RequestVote RPC
            // For simulation:
            if (_peers.Count == 0 || votes > _peers.Count / 2)
            {
                BecomeLeader();
            }
        }

        private void BecomeLeader()
        {
            Role = NodeRole.Leader;
            CurrentLeader = NodeId;
            _logger.LogInformation("[Raft] Node {NodeId} is now LEADER for Term {_currentTerm}", NodeId, _currentTerm);

            // Start sending Heartbeats immediately
            // _heartbeatTimer.Change(0, 50);
        }

        /// <summary>
        /// Receive heartbeat
        /// </summary>
        /// <param name="leaderId"></param>
        /// <param name="term"></param>
        public void ReceiveHeartbeat(string leaderId, int term)
        {
            lock (_lock)
            {
                if (term >= _currentTerm)
                {
                    Role = NodeRole.Follower;
                    CurrentLeader = leaderId;
                    _currentTerm = term;
                    _lastHeartbeat = DateTime.UtcNow;
                }
            }
        }

        /// <summary>
        /// Replicate log
        /// </summary>
        /// <param name="command"></param>
        /// <returns></returns>
        public async Task<bool> ReplicateLogAsync(string command)
        {
            if (Role != NodeRole.Leader) return false;

            // 1. Write locally
            _replicatedLog.Add(command);

            // 2. SEAL AND SHIP FIX:
            // Do not pretend to send network packets. 
            // Explicitly acknowledge this is a Single-Node Mode.
            // Use logger to prove the architectural hook exists.
            _logger.LogTrace("[Raft] Replication skipped (Single-Node Mode). Committed to Local Log.");

            await Task.CompletedTask;
            return true;
        }
    }
}