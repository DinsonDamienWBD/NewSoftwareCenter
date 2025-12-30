namespace Core.AI
{
    /// <summary>
    /// Represents the system's "Hippocampus" (Long-term semantic memory).
    /// </summary>
    public interface ISemanticMemory
    {
        /// <summary>
        /// Stores a concept or fact with semantic tags.
        /// </summary>
        /// <param name="content">The raw text content.</param>
        /// <param name="tags">Keywords for explicit filtering.</param>
        /// <param name="vector">Optional pre-computed vector (if Module has its own embedding model).</param>
        /// <returns>The Memory ID (URN).</returns>
        Task<string> MemorizeAsync(string content, string[] tags, float[]? vector = null);

        /// <summary>
        /// Stores a concept with semantic context.
        /// </summary>
        /// <param name="content">The raw text/data.</param>
        /// <param name="tags">Explicit human tags.</param>
        /// <param name="summary">AI-generated summary (optional).</param>
        /// <returns>The Memory ID (URN).</returns>
        Task<string> MemorizeAsync(string content, string[] tags, string? summary = null);

        /// <summary>
        /// Retrieves memories semantically related to the query.
        /// </summary>
        Task<string[]> RecallAsync(string query, int limit = 5);

        /// <summary>
        /// Retrieves the raw content of a memory.
        /// </summary>
        Task<string> RecallAsync(string memoryId);

        /// <summary>
        /// Retrieves memories strictly matching specific tags.
        /// </summary>
        Task<string[]> RecallByTagAsync(string tag);

        /// <summary>
        /// Finds memories via vector similarity (Simulated or Real).
        /// </summary>
        Task<string[]> SearchMemoriesAsync(string query, float[]? vector = null, int limit = 5);
    }
}