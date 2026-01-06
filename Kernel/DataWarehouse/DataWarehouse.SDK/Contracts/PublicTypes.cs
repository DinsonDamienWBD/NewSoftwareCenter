namespace DataWarehouse.SDK.Contracts
{
    /// <summary>
    /// The standalone AI Contract
    /// </summary>
    public interface ISemanticMemory
    {
        /// <summary>
        /// Memorize
        /// </summary>
        /// <param name="content"></param>
        /// <param name="tags"></param>
        /// <param name="summary"></param>
        /// <returns></returns>
        Task<string> MemorizeAsync(string content, string[] tags, string? summary = null);

        /// <summary>
        /// Recall
        /// </summary>
        /// <param name="memoryId"></param>
        /// <returns></returns>
        Task<string> RecallAsync(string memoryId);

        /// <summary>
        /// Search
        /// </summary>
        /// <param name="query"></param>
        /// <param name="vector"></param>
        /// <param name="limit"></param>
        /// <returns></returns>
        Task<string[]> SearchMemoriesAsync(string query, float[]? vector, int limit = 5);
    }

    /// <summary>
    /// The standalone Metrics Contract
    /// </summary>
    public interface IMetricsProvider
    {
        /// <summary>
        /// Increment the counter
        /// </summary>
        /// <param name="metric"></param>
        void IncrementCounter(string metric);

        /// <summary>
        /// Record a metric
        /// </summary>
        /// <param name="metric"></param>
        /// <param name="value"></param>
        void RecordMetric(string metric, double value);

        /// <summary>
        /// Track duration
        /// </summary>
        /// <param name="metric"></param>
        /// <returns></returns>
        IDisposable TrackDuration(string metric);
    }

    /// <summary>
    /// Remote resource is not available
    /// </summary>
    /// <remarks>
    /// Remote resource unavailable exception
    /// </remarks>
    /// <param name="message"></param>
    /// <param name="inner"></param>
    public class RemoteResourceUnavailableException(string message, Exception inner) : IOException(message, inner)
    {
    }
}