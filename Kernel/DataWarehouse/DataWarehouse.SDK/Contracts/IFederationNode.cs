using DataWarehouse.SDK.Primitives;

namespace DataWarehouse.SDK.Contracts
{
    /// <summary>
    /// The Service Contract for gRPC implementation
    /// </summary>
    public interface IFederationNode
    {
        /// <summary>
        /// Node ID
        /// </summary>
        string NodeId { get; }

        /// <summary>
        /// Base address for RPC calls (e.g., "https://10.0.0.5:5000")
        /// </summary>
        string Address { get; }

        /// <summary>
        /// Is Active
        /// </summary>
        bool IsActive { get; }

        /// <summary>
        /// Handshake with a node.
        /// "Hello, I am Node A. Here are my public capabilities."
        /// </summary>
        /// <param name="nodeId"></param>
        /// <returns></returns>
        Task<NodeHandshake> HandshakeAsync(string nodeId);

        /// <summary>
        /// Get manifest from a node.
        /// "Do you have file X?"
        /// </summary>
        /// <param name="uri"></param>
        /// <returns></returns>
        Task<Manifest?> GetManifestAsync(string uri);

        /// <summary>
        /// Read data from a node.
        /// "Give me the bytes for file X."
        /// </summary>
        /// <param name="uri"></param>
        /// <param name="offset"></param>
        /// <param name="length"></param>
        /// <returns></returns>
        Task<Stream> OpenReadStreamAsync(string uri, long offset, long length);

        /// <summary>
        /// Write data to a node.
        /// "Here is a file to store." (Only if we have write permission)
        /// </summary>
        /// <param name="uri"></param>
        /// <param name="data"></param>
        /// <returns></returns>
        Task WriteStreamAsync(string uri, Stream data);
    }
}