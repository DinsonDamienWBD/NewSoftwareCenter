using DataWarehouse.Plugins.Features.Consensus.Protos;
using DataWarehouse.Plugins.Features.Consensus.Services;
using DataWarehouse.SDK.Contracts;
using DataWarehouse.SDK.Utilities;
using Grpc.Net.Client;
using System.Collections.Concurrent;
using System.Text.Json;

namespace DataWarehouse.Plugins.Features.Consensus.Engine
{
    /// <summary>
    /// PRODUCTION-GRADE Raft Consensus Engine.
    /// Implements Leader Election, Log Replication, Log Consistency checks, and Safety Commit.
    /// </summary>
    public class RaftEngine : IConsensusEngine, IDisposable
    {
        public string Id => "raft-consensus-engine";
        public string Name => "Raft Engine V6";
        public string Version => "6.0.0";

        private readonly string _nodeId;
        private readonly FederationManager _federation;
        private readonly IKernelContext _context;
        private readonly RaftLog _log;

        // Persistent State (Term & VotedFor)
        private readonly DurableState<RaftMetadata> _metaStore;
        private readonly RaftMetadata _persistentState;

        // Volatile State
        private RaftState _currentState = RaftState.Follower;
        private readonly long _currentTerm = 0;
        private long _commitIndex = 0;
        private long _lastApplied = 0;
        private string? _leaderId = null;

        // Leader Volatile State
        private readonly ConcurrentDictionary<string, long> _nextIndex = new();
        private readonly ConcurrentDictionary<string, long> _matchIndex = new();

        // [FIX 2] Event-Driven Commit Registry
        // Maps Log Index -> Task Completion Source (Waiting Client)
        private readonly ConcurrentDictionary<long, TaskCompletionSource<bool>> _pendingProposals = new();

        // Timers & locks
        private readonly Timer? _electionTimer;
        private Timer? _heartbeatTimer;
        private readonly Random _rng = new();
        private readonly Lock _stateLock = new();

        // Callbacks
        private Action<Proposal>? _commitHandler;

        // --- Constants ---
        private const int HeartbeatIntervalMs = 150; // Fast heartbeats
        private const int MinElectionTimeoutMs = 300;
        private const int MaxElectionTimeoutMs = 600;
        private const int BatchSize = 100; // Max log entries per RPC

        public bool IsLeader => _currentState == RaftState.Leader;

        public RaftEngine(string nodeId, FederationManager federation, IKernelContext context)
        {
            _nodeId = nodeId;
            _federation = federation;
            _context = context;
            _log = new RaftLog(context.RootPath);

            // [FIX 1] Load Persistent State
            _metaStore = new DurableState<RaftMetadata>(System.IO.Path.Combine(context.RootPath, "raft_meta.json"));
            if (!_metaStore.TryGet("state", out _persistentState!) || _persistentState == null)
            {
                _persistentState = new RaftMetadata { CurrentTerm = 0, VotedFor = null };
                PersistState();
            }
            _electionTimer = new Timer(OnElectionTimeout, null, _rng.Next(MinElectionTimeoutMs, MaxElectionTimeoutMs), Timeout.Infinite);
            InitializeTimers();
        }

        public void Initialize(IKernelContext context) { }

        private void InitializeTimers()
        {
            ResetElectionTimer();
            _heartbeatTimer = new Timer(OnHeartbeatTick, null, Timeout.Infinite, Timeout.Infinite);
        }

        // --- Public API ---

        public async Task<bool> ProposeAsync(Proposal proposal)
        {
            if (!IsLeader)
            {
                // [FIX 3] Redirect to Leader
                if (_leaderId != null)
                {
                    throw new NotLeaderException(_leaderId, $"[Raft] Cannot propose. Not Leader. Leader is {_leaderId}");
                }
                return false;
            }

            long entryIndex;
            TaskCompletionSource<bool> tcs;

            lock (_stateLock)
            {
                entryIndex = _log.LastLogIndex + 1;
                var entry = new LogEntry
                {
                    Index = entryIndex,
                    Term = _currentTerm,
                    Command = proposal
                };
                _log.Append(entry);

                // [FIX 2] Register TCS
                tcs = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
                _pendingProposals[entryIndex] = tcs;

                _context.LogInfo($"[Raft] Proposed {entryIndex}. Waiting for quorum...");
            }

            // Force immediate replication trigger
            await ReplicateLogToFollowersAsync();

            // [FIX 2] Efficient Await (No Polling)
            // Add a timeout safety
            var timeoutTask = Task.Delay(5000);
            var completedTask = await Task.WhenAny(tcs.Task, timeoutTask);

            if (completedTask == timeoutTask)
            {
                _pendingProposals.TryRemove(entryIndex, out _);
                _context.LogWarning($"[Raft] Proposal {entryIndex} timed out waiting for quorum.");
                return false;
            }

            return await tcs.Task;
        }

