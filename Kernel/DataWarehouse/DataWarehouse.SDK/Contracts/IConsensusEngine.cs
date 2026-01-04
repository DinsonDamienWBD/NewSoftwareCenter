namespace DataWarehouse.SDK.Contracts
{
    /// <summary>
    /// Consensus proposal record
    /// </summary>
    /// <param name="Id"></param>
    /// <param name="Command"></param>
    /// <param name="Payload"></param>
    public record Proposal(string Id, string Command, byte[] Payload);

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