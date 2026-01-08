using DataWarehouse.SDK.Primitives;

namespace DataWarehouse.SDK.Contracts
{
    // --- COMMANDS (Do something) ---

    /// <summary>
    /// Save data command
    /// </summary>
    public record StoreBlobCommand
    {
        /// <summary>
        /// Bucket
        /// </summary>
        public string Bucket { get; init; } = string.Empty;

        /// <summary>
        /// Key
        /// </summary>
        public string Key { get; init; } = string.Empty;

        /// <summary>
        /// Data
        /// </summary>
        public Stream Data { get; init; } = Stream.Null; // Or byte[] for network scenarios

        /// <summary>
        /// Intent
        /// </summary>
        public StorageIntent? Intent { get; init; } // Optional override

        /// <summary>
        /// ETag
        /// </summary>
        public string? ExpectedETag { get; init; }

        /// <summary>
        /// Manifest metadata
        /// </summary>
        public Manifest? Metadata { get; init; } // AI Tags, Summary
    }

    /// <summary>
    /// Delete data
    /// </summary>
    /// <param name="Bucket"></param>
    /// <param name="Key"></param>
    public record DeleteBlobCommand(string Bucket, string Key);

    /// <summary>
    /// Memorize
    /// </summary>
    /// <param name="Content"></param>
    /// <param name="Tags"></param>
    public record MemorizeFactCommand(string Content, string[] Tags);

    // --- QUERIES (Get something) ---

    /// <summary>
    /// Get data
    /// </summary>
    /// <param name="Bucket"></param>
    /// <param name="Key"></param>
    public record GetBlobQuery(string Bucket, string Key);

    /// <summary>
    /// Search data
    /// </summary>
    /// <param name="Text"></param>
    /// <param name="Limit"></param>
    public record SearchMemoriesQuery(string Text, int Limit = 10);

    // --- EVENTS (Something happened) ---

    /// <summary>
    /// Saved data event
    /// </summary>
    /// <param name="Uri"></param>
    /// <param name="ETag"></param>
    /// <param name="SizeBytes"></param>
    public record BlobStoredEvent(string Uri, string ETag, long SizeBytes);

    /// <summary>
    /// Data access event
    /// </summary>
    /// <param name="Uri"></param>
    /// <param name="AccessorId"></param>
    public record BlobAccessedEvent(string Uri, string AccessorId);

    // --- FEDERATION COMMANDS ---

    /// <summary>
    /// Link a remote Data Warehouse.
    /// Usage: Frontend sends this when user clicks "Add Server".
    /// </summary>
    public record LinkRemoteStorageCommand(string Alias, string Address);

    /// <summary>
    /// Unlink a remote Data Warehouse.
    /// Usage: Frontend sends this when user clicks "Disconnect".
    /// </summary>
    public record UnlinkRemoteStorageCommand(string Alias);

    /// <summary>
    /// Query to get list of all active links for the UI Dashboard.
    /// </summary>
    public record GetLinkedResourcesQuery();

    /// <summary>
    /// COMMAND: "Turn off Quantum Encryption"
    /// </summary>
    /// <param name="PluginId"></param>
    /// <param name="IsEnabled"></param>
    public record ToggleFeatureCommand(string PluginId, bool IsEnabled);

    /// <summary>
    /// QUERY: "What features do I have installed, and are they on?"
    /// Returns: [{ "Id": "QuantumCrypto", "Name": "Quantum Encryption v1", "Enabled": true }]
    /// </summary>
    public record GetFeatureStatusQuery();
}