using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using DataWarehouse.SDK.AI.Vector;
using DataWarehouse.SDK.Contracts;

namespace DataWarehouse.SDK.AI.Runtime
{
    /// <summary>
    /// Indexes plugin capabilities for semantic search.
    /// Auto-generates and maintains embeddings for all capabilities.
    ///
    /// Used by AI Runtime to:
    /// - Index capabilities when plugins load
    /// - Enable semantic search over capabilities
    /// - Discover capabilities from natural language
    /// - Find similar capabilities
    /// - Group capabilities by semantic similarity
    ///
    /// Features:
    /// - Automatic indexing on plugin load
    /// - Incremental updates
    /// - Batch indexing for performance
    /// - Semantic grouping and clustering
    /// </summary>
    public class CapabilityIndex
    {
        private readonly IVectorStore _vectorStore;
        private readonly IEmbeddingProvider _embeddings;
        private readonly Dictionary<string, CapabilityIndexEntry> _entries = new();
        private readonly object _lock = new();

        public CapabilityIndex(IVectorStore vectorStore, IEmbeddingProvider embeddingProvider)
        {
            _vectorStore = vectorStore ?? throw new ArgumentNullException(nameof(vectorStore));
            _embeddings = embeddingProvider ?? throw new ArgumentNullException(nameof(embeddingProvider));
        }

        /// <summary>
        /// Indexes a single capability.
        /// Generates embedding from description and tags.
        /// </summary>
        /// <param name="capability">Capability to index.</param>
        /// <param name="pluginId">Plugin providing this capability.</param>
        /// <param name="semanticDescription">Semantic description for embedding.</param>
        /// <param name="tags">Semantic tags.</param>
        public async Task IndexCapabilityAsync(
            PluginCapabilityDescriptor capability,
            string pluginId,
            string semanticDescription,
            string[] tags)
        {
            if (capability == null)
                throw new ArgumentNullException(nameof(capability));

            // Generate text for embedding
            var embeddingText = BuildEmbeddingText(capability, semanticDescription, tags);

            // Generate embedding
            var embedding = await _embeddings.GenerateEmbeddingAsync(embeddingText);

            // Prepare metadata
            var metadata = new Dictionary<string, object>
            {
                ["capabilityId"] = capability.Id,
                ["pluginId"] = pluginId,
                ["description"] = capability.Description,
                ["semanticDescription"] = semanticDescription,
                ["tags"] = string.Join(",", tags),
                ["indexed"] = DateTime.UtcNow.ToString("O")
            };

            // Add to vector store
            await _vectorStore.AddAsync(capability.Id, embedding, metadata);

            // Track locally
            lock (_lock)
            {
                _entries[capability.Id] = new CapabilityIndexEntry
                {
                    CapabilityId = capability.Id,
                    PluginId = pluginId,
                    Embedding = embedding,
                    SemanticDescription = semanticDescription,
                    Tags = tags.ToList(),
                    IndexedAt = DateTime.UtcNow
                };
            }
        }

        /// <summary>
        /// Indexes multiple capabilities in batch.
        /// More efficient than indexing one at a time.
        ///
        /// Use cases:
        /// - Initial indexing when DataWarehouse starts
        /// - Bulk re-indexing after updates
        /// </summary>
        /// <param name="capabilities">List of capabilities with metadata to index.</param>
        public async Task IndexCapabilitiesBatchAsync(List<CapabilityIndexRequest> capabilities)
        {
            if (capabilities == null || capabilities.Count == 0)
                return;

            // Generate all embeddings in batch
            var embeddingTexts = capabilities
                .Select(c => BuildEmbeddingText(c.Capability, c.SemanticDescription, c.Tags))
                .ToList();

            var embeddings = await _embeddings.GenerateEmbeddingsBatchAsync(embeddingTexts);

            // Prepare vector entries
            var vectorEntries = new List<VectorEntry>();
            for (int i = 0; i < capabilities.Count; i++)
            {
                var cap = capabilities[i];
                var metadata = new Dictionary<string, object>
                {
                    ["capabilityId"] = cap.Capability.Id,
                    ["pluginId"] = cap.PluginId,
                    ["description"] = cap.Capability.Description,
                    ["semanticDescription"] = cap.SemanticDescription,
                    ["tags"] = string.Join(",", cap.Tags),
                    ["indexed"] = DateTime.UtcNow.ToString("O")
                };

                vectorEntries.Add(new VectorEntry(cap.Capability.Id, embeddings[i], metadata));

                // Track locally
                lock (_lock)
                {
                    _entries[cap.Capability.Id] = new CapabilityIndexEntry
                    {
                        CapabilityId = cap.Capability.Id,
                        PluginId = cap.PluginId,
                        Embedding = embeddings[i],
                        SemanticDescription = cap.SemanticDescription,
                        Tags = cap.Tags.ToList(),
                        IndexedAt = DateTime.UtcNow
                    };
                }
            }

            // Batch add to vector store
            await _vectorStore.AddBatchAsync(vectorEntries);
        }

        /// <summary>
        /// Removes a capability from the index.
        /// Called when a plugin is unloaded.
        /// </summary>
        /// <param name="capabilityId">Capability ID to remove.</param>
        public async Task RemoveCapabilityAsync(string capabilityId)
        {
            await _vectorStore.DeleteAsync(capabilityId);

            lock (_lock)
            {
                _entries.Remove(capabilityId);
            }
        }

        /// <summary>
        /// Searches for capabilities matching a natural language query.
        /// </summary>
        /// <param name="query">Natural language query.</param>
        /// <param name="topK">Number of results to return.</param>
        /// <param name="filters">Optional metadata filters.</param>
        /// <returns>List of matching capabilities with similarity scores.</returns>
        public async Task<List<CapabilitySearchResult>> SearchAsync(
            string query,
            int topK = 10,
            Dictionary<string, object>? filters = null)
        {
            // Generate query embedding
            var queryEmbedding = await _embeddings.GenerateEmbeddingAsync(query);

            // Search vector store
            var results = await _vectorStore.SearchAsync(queryEmbedding, topK, filters);

            // Convert to capability search results
            return results.Select(r => new CapabilitySearchResult
            {
                CapabilityId = r.Entry.Id,
                PluginId = r.Entry.Metadata.TryGetValue("pluginId", out var pid) ? pid.ToString() ?? "" : "",
                Description = r.Entry.Metadata.TryGetValue("description", out var desc) ? desc.ToString() ?? "" : "",
                SemanticDescription = r.Entry.Metadata.TryGetValue("semanticDescription", out var sdesc) ? sdesc.ToString() ?? "" : "",
                SimilarityScore = r.SimilarityScore
            }).ToList();
        }

        /// <summary>
        /// Finds capabilities similar to a given capability.
        /// Useful for discovering alternatives.
        /// </summary>
        /// <param name="capabilityId">Reference capability ID.</param>
        /// <param name="topK">Number of similar capabilities to return.</param>
        /// <returns>List of similar capabilities.</returns>
        public async Task<List<CapabilitySearchResult>> FindSimilarAsync(string capabilityId, int topK = 5)
        {
            // Get the capability's embedding
            var entry = await _vectorStore.GetByIdAsync(capabilityId);
            if (entry == null)
                throw new ArgumentException($"Capability '{capabilityId}' not found in index");

            // Search using this embedding
            var results = await _vectorStore.SearchAsync(entry.Embedding, topK + 1); // +1 to exclude self

            // Remove self and convert to results
            return results
                .Where(r => r.Entry.Id != capabilityId)
                .Take(topK)
                .Select(r => new CapabilitySearchResult
                {
                    CapabilityId = r.Entry.Id,
                    PluginId = r.Entry.Metadata.TryGetValue("pluginId", out var pid) ? pid.ToString() ?? "" : "",
                    Description = r.Entry.Metadata.TryGetValue("description", out var desc) ? desc.ToString() ?? "" : "",
                    SemanticDescription = r.Entry.Metadata.TryGetValue("semanticDescription", out var sdesc) ? sdesc.ToString() ?? "" : "",
                    SimilarityScore = r.SimilarityScore
                })
                .ToList();
        }

        /// <summary>
        /// Gets total count of indexed capabilities.
        /// </summary>
        public async Task<long> GetCountAsync()
        {
            return await _vectorStore.GetCountAsync();
        }

        /// <summary>
        /// Clears the entire index.
        /// Used for testing or complete re-indexing.
        /// </summary>
        public async Task ClearAsync()
        {
            await _vectorStore.ClearAsync();

            lock (_lock)
            {
                _entries.Clear();
            }
        }

        /// <summary>
        /// Builds text for embedding generation.
        /// Combines description and tags into optimized text.
        /// </summary>
        private string BuildEmbeddingText(
            PluginCapabilityDescriptor capability,
            string semanticDescription,
            string[] tags)
        {
            var text = $"{capability.Id}\n\n";
            text += $"{semanticDescription}\n\n";
            text += $"Description: {capability.Description}\n\n";
            text += $"Tags: {string.Join(", ", tags)}\n";

            return text;
        }
    }

    /// <summary>
    /// Request to index a capability.
    /// </summary>
    public class CapabilityIndexRequest
    {
        public PluginCapabilityDescriptor Capability { get; init; } = new();
        public string PluginId { get; init; } = string.Empty;
        public string SemanticDescription { get; init; } = string.Empty;
        public string[] Tags { get; init; } = Array.Empty<string>();
    }

    /// <summary>
    /// Entry in the capability index.
    /// </summary>
    public class CapabilityIndexEntry
    {
        public string CapabilityId { get; set; } = string.Empty;
        public string PluginId { get; set; } = string.Empty;
        public float[] Embedding { get; set; } = Array.Empty<float>();
        public string SemanticDescription { get; set; } = string.Empty;
        public List<string> Tags { get; set; } = new();
        public DateTime IndexedAt { get; set; }
    }

    /// <summary>
    /// Result of capability search.
    /// </summary>
    public class CapabilitySearchResult
    {
        public string CapabilityId { get; set; } = string.Empty;
        public string PluginId { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public string SemanticDescription { get; set; } = string.Empty;
        public float SimilarityScore { get; set; }
    }
}