        public void OnCommit(Action<Proposal> handler)
        {
            _commitHandler = handler;
        }

        // --- Persistence Helpers ---

        private void PersistState()
        {
            // Saves Term and VotedFor synchronously to disk
            _metaStore.Set("state", _persistentState);
        }

        // --- State Transitions ---

        private void BecomeFollower(long term)
        {
            lock (_stateLock)
            {
                if (term > _persistentState.CurrentTerm)
                {
                    _persistentState.CurrentTerm = term;
                    _persistentState.VotedFor = null;
                    PersistState(); // Save immediately
                }

                _currentState = RaftState.Follower;
                _leaderId = null;

                ResetElectionTimer();
                _heartbeatTimer?.Change(Timeout.Infinite, Timeout.Infinite);
            }
        }

        private void BecomeLeader()
        {
            lock (_stateLock)
            {
                if (_currentState != RaftState.Candidate) return;

                _currentState = RaftState.Leader;
                _leaderId = _nodeId;
                _context.LogInfo($"[Raft] ELECTED LEADER for Term {_persistentState.CurrentTerm}");

                var peers = _federation.GetPeers();
                foreach (var peer in peers)
                {
                    _nextIndex[peer.NodeId] = _log.LastLogIndex + 1;
                    _matchIndex[peer.NodeId] = 0;
                }

                _electionTimer?.Change(Timeout.Infinite, Timeout.Infinite);
                _heartbeatTimer?.Change(0, HeartbeatIntervalMs);
            }
            Task.Run(ReplicateLogToFollowersAsync);
        }

        // --- State Machine Events ---

        private void OnElectionTimeout(object? state)
        {
            lock (_stateLock)
            {
                if (_currentState == RaftState.Leader) return;

                _persistentState.CurrentTerm++;
                _persistentState.VotedFor = _nodeId;
                PersistState(); // Save new term

                _currentState = RaftState.Candidate;
                _context.LogInfo($"[Raft] Election Started. Term: {_persistentState.CurrentTerm}");
            }

            ResetElectionTimer();
            Task.Run(RequestVotesAsync);
        }

        private async Task RequestVotesAsync()
        {
            var peers = _federation.GetPeers();
            int votes = 1;
            int quorum = (peers.Count + 1) / 2 + 1;

            if (peers.Count == 0 && votes >= quorum)
            {
                BecomeLeader();
                return;
            }

            var tasks = peers.Select(async peer =>
            {
                try
                {
                    using var channel = GrpcChannel.ForAddress(peer.Address);
                    var client = new StorageTransport.StorageTransportClient(channel);

                    var request = new VoteRequest
                    {
                        Term = _persistentState.CurrentTerm,
                        CandidateId = _nodeId,
                        LastLogIndex = _log.LastLogIndex,
                        LastLogTerm = _log.LastLogTerm
                    };

                    var reply = await client.RequestVoteAsync(request, deadline: DateTime.UtcNow.AddMilliseconds(200));

                    lock (_stateLock)
                    {
                        if (reply.Term > _persistentState.CurrentTerm)
                        {
                            BecomeFollower(reply.Term);
                            return;
                        }
                        if (reply.VoteGranted && _currentState == RaftState.Candidate)
                        {
                            votes++;
                            if (votes >= quorum) BecomeLeader();
                        }
                    }
                }
                catch { }
            });

            await Task.WhenAll(tasks);
        }

        /// <summary>
        /// Handles an INCOMING Vote Request from a peer candidate.
        /// This is the server-side logic that decides whether to grant a vote.
        /// </summary>
        public VoteResponse HandleRequestVote(VoteRequest request)
        {
            lock (_stateLock)
            {
                // 1. Reply False if Term < CurrentTerm
                if (request.Term < _persistentState.CurrentTerm)
                {
                    return new VoteResponse
                    {
                        Term = _persistentState.CurrentTerm,
                        VoteGranted = false
                    };
                }

                // 2. If Term > CurrentTerm, update immediately and become Follower
                if (request.Term > _persistentState.CurrentTerm)
                {
                    _persistentState.CurrentTerm = request.Term;
                    _persistentState.VotedFor = null; // Reset vote for the new term
                    PersistState();
                    BecomeFollower(request.Term);
                }

                // 3. Safety Check: Have we already voted for someone else in this term?
                // [FIX] This uses the previously "unused" variable
                bool alreadyVotedForOthers = _persistentState.VotedFor != null && _persistentState.VotedFor != request.CandidateId;

                // 4. Safety Check: Is Candidate's Log up-to-date?
                // Raft ensures we only vote for candidates with all committed entries.
                long lastLogIdx = _log.LastLogIndex;
                long lastLogTerm = _log.LastLogTerm;

                bool isLogUpToDate = (request.LastLogTerm > lastLogTerm) ||
                                     (request.LastLogTerm == lastLogTerm && request.LastLogIndex >= lastLogIdx);

                if (!alreadyVotedForOthers && isLogUpToDate)
                {
                    // GRANT VOTE
                    _persistentState.VotedFor = request.CandidateId;
                    PersistState();

                    // Reset election timer so we don't timeout while waiting for this leader
                    ResetElectionTimer();

                    _context.LogInfo($"[Raft] Granting Vote to {request.CandidateId} for Term {_persistentState.CurrentTerm}");

                    return new VoteResponse
                    {
                        Term = _persistentState.CurrentTerm,
                        VoteGranted = true
                    };
                }

                // Deny Vote
                return new VoteResponse
                {
                    Term = _persistentState.CurrentTerm,
                    VoteGranted = false
                };
            }
        }

