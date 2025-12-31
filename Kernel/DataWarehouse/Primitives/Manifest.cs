namespace DataWarehouse.Primitives
{
    /// <summary>
    /// THE COSMIC ENVELOPE.
    /// Separates "What it is" (Metadata) from "Where it is" (Blob).
    /// </summary>
    public class Manifest
    {
        /// <summary>
        /// ID
        /// </summary>
        public string Id { get; set; } = Guid.NewGuid().ToString("N");

        /// <summary>
        /// Created At
        /// </summary>
        public long CreatedAt { get; set; } = DateTimeOffset.UtcNow.ToUnixTimeSeconds();

        /// <summary>
        /// Universal Addressing (e.g., "file:///storage/blob/123", "s3://bucket/123")
        /// </summary>
        public string BlobUri { get; set; } = string.Empty;

        /// <summary>
        /// The Recipe used to create this file
        /// </summary>
        public PipelineConfig Pipeline { get; set; } = new PipelineConfig();

        /// <summary>
        /// User Metadata (Searchable)
        /// </summary>
        public Dictionary<string, string> Tags { get; set; } = [];

        // AI / Agent Metadata (The "Cognitive Layer")
        // Allows RAG (Retrieval Augmented Generation) without decrypting the blob.

        /// <summary>
        /// For Semantic Search
        /// </summary>
        public float[]? VectorEmbedding { get; set; }

        /// <summary>
        /// Token-efficient summary for LLMs
        /// </summary>
        public string? ContentSummary { get; set; }

        /// <summary>
        /// JSON Schema for Agents to validate structure
        /// </summary>
        public string? ContentSchema { get; set; }

        /// <summary>
        /// Integrity
        /// SHA-256 of the *Raw* content
        /// </summary>
        public string Checksum { get; set; } = string.Empty;

        /// <summary>
        /// Size in bytes
        /// </summary>
        public long SizeBytes { get; set; }

        // CONCURRENCY CONTROL

        /// <summary>
        /// ETag
        /// </summary>
        public string ETag { get; set; } = Guid.NewGuid().ToString("N");

        /// <summary>
        /// Version
        /// </summary>
        public int Version { get; set; } = 1;
    }

    /// <summary>
    /// Pipeline configuration
    /// </summary>
    public class PipelineConfig
    {
        /// <summary>
        /// Algorithm to be used for data compression
        /// </summary>
        public string CompressionAlgo { get; set; } = "None";

        /// <summary>
        /// Algorithm to be used for encryption
        /// </summary>
        public string CryptoAlgo { get; set; } = "None";

        /// <summary>
        /// Key ID in the KeyStore
        /// </summary>
        public string KeyId { get; set; } = string.Empty;

        /// <summary>
        /// Transformation Order
        /// </summary>
        public List<string> TransformationOrder { get; set; } = new() { "Compression", "Encryption" };
    }
}