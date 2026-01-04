using DataWarehouse.SDK.Contracts;
using DataWarehouse.SDK.Primitives;    // Uses NodeHandshake
using DataWarehouse.SDK.Utilities; // Uses DurableState

namespace DataWarehouse.Plugins.Features.Consensus.Services
{
    /// <summary>
    /// Manages the list of known peers (Federation) and their connection states.
    /// Acts as the address book for the cluster.
    /// </summary>
    public class FederationManager : IDisposable
    {
        private readonly DurableState<string> _peers;
        private readonly string _rootPath;
        private bool _isDisposed;

        /// <summary>
        /// Initializes the Federation Manager.
        /// </summary>
        /// <param name="rootPath">Root path for storing peer lists.</param>
        public FederationManager(string rootPath)
        {
            _rootPath = rootPath;
            string dbPath = Path.Combine(rootPath, "peers.json");

            // [Fix] Matches the single-arg constructor in SDK
            _peers = new DurableState<string>(dbPath);
        }

        /// <summary>
        /// Registers a known peer.
        /// </summary>
        /// <param name="nodeId">The unique node ID.</param>
        /// <param name="address">The connection address (e.g., https://ip:port).</param>
        public void RegisterPeer(string nodeId, string address)
        {
            _peers.Set(nodeId, address);
        }

        /// <summary>
        /// Removes a peer from the federation.
        /// </summary>
        public void RemovePeer(string nodeId)
        {
            _peers.Remove(nodeId);
        }

        /// <summary>
        /// Retrieves all known peers as IFederationNode objects.
        /// </summary>
        public List<IFederationNode> GetPeers()
        {
            var list = new List<IFederationNode>();
            foreach (var kvp in _peers.ToDictionary())
            {
                list.Add(new SimpleFederationNode(kvp.Key, kvp.Value));
            }
            return list;
        }

        /// <summary>
        /// Disposes resources.
        /// </summary>
        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        /// <summary>
        /// Protected dispose pattern.
        /// </summary>
        protected virtual void Dispose(bool disposing)
        {
            if (!_isDisposed)
            {
                if (disposing)
                {
                    _peers.Dispose();
                }
                _isDisposed = true;
            }
        }

        // [Internal Class] Represents a directory entry.
        // Implements IFederationNode for discovery purposes only.
        private class SimpleFederationNode(string id, string addr) : IFederationNode
        {
            public string NodeId { get; } = id;
            public string Address { get; } = addr;
            public bool IsActive => true;

            public Task<NodeHandshake> HandshakeAsync(string requestorNodeId)
            {
                // Simulated Handshake for directory listing
                return Task.FromResult(new NodeHandshake
                {
                    Success = true,
                    NodeId = this.NodeId,
                    Version = "5.0.0",
                    Capabilities = ["Consensus", "Storage"]
                });
            }

            public Task<Manifest?> GetManifestAsync(string blobId)
            {
                throw new NotSupportedException("Discovery-only node. Use TransportClient for data.");
            }

            public Task<Stream> OpenReadStreamAsync(string blobId, long start, long length)
            {
                throw new NotSupportedException("Discovery-only node. Use TransportClient for data.");
            }

            public Task WriteStreamAsync(string blobId, Stream source)
            {
                throw new NotSupportedException("Discovery-only node. Use TransportClient for data.");
            }
        }
    }
}