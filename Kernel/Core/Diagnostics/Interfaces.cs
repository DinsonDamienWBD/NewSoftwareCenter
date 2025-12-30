using Core.Primitives;

namespace Core.Diagnostics
{
    /// <summary>
    /// Log severity levels.
    /// </summary>
    public enum LogLevel 
    {
        /// <summary>
        /// Debug-level messages, typically for development and troubleshooting.
        /// </summary>
        Debug,

        /// <summary>
        /// Informational messages that highlight the progress of the application.
        /// </summary>
        Info,

        /// <summary>
        /// Warning messages indicating a potential issue or important event.
        /// </summary>
        Warning,

        /// <summary>
        /// Error messages indicating a failure in the application.
        /// </summary>
        Error,

        /// <summary>
        /// Critical messages indicating a severe failure that requires immediate attention.
        /// </summary>
        Critical
    }

    /// <summary>
    /// Contract for the backend logging engine.
    /// </summary>
    public interface ILogProvider
    {
        /// <summary>
        /// Writes a log entry.
        /// </summary>
        /// <param name="level"></param>
        /// <param name="source"></param>
        /// <param name="message"></param>
        /// <param name="ex"></param>
        /// <param name="eventId"></param>
        void Write(LogLevel level, string source, string message, Exception? ex = null, int eventId = 0);
    }

    /// <summary>
    /// Rich Health Check Result and Cancellation
    /// </summary>
    /// <remarks>
    /// Constructor
    /// </remarks>
    /// <param name="status"></param>
    /// <param name="description"></param>
    /// <param name="latency"></param>
    /// <param name="exception"></param>
    public readonly struct HealthCheckResult(HealthStatus status, string description, TimeSpan latency, Exception? exception = null)
    {
        /// <summary>
        /// Status of the health check
        /// </summary>
        public HealthStatus Status { get; } = status;

        /// <summary>
        /// Description of the health check
        /// </summary>
        public string Description { get; } = description;

        /// <summary>
        /// Latency of the health check
        /// </summary>
        public TimeSpan Latency { get; } = latency;

        /// <summary>
        /// Any exceptions in the health check
        /// </summary>
        public Exception? Exception { get; } = exception;

        /// <summary>
        /// Health check data
        /// </summary>
        public IDictionary<string, object>? Data { get; } = null;

        /// <summary>
        /// Helper function to return healthy result
        /// </summary>
        /// <param name="description"></param>
        /// <param name="latency"></param>
        /// <returns></returns>
        public static HealthCheckResult Healthy(string description = "Healthy", TimeSpan latency = default)
            => new(HealthStatus.Healthy, description, latency);

        /// <summary>
        /// Helper function to return degraded result
        /// </summary>
        /// <param name="description"></param>
        /// <param name="ex"></param>
        /// <param name="latency"></param>
        /// <returns></returns>
        public static HealthCheckResult Degraded(string description, Exception? ex = null, TimeSpan latency = default)
            => new(HealthStatus.Degraded, description, latency, ex);

        /// <summary>
        /// Helper function to return unhealthy result
        /// </summary>
        /// <param name="description"></param>
        /// <param name="ex"></param>
        /// <param name="latency"></param>
        /// <returns></returns>
        public static HealthCheckResult Unhealthy(string description, Exception? ex = null, TimeSpan latency = default)
            => new(HealthStatus.Unhealthy, description, latency, ex);
    }

    /// <summary>
    /// Contract for health check probes.
    /// Modules expose these to let the Kernel know if they are functioning correctly.
    /// </summary>
    public interface IProbe
    {
        /// <summary>Name of the resource being checked (e.g., "MainDatabase").</summary>
        string Name { get; }

        /// <summary>Performs the health check logic.</summary>
        Task<HealthCheckResult> CheckHealthAsync(CancellationToken ct);
    }
}