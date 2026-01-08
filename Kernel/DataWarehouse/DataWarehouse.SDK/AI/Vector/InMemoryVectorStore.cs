namespace DataWarehouse.SDK.AI.Vector
{
    /// <summary>
    /// In-memory implementation of IVectorStore.
    /// Stores vectors in memory for fast access. Data is lost when process stops.
    ///
    /// Use cases:
    /// - Development and testing
    /// - Small deployments (< 10,000 vectors)
    /// - Single-process applications
    /// - Prototyping
    ///
    /// Limitations:
    /// - No persistence (data lost on restart)
    /// - Limited to available RAM
    /// - No clustering or sharding
    /// - Linear search (O(n) complexity)
    ///
    /// For production with large datasets, use:
    /// - SQLite with vector extension
    /// - PostgreSQL with pgvector
    /// - Dedicated vector databases (Pinecone, Weaviate, Qdrant)
    /// </summary>
    public class InMemoryVectorStore : IVectorStore
    {
        private readonly Dictionary<string, VectorEntry> _vectors = [];
        private readonly Lock _lock = new();
        private readonly int _dimensions;

        /// <summary>
        /// Constructs an in-memory vector store with specified dimensions.
        /// </summary>
        /// <param name="dimensions">Expected vector dimensionality (must match embedding provider).</param>
        public InMemoryVectorStore(int dimensions)
        {
            if (dimensions <= 0)
                throw new ArgumentException("Dimensions must be positive", nameof(dimensions));

            _dimensions = dimensions;
        }

        /// <summary>Name of the vector store implementation.</summary>
        public string StoreName => "In-Memory Vector Store";

        /// <summary>Vector dimensionality.</summary>
        public int VectorDimensions => _dimensions;

        /// <summary>Adds a vector with metadata to the store.</summary>
        public Task AddAsync(string id, float[] embedding, Dictionary<string, object> metadata)
        {
            if (string.IsNullOrWhiteSpace(id))
                throw new ArgumentException("ID cannot be empty", nameof(id));
            ArgumentNullException.ThrowIfNull(embedding);
            if (embedding.Length != _dimensions)
                throw new ArgumentException($"Embedding dimension mismatch: expected {_dimensions}, got {embedding.Length}");

            lock (_lock)
            {
                _vectors[id] = new VectorEntry(id, embedding, metadata ?? []);
            }

            return Task.CompletedTask;
        }

        /// <summary>Adds multiple vectors in a batch.</summary>
        public Task AddBatchAsync(List<VectorEntry> entries)
        {
            ArgumentNullException.ThrowIfNull(entries);

            lock (_lock)
            {
                foreach (var entry in entries)
                {
                    if (entry.Embedding.Length != _dimensions)
                        throw new ArgumentException($"Embedding dimension mismatch for ID '{entry.Id}': expected {_dimensions}, got {entry.Embedding.Length}");

                    _vectors[entry.Id] = entry;
                }
            }

            return Task.CompletedTask;
        }

        /// <summary>Performs semantic search using cosine similarity.</summary>
        public Task<List<VectorSearchResult>> SearchAsync(
            float[] queryEmbedding,
            int topK = 10,
            Dictionary<string, object>? filters = null)
        {
            ArgumentNullException.ThrowIfNull(queryEmbedding);
            if (queryEmbedding.Length != _dimensions)
                throw new ArgumentException($"Query embedding dimension mismatch: expected {_dimensions}, got {queryEmbedding.Length}");
            if (topK <= 0)
                throw new ArgumentException("topK must be positive", nameof(topK));

            List<VectorSearchResult> results;

            lock (_lock)
            {
                // Get all vectors (or filtered vectors)
                var candidates = _vectors.Values.AsEnumerable();

                // Apply metadata filters if specified
                if (filters != null && filters.Count > 0)
                {
                    candidates = ApplyFilters(candidates, filters);
                }

                // Compute similarities and sort
                results = [.. candidates
                    .Select(entry => new VectorSearchResult(
                        entry,
                        VectorMath.CosineSimilarity(queryEmbedding, entry.Embedding)
                    ))
                    .OrderByDescending(r => r.SimilarityScore)
                    .Take(topK)];
            }

            return Task.FromResult(results);
        }

        /// <summary>Updates an existing vector entry.</summary>
        public Task UpdateAsync(string id, float[] embedding, Dictionary<string, object> metadata)
        {
            if (string.IsNullOrWhiteSpace(id))
                throw new ArgumentException("ID cannot be empty", nameof(id));
            ArgumentNullException.ThrowIfNull(embedding);
            if (embedding.Length != _dimensions)
                throw new ArgumentException($"Embedding dimension mismatch: expected {_dimensions}, got {embedding.Length}");

            lock (_lock)
            {
                if (!_vectors.ContainsKey(id))
                    throw new KeyNotFoundException($"Vector with ID '{id}' not found");

                _vectors[id] = new VectorEntry(id, embedding, metadata ?? []);
            }

            return Task.CompletedTask;
        }

        /// <summary>Deletes a vector entry.</summary>
        public Task DeleteAsync(string id)
        {
            if (string.IsNullOrWhiteSpace(id))
                throw new ArgumentException("ID cannot be empty", nameof(id));

            lock (_lock)
            {
                _vectors.Remove(id);
            }

            return Task.CompletedTask;
        }

        /// <summary>Retrieves a vector entry by ID.</summary>
        public Task<VectorEntry?> GetByIdAsync(string id)
        {
            if (string.IsNullOrWhiteSpace(id))
                throw new ArgumentException("ID cannot be empty", nameof(id));

            lock (_lock)
            {
                return Task.FromResult(_vectors.TryGetValue(id, out var entry) ? entry : null);
            }
        }

        /// <summary>Gets the total number of vectors stored.</summary>
        public Task<long> GetCountAsync()
        {
            lock (_lock)
            {
                return Task.FromResult((long)_vectors.Count);
            }
        }

        /// <summary>Clears all vectors from the store.</summary>
        public Task ClearAsync()
        {
            lock (_lock)
            {
                _vectors.Clear();
            }

            return Task.CompletedTask;
        }

        /// <summary>
        /// Applies metadata filters to vector entries.
        /// Filters are applied as AND conditions (all must match).
        /// </summary>
        private static IEnumerable<VectorEntry> ApplyFilters(
            IEnumerable<VectorEntry> entries,
            Dictionary<string, object> filters)
        {
            return entries.Where(entry =>
            {
                foreach (var filter in filters)
                {
                    // Check if metadata contains the key
                    if (!entry.Metadata.TryGetValue(filter.Key, out var value))
                        return false;

                    // Check if values match
                    if (!Equals(value, filter.Value))
                        return false;
                }
                return true;
            });
        }
    }
}
