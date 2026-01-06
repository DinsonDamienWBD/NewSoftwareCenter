using System.Collections.Generic;
using System.Threading.Tasks;

namespace DataWarehouse.SDK.AI.Vector
{
    /// <summary>
    /// Interface for storing and searching embedding vectors with metadata.
    /// Enables semantic search over capabilities using natural language queries.
    ///
    /// Used by AI Runtime to:
    /// - Index capability embeddings with metadata
    /// - Perform semantic search (cosine similarity)
    /// - Find nearest neighbors for capability discovery
    /// - Support hybrid search (semantic + keyword)
    ///
    /// Implementations:
    /// - In-memory vector store (for development)
    /// - SQLite with vector extension (for local deployments)
    /// - PostgreSQL with pgvector (for production)
    /// - Pinecone, Weaviate, Qdrant (for scale)
    /// </summary>
    public interface IVectorStore
    {
        /// <summary>
        /// Name of the vector store implementation.
        ///
        /// Examples:
        /// - "In-Memory Vector Store"
        /// - "SQLite with Vector Extension"
        /// - "PostgreSQL with pgvector"
        /// - "Pinecone"
        /// </summary>
        string StoreName { get; }

        /// <summary>
        /// Expected dimensionality of vectors stored in this store.
        /// Must match the EmbeddingDimensions of the IEmbeddingProvider being used.
        ///
        /// Example: If using OpenAI text-embedding-3-small (1536 dimensions),
        /// this should be 1536.
        /// </summary>
        int VectorDimensions { get; }

        /// <summary>
        /// Adds a single embedding vector with metadata to the store.
        ///
        /// Use cases:
        /// - Index a new capability when plugin is loaded
        /// - Update capability embedding when description changes
        /// - Add usage example embeddings
        ///
        /// Metadata typically includes:
        /// - "capabilityId": Unique identifier for the capability
        /// - "pluginId": ID of the plugin providing this capability
        /// - "description": Human-readable description
        /// - "category": Plugin category (Pipeline, Storage, etc.)
        /// - "tags": Semantic tags for filtering
        /// </summary>
        /// <param name="id">Unique identifier for this vector entry.</param>
        /// <param name="embedding">Embedding vector (must have length = VectorDimensions).</param>
        /// <param name="metadata">Metadata to store with the vector.</param>
        /// <returns>Task representing the async operation.</returns>
        Task AddAsync(string id, float[] embedding, Dictionary<string, object> metadata);

        /// <summary>
        /// Adds multiple embedding vectors with metadata in a single batch.
        /// More efficient than calling AddAsync multiple times.
        ///
        /// Use cases:
        /// - Initial indexing of all plugins
        /// - Bulk updates of capability embeddings
        /// - Re-indexing after schema changes
        /// </summary>
        /// <param name="entries">List of vector entries to add.</param>
        /// <returns>Task representing the async operation.</returns>
        Task AddBatchAsync(List<VectorEntry> entries);

        /// <summary>
        /// Performs semantic search to find the most similar vectors.
        /// Uses cosine similarity to measure semantic closeness.
        ///
        /// Use cases:
        /// - Find capabilities matching natural language query
        /// - Discover similar capabilities
        /// - Recommend related plugins
        ///
        /// Process:
        /// 1. Compute cosine similarity between query vector and all stored vectors
        /// 2. Sort by similarity (descending)
        /// 3. Return top K results with similarity scores
        /// </summary>
        /// <param name="queryEmbedding">Query vector to search for.</param>
        /// <param name="topK">Number of results to return (default 10).</param>
        /// <param name="filters">Optional metadata filters (e.g., category="Pipeline").</param>
        /// <returns>List of search results with similarity scores.</returns>
        Task<List<VectorSearchResult>> SearchAsync(
            float[] queryEmbedding,
            int topK = 10,
            Dictionary<string, object>? filters = null);

        /// <summary>
        /// Updates an existing vector entry.
        /// Used when capability description changes or metadata is updated.
        /// </summary>
        /// <param name="id">ID of the vector entry to update.</param>
        /// <param name="embedding">New embedding vector.</param>
        /// <param name="metadata">New metadata.</param>
        /// <returns>Task representing the async operation.</returns>
        Task UpdateAsync(string id, float[] embedding, Dictionary<string, object> metadata);

        /// <summary>
        /// Deletes a vector entry from the store.
        /// Used when a plugin is unloaded or a capability is removed.
        /// </summary>
        /// <param name="id">ID of the vector entry to delete.</param>
        /// <returns>Task representing the async operation.</returns>
        Task DeleteAsync(string id);

        /// <summary>
        /// Retrieves a vector entry by ID.
        /// </summary>
        /// <param name="id">ID of the vector entry.</param>
        /// <returns>The vector entry, or null if not found.</returns>
        Task<VectorEntry?> GetByIdAsync(string id);

        /// <summary>
        /// Gets the total number of vectors stored.
        /// </summary>
        /// <returns>Count of stored vectors.</returns>
        Task<long> GetCountAsync();

        /// <summary>
        /// Clears all vectors from the store.
        /// Used for testing or complete re-indexing.
        /// </summary>
        /// <returns>Task representing the async operation.</returns>
        Task ClearAsync();
    }

    /// <summary>
    /// Represents a vector entry with ID, embedding, and metadata.
    /// </summary>
    public class VectorEntry
    {
        /// <summary>Unique identifier for this vector entry.</summary>
        public string Id { get; init; } = string.Empty;

        /// <summary>Embedding vector.</summary>
        public float[] Embedding { get; init; } = System.Array.Empty<float>();

        /// <summary>Metadata associated with this vector.</summary>
        public Dictionary<string, object> Metadata { get; init; } = new();

        /// <summary>Constructs an empty vector entry.</summary>
        public VectorEntry() { }

        /// <summary>Constructs a vector entry with specified values.</summary>
        public VectorEntry(string id, float[] embedding, Dictionary<string, object> metadata)
        {
            Id = id;
            Embedding = embedding;
            Metadata = metadata;
        }
    }

    /// <summary>
    /// Represents a search result with similarity score.
    /// </summary>
    public class VectorSearchResult
    {
        /// <summary>The vector entry that matched the query.</summary>
        public VectorEntry Entry { get; init; } = new();

        /// <summary>
        /// Cosine similarity score (0.0 to 1.0).
        /// Higher values indicate stronger semantic similarity.
        ///
        /// Typical interpretation:
        /// - 0.9 - 1.0: Nearly identical semantic meaning
        /// - 0.8 - 0.9: Very similar, likely relevant
        /// - 0.7 - 0.8: Somewhat similar, possibly relevant
        /// - 0.6 - 0.7: Loosely related
        /// - Below 0.6: Low relevance
        /// </summary>
        public float SimilarityScore { get; init; }

        /// <summary>Constructs an empty search result.</summary>
        public VectorSearchResult() { }

        /// <summary>Constructs a search result with specified values.</summary>
        public VectorSearchResult(VectorEntry entry, float similarityScore)
        {
            Entry = entry;
            SimilarityScore = similarityScore;
        }
    }
}