        /// <summary>
        /// Handles an INCOMING AppendEntries RPC (Heartbeat or Data Replication).
        /// This ensures the Follower's log matches the Leader's log.
        /// </summary>
        public AppendResponse HandleAppendEntries(AppendRequest request)
        {
            lock (_stateLock)
            {
                // 1. Term Check: Reject requests from old leaders (Zombies)
                if (request.Term < _persistentState.CurrentTerm)
                {
                    return new AppendResponse
                    {
                        Term = _persistentState.CurrentTerm,
                        Success = false
                    };
                }

                // 2. Update Term: We found a valid leader
                if (request.Term >= _persistentState.CurrentTerm)
                {
                    _persistentState.CurrentTerm = request.Term;
                    _persistentState.VotedFor = null; // Reset vote
                    PersistState();

                    // Transition to Follower if we were Candidate/Leader
                    if (_currentState != RaftState.Follower)
                    {
                        BecomeFollower(request.Term);
                    }

                    _leaderId = request.LeaderId;
                    ResetElectionTimer(); // Heartbeat received, reset timeout
                }

                // 3. Consistency Check: Does our log contain the 'PrevLogIndex'?
                // If the Leader says "I'm appending after index 10", but we only have index 5, we fail.
                if (request.PrevLogIndex > 0)
                {
                    var prevEntry = _log.Get(request.PrevLogIndex);

                    // Fail if index doesn't exist OR terms mismatch (Log Divergence)
                    if (prevEntry == null || prevEntry.Term != request.PrevLogTerm)
                    {
                        _context.LogWarning($"[Raft] Consistency Check Failed. MyLog: {_log.LastLogIndex}. Leader expects prev: {request.PrevLogIndex} (Term {request.PrevLogTerm})");
                        return new AppendResponse
                        {
                            Term = _persistentState.CurrentTerm,
                            Success = false
                        };
                    }
                }

                // 4. Process New Entries (Reconciliation)
                foreach (var entryProto in request.Entries)
                {
                    // Convert Proto to Model
                    var newEntry = new LogEntry
                    {
                        Index = entryProto.Index,
                        Term = entryProto.Term,
                        Command = JsonSerializer.Deserialize<Proposal>(entryProto.CommandJson)!
                    };

                    // Check for conflict: Does index exist with different term?
                    var existing = _log.Get(newEntry.Index);
                    if (existing != null)
                    {
                        if (existing.Term != newEntry.Term)
                        {
                            // CONFLICT: Delete everything from here onwards
                            _context.LogWarning($"[Raft] Conflict at index {newEntry.Index}. Truncating.");
                            _log.TruncateFrom(newEntry.Index);
                            _log.Append(newEntry);
                        }
                        // If term matches, it's a duplicate. Ignore.
                    }
                    else
                    {
                        // No conflict, just append
                        _log.Append(newEntry);
                    }
                }

                // 5. Update Commit Index
                // If Leader committed X, we can commit X (or our last index, whichever is smaller)
                if (request.LeaderCommit > _commitIndex)
                {
                    long lastNewIndex = request.Entries.Count > 0
                        ? request.Entries.Last().Index
                        : _log.LastLogIndex;

                    _commitIndex = Math.Min(request.LeaderCommit, lastNewIndex);

                    // Apply to State Machine immediately
                    ApplyToStateMachine();
                }

                return new AppendResponse
                {
                    Term = _persistentState.CurrentTerm,
                    Success = true
                };
            }
        }

        // --- Replication Logic (The Critical Fix) ---

        private void OnHeartbeatTick(object? state)
        {
            if (_currentState != RaftState.Leader) return;
            Task.Run(ReplicateLogToFollowersAsync);
        }

        private async Task ReplicateLogToFollowersAsync()
        {
            var peers = _federation.GetPeers();

            // Single node cluster: just update commit index
            if (peers.Count == 0)
            {
                UpdateCommitIndex(0); // 0 peers needed
                return;
            }

            var tasks = peers.Select(peer => ReplicateToPeerAsync((FederationManager.SimpleFederationNode)peer));
            await Task.WhenAll(tasks);

            // After replication round, try to advance commit index
            UpdateCommitIndex(peers.Count);
        }

        private async Task ReplicateToPeerAsync(FederationManager.SimpleFederationNode peer)
        {
            try
            {
                long prevLogIndex;
                long prevLogTerm;
                List<LogEntry> entriesToSend;

                // 1. Prepare Batch
                lock (_stateLock)
                {
                    if (_currentState != RaftState.Leader) return;

                    long nextIdx = _nextIndex.GetValueOrDefault(peer.NodeId, _log.LastLogIndex + 1);
                    prevLogIndex = nextIdx - 1;
                    prevLogTerm = prevLogIndex > 0 ? (_log.Get(prevLogIndex)?.Term ?? 0) : 0;

                    entriesToSend = [.. _log.GetRange(nextIdx).Take(BatchSize)];
                }

                // 2. Send RPC
                using var channel = GrpcChannel.ForAddress(peer.Address);
                var client = new StorageTransport.StorageTransportClient(channel);

                var request = new AppendRequest
                {
                    Term = _currentTerm,
                    LeaderId = _nodeId,
                    PrevLogIndex = prevLogIndex,
                    PrevLogTerm = prevLogTerm,
                    LeaderCommit = _commitIndex
                };

                // Map entries to Proto
                foreach (var e in entriesToSend)
                {
                    request.Entries.Add(new LogEntryProto
                    {
                        Index = e.Index,
                        Term = e.Term,
                        CommandJson = JsonSerializer.Serialize(e.Command)
                    });
                }

                var reply = await client.AppendEntriesAsync(request, deadline: DateTime.UtcNow.AddMilliseconds(500));

                // 3. Process Reply
                lock (_stateLock)
                {
                    if (reply.Term > _persistentState.CurrentTerm)
                    {
                        BecomeFollower(reply.Term);
                        return;
                    }

                    if (reply.Success)
                    {
                        if (entriesToSend.Count > 0)
                        {
                            long lastMatch = entriesToSend.Last().Index;
                            _nextIndex[peer.NodeId] = lastMatch + 1;
                            _matchIndex[peer.NodeId] = lastMatch;
                        }
                    }
                    else
                    {
                        var currentNext = _nextIndex[peer.NodeId];
                        _nextIndex[peer.NodeId] = Math.Max(1, currentNext - 1);
                    }
                }
            }
            catch (Exception ex)
            {
                _context.LogDebug($"[Raft] Replication failed to {peer.NodeId}: {ex.Message}");
            }
        }

        private void UpdateCommitIndex(int peerCount)
        {
            lock (_stateLock)
            {
                if (_currentState != RaftState.Leader) return;

                // Find largest N such that N > commitIndex, and majority of matchIndex[i] >= N, and log[N].term == currentTerm
                long start = _commitIndex + 1;
                long end = _log.LastLogIndex;

                for (long n = end; n >= start; n--)
                {
                    var entry = _log.Get(n);
                    if (entry == null || entry.Term != _persistentState.CurrentTerm) continue;

                    int count = 1;
                    foreach (var peerId in _matchIndex.Keys)
                    {
                        if (_matchIndex[peerId] >= n) count++;
                    }

                    int quorum = (peerCount + 1) / 2 + 1;
                    if (count >= quorum)
                    {
                        _commitIndex = n;
                        ApplyToStateMachine();
                        break;
                    }
                }
            }
        }

        private void ApplyToStateMachine()
        {
            while (_lastApplied < _commitIndex)
            {
                _lastApplied++;
                var entry = _log.Get(_lastApplied);
                if (entry != null)
                {
                    _commitHandler?.Invoke(entry.Command);
                    _context.LogInfo($"[Raft] Applied Index {_lastApplied}");

                    // [FIX 2] Notify Client
                    if (_pendingProposals.TryRemove(_lastApplied, out var tcs))
                    {
                        tcs.TrySetResult(true);
                    }
                }
            }
        }

        private void ResetElectionTimer()
        {
            _electionTimer?.Change(_rng.Next(MinElectionTimeoutMs, MaxElectionTimeoutMs), Timeout.Infinite);
        }

        public void Dispose()
        {
            _electionTimer?.Dispose();
            _heartbeatTimer?.Dispose();
            GC.SuppressFinalize(this);
        }

        // --- Data Classes ---

        // DTO for Persistent State
        public class RaftMetadata
        {
            public long CurrentTerm { get; set; }
            public string? VotedFor { get; set; }
        }

        // Exception for Redirection
        public class NotLeaderException(string leaderHint, string message) : Exception(message)
        {
            public string LeaderHint { get; } = leaderHint;
        }

        private enum RaftState { Follower, Candidate, Leader }
    }
}