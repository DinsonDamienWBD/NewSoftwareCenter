// NOTE: These are now NATIVE to DataWarehouse. 
// A user downloading "Cosmic.DataWarehouse" from NuGet gets these.

namespace DataWarehouse // Root Namespace
{
    /// <summary>
    /// Level of security to be used
    /// </summary>
    public enum SecurityLevel
    {
        /// <summary>
        /// No security
        /// </summary>
        None,

        /// <summary>
        /// Standard security level
        /// </summary>
        Standard,

        /// <summary>
        /// High level of security
        /// </summary>
        High,

        /// <summary>
        /// Top level security
        /// </summary>
        Quantum
    }

    /// <summary>
    /// Level of compression to b∈ used
    /// </summary>
    public enum CompressionLevel
    {
        /// <summary>
        /// No compression
        /// </summary>
        None,

        /// <summary>
        /// Fast but less compression
        /// </summary>
        Fast,

        /// <summary>
        /// Optimal balance between compression and performance
        /// </summary>
        Optimal
    }

    /// <summary>
    /// Level of availability
    /// </summary>
    public enum AvailabilityLevel
    {
        /// <summary>
        /// Single copy, no redundancy
        /// </summary>
        Single,

        /// <summary>
        /// Redundancy applied
        /// </summary>
        Redundant,

        /// <summary>
        /// Geo level redundancy applied
        /// </summary>
        GeoRedundant
    }

    /// <summary>
    /// A storage intent record
    /// </summary>
    /// <param name="Security"></param>
    /// <param name="Compression"></param>
    /// <param name="Availability"></param>
    public record StorageIntent(
        SecurityLevel Security = SecurityLevel.Standard,
        CompressionLevel Compression = CompressionLevel.Optimal,
        AvailabilityLevel Availability = AvailabilityLevel.Single
    );

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