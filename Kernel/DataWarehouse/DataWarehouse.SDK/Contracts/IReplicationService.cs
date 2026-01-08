namespace DataWarehouse.SDK.Contracts
{
    /// <summary>
    /// Manages data redundancy, backups, and restoration.
    /// Used by the Neural Sentinel to auto-heal corrupted data.
    /// </summary>
    public interface IReplicationService : IPlugin
    {
        /// <summary>
        /// Restores a corrupted blob using a healthy replica.
        /// </summary>
        /// <param name="blobId">The ID of the corrupted manifest/blob.</param>
        /// <param name="replicaId">The specific replica node/ID to restore from (optional).</param>
        /// <returns>True if restoration was successful.</returns>
        Task<bool> RestoreAsync(string blobId, string? replicaId);
    }
}