using System.Collections.Generic;
using System.Threading.Tasks;

namespace DataWarehouse.SDK.AI.Vector
{
    /// <summary>
    /// Interface for generating text embeddings using various embedding models.
    /// Embeddings are dense vector representations of text that capture semantic meaning.
    ///
    /// Used by AI Runtime to:
    /// - Generate embeddings for capability descriptions
    /// - Enable semantic search over capabilities
    /// - Find similar capabilities and patterns
    /// - Support natural language queries
    ///
    /// Implementations:
    /// - OpenAI Embeddings (text-embedding-ada-002, text-embedding-3-small, etc.)
    /// - Sentence Transformers (all-MiniLM-L6-v2, etc.)
    /// - Local models (via Ollama, llama.cpp, etc.)
    /// - Azure OpenAI Embeddings
    /// </summary>
    public interface IEmbeddingProvider
    {
        /// <summary>
        /// Name of the embedding provider.
        ///
        /// Examples:
        /// - "OpenAI (text-embedding-3-small)"
        /// - "SentenceTransformers (all-MiniLM-L6-v2)"
        /// - "Ollama (nomic-embed-text)"
        /// </summary>
        string ProviderName { get; }

        /// <summary>
        /// Dimensionality of the embedding vectors produced by this provider.
        ///
        /// Common dimensions:
        /// - OpenAI text-embedding-3-small: 1536 dimensions
        /// - OpenAI text-embedding-ada-002: 1536 dimensions
        /// - Sentence Transformers all-MiniLM-L6-v2: 384 dimensions
        /// - Nomic Embed Text: 768 dimensions
        ///
        /// Higher dimensions can capture more nuance but require more storage and computation.
        /// </summary>
        int EmbeddingDimensions { get; }

        /// <summary>
        /// Maximum number of tokens the model can process in a single input.
        ///
        /// Examples:
        /// - OpenAI text-embedding-3-small: 8191 tokens
        /// - Sentence Transformers: 256-512 tokens (varies by model)
        ///
        /// Text exceeding this limit should be truncated or chunked.
        /// </summary>
        int MaxTokens { get; }

        /// <summary>
        /// Generates an embedding vector for a single text input.
        ///
        /// The embedding captures the semantic meaning of the text in a dense vector format.
        /// Similar texts will have embeddings that are close together in vector space (high cosine similarity).
        ///
        /// Use cases:
        /// - Generate embedding for a capability description
        /// - Generate embedding for a natural language query
        /// - Generate embedding for usage example text
        /// </summary>
        /// <param name="text">Text to embed (will be truncated if exceeds MaxTokens).</param>
        /// <returns>Embedding vector (array of floats with length = EmbeddingDimensions).</returns>
        Task<float[]> GenerateEmbeddingAsync(string text);

        /// <summary>
        /// Generates embedding vectors for multiple text inputs in a single batch.
        /// More efficient than calling GenerateEmbeddingAsync multiple times.
        ///
        /// Use cases:
        /// - Generate embeddings for all capability descriptions at once
        /// - Batch processing of usage examples
        /// - Initial indexing of all plugins
        ///
        /// Note: Some providers have batch size limits (e.g., OpenAI: 2048 inputs per batch).
        /// </summary>
        /// <param name="texts">List of texts to embed.</param>
        /// <returns>List of embedding vectors (each of length EmbeddingDimensions).</returns>
        Task<List<float[]>> GenerateEmbeddingsBatchAsync(List<string> texts);

        /// <summary>
        /// Estimates the cost of generating embeddings for the given text.
        ///
        /// Cost factors:
        /// - Number of tokens in the text
        /// - Provider pricing (e.g., OpenAI charges per million tokens)
        /// - Local models typically have zero API cost
        ///
        /// Returns:
        /// - Cost in USD for API-based providers
        /// - 0 for local models
        /// - Estimated compute cost for self-hosted models
        /// </summary>
        /// <param name="text">Text to estimate cost for.</param>
        /// <returns>Estimated cost in USD.</returns>
        decimal EstimateCost(string text);

        /// <summary>
        /// Checks if the provider is available and healthy.
        ///
        /// Health checks:
        /// - API key is valid (for API providers)
        /// - Model is loaded and ready (for local providers)
        /// - Network connectivity is working
        /// - Rate limits are not exceeded
        /// </summary>
        /// <returns>True if provider is healthy, false otherwise.</returns>
        Task<bool> IsHealthyAsync();
    }
}
