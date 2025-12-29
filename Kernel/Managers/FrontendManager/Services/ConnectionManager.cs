using System.Collections.Concurrent;

namespace FrontendManager.Services
{
    /// <summary>
    /// Contract for managing user connections in a real-time application.
    /// </summary>
    public interface IConnectionManager
    {
        /// <summary>
        /// Adds a connection for a user.
        /// </summary>
        /// <param name="userId"></param>
        /// <param name="connectionId"></param>
        void AddConnection(string userId, string connectionId);

        /// <summary>
        /// Removes a connection by its ID.
        /// </summary>
        /// <param name="connectionId"></param>
        void RemoveConnection(string connectionId);

        /// <summary>
        /// Gets all connection IDs for a given user.
        /// </summary>
        /// <param name="userId"></param>
        /// <returns></returns>
        IEnumerable<string> GetConnections(string userId);

        /// <summary>
        /// Gets a list of all online users.
        /// </summary>
        /// <returns></returns>
        IEnumerable<string> GetOnlineUsers();
    }

    /// <summary>
    /// Concrete implementation of IConnectionManager using in-memory storage.
    /// </summary>
    public class ConnectionManager : IConnectionManager
    {
        // Key: UserId, Value: Set of ConnectionIds (a user can have multiple tabs open)
        private readonly ConcurrentDictionary<string, HashSet<string>> _userMap = new();

        // Reverse lookup: Key: ConnectionId, Value: UserId (for fast disconnect)
        private readonly ConcurrentDictionary<string, string> _connectionMap = new();

        /// <summary>
        /// Adds a connection for a user.
        /// </summary>
        /// <param name="userId"></param>
        /// <param name="connectionId"></param>
        public void AddConnection(string userId, string connectionId)
        {
            _connectionMap[connectionId] = userId;

            _userMap.AddOrUpdate(userId,
                _ => new HashSet<string> { connectionId },
                (_, connections) =>
                {
                    lock (connections)
                    {
                        connections.Add(connectionId);
                        return connections;
                    }
                });
        }

        /// <summary>
        /// Removes a connection by its ID.
        /// </summary>
        /// <param name="connectionId"></param>
        public void RemoveConnection(string connectionId)
        {
            if (_connectionMap.TryRemove(connectionId, out var userId))
            {
                if (_userMap.TryGetValue(userId, out var connections))
                {
                    lock (connections)
                    {
                        connections.Remove(connectionId);
                        if (connections.Count == 0)
                        {
                            _userMap.TryRemove(userId, out _);
                        }
                    }
                }
            }
        }

        /// <summary>
        /// Gets all connection IDs for a given user.
        /// </summary>
        /// <param name="userId"></param>
        /// <returns></returns>
        public IEnumerable<string> GetConnections(string userId)
        {
            if (_userMap.TryGetValue(userId, out var connections))
            {
                lock (connections)
                {
                    return connections.ToArray();
                }
            }
            return Enumerable.Empty<string>();
        }

        /// <summary>
        /// Gets a list of all online users.
        /// </summary>
        /// <returns></returns>
        public IEnumerable<string> GetOnlineUsers() => _userMap.Keys;
    }
}