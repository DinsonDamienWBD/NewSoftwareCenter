namespace DataWarehouse.SDK.Contracts
{
    /// <summary>
    /// Proposal
    /// </summary>
    public class Proposal
    {
        /// <summary>
        /// ID
        /// </summary>
        public string Id { get; set; } = Guid.NewGuid().ToString();

        /// <summary>
        /// Command
        /// </summary>
        public string Command { get; set; } = string.Empty;

        /// <summary>
        /// Payload
        /// </summary>
        public byte[] Payload { get; set; } = [];
    }

    /// <summary>
    /// Consensus interface
    /// </summary>
    public interface IConsensusEngine : IPlugin
    {
        /// <summary>
        /// Is leader
        /// </summary>
        bool IsLeader { get; }

        /// <summary>
        /// Propose a state change to the cluster.
        /// Returns only when Quorum is reached.
        /// </summary>
        Task<bool> ProposeAsync(Proposal proposal);

        /// <summary>
        /// Subscribe to committed entries from other nodes.
        /// </summary>
        void OnCommit(Action<Proposal> handler);
    }
}