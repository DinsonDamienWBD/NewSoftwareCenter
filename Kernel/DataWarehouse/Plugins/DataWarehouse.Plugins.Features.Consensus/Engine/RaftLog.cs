using DataWarehouse.SDK.Contracts;
using DataWarehouse.SDK.Utilities;

namespace DataWarehouse.Plugins.Features.Consensus.Engine
{
    /// <summary>
    /// Represents the Persistent Write-Ahead Log (WAL) for the Raft Consensus Algorithm.
    /// Stores commands that have been proposed but not necessarily committed.
    /// </summary>
    public class RaftLog
    {
        // In V5, we use DurableState (Json) for simplicity, but in V6 this should be a binary append-only file.
        private readonly DurableState<LogEntry> _store;
        private readonly List<LogEntry> _memoryLog; // Sorted Cache
        private readonly Lock _lock = new();

        /// <summary>
        /// Initializes the Raft Log.
        /// </summary>
        public RaftLog(string rootPath)
        {
            string logPath = System.IO.Path.Combine(rootPath, "raft_wal.json");
            _store = new DurableState<LogEntry>(logPath);

            // Hydrate memory cache sorted by Index
            _memoryLog = [.. _store.GetAll().OrderBy(e => e.Index)];
        }

        /// <summary>
        /// Gets the index of the last entry in the log.
        /// </summary>
        public long LastLogIndex
        {
            get { lock (_lock) return _memoryLog.Count > 0 ? _memoryLog[^1].Index : 0; }
        }

        /// <summary>
        /// Gets the term of the last entry in the log.
        /// </summary>
        public long LastLogTerm
        {
            get { lock (_lock) return _memoryLog.Count > 0 ? _memoryLog[^1].Term : 0; }
        }

        /// <summary>
        /// Appends a new entry to the log.
        /// </summary>
        public void Append(LogEntry entry)
        {
            lock (_lock)
            {
                // Ensure monotonicity
                if (entry.Index <= LastLogIndex)
                {
                    // Truncate conflict: If we are appending an index that exists, overwrite it and all subsequent
                    TruncateFrom(entry.Index);
                }

                _memoryLog.Add(entry);
                _store.Set(entry.Index.ToString(), entry);
            }
        }

        /// <summary>
        /// Gets an entry by index.
        /// </summary>
        public LogEntry? Get(long index)
        {
            lock (_lock)
            {
                // Binary search or direct lookup if list is contiguous
                if (index <= 0 || index > LastLogIndex) return null;

                // Optimization: Assuming contiguous 1-based indexing
                int arrayIndex = (int)index - 1;
                if (arrayIndex < _memoryLog.Count && _memoryLog[arrayIndex].Index == index)
                {
                    return _memoryLog[arrayIndex];
                }

                return _memoryLog.FirstOrDefault(e => e.Index == index);
            }
        }

        /// <summary>
        /// Deletes all entries from specific index onwards (Handling Split Brain healing).
        /// </summary>
        public void TruncateFrom(long index)
        {
            lock (_lock)
            {
                var toRemove = _memoryLog.Where(e => e.Index >= index).ToList();
                foreach (var item in toRemove)
                {
                    _store.Remove(item.Index.ToString(), out _);
                    _memoryLog.Remove(item);
                }
            }
        }

        /// <summary>
        /// Gets a range of entries for replication.
        /// </summary>
        public List<LogEntry> GetRange(long startIndex)
        {
            lock (_lock)
            {
                return [.. _memoryLog.Where(e => e.Index >= startIndex).OrderBy(e => e.Index)];
            }
        }
    }

    /// <summary>
    /// A single entry in the Raft Log.
    /// </summary>
    public class LogEntry
    {
        public long Index { get; set; }
        public long Term { get; set; }
        public required Proposal Command { get; set; }
    }
}