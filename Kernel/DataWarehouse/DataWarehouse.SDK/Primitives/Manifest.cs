namespace DataWarehouse.SDK.Primitives
{
    /// <summary>
    /// Manifest definition
    /// </summary>
    public class Manifest
    {
        /// <summary>
        /// Manifest ID
        /// </summary>
        public string Id { get; set; } = Guid.NewGuid().ToString("N");

        /// <summary>
        /// Container ID
        /// </summary>
        public string ContainerId { get; set; } = "default";

        /// <summary>
        /// BLOB URI
        /// </summary>
        public string BlobUri { get; set; } = string.Empty;

        /// <summary>
        /// BLOB size in bytes
        /// </summary>
        public long SizeBytes { get; set; }

        /// <summary>
        /// Created datetime in offset
        /// </summary>
        public long CreatedAt { get; set; } = DateTimeOffset.UtcNow.ToUnixTimeSeconds();

        // Security Metadata

        /// <summary>
        /// Owner/Creator ID
        /// </summary>
        public string OwnerId { get; set; } = string.Empty;

        // Pipeline used to write this file (needed for read-back)

        /// <summary>
        /// Pipeline config
        /// </summary>
        public PipelineConfig Pipeline { get; set; } = new();

        // AI / Search

        /// <summary>
        /// Vector embedding
        /// </summary>
        public float[]? VectorEmbedding { get; set; }

        /// <summary>
        /// Tags
        /// </summary>
        public Dictionary<string, string> Tags { get; set; } = [];

        /// <summary>
        /// A brief text summary of the content (first 100 chars, AI summary, etc).
        /// Used by SQL SELECT queries.
        /// </summary>
        public string? ContentSummary { get; set; }

        /// <summary>
        /// The current storage tier (Hot, Warm, Cold).
        /// </summary>
        public string CurrentTier { get; set; } = "Warm";

        /// <summary>
        /// Timestamp of last read access (Unix Seconds).
        /// </summary>
        public long LastAccessedAt { get; set; } = DateTimeOffset.UtcNow.ToUnixTimeSeconds();

        /// <summary>
        /// Integrity Checksum (SHA256/CRC32).
        /// </summary>
        public string Checksum { get; set; } = string.Empty;

        /// <summary>
        /// [NEW] System-managed tags for Governance and Sentinel state.
        /// Examples: "SentinelScan:Passed", "PII:True", "Retention:7Years"
        /// </summary>
        public Dictionary<string, string> GovernanceTags { get; set; } = [];

        /// <summary>
        /// Entity Tag for concurrency control.
        /// </summary>
        public string ETag { get; set; } = string.Empty;
    }
}