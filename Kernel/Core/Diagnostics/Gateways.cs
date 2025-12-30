using System.Runtime.CompilerServices;

namespace Core.Diagnostics
{
    /// <summary>
    /// Static Gateway for Logging. Decouples Modules from DI containers for basic logging needs.
    /// </summary>
    public static class SysLog
    {
        private static ILogProvider? _provider;

        /// <summary>Initializes the gateway with a specific provider (Called by Host).</summary>
        public static void Initialize(ILogProvider provider) => _provider = provider;

        /// <summary>
        /// Info Level Log Entry.
        /// </summary>
        public static void Info(string message, int eventId = 0, [CallerMemberName] string source = "")
            => _provider?.Write(LogLevel.Info, source, message, null, eventId);

        /// <summary>
        /// Error Level Log Entry.
        /// </summary>
        public static void Error(Exception ex, string message, int eventId = 0, [CallerMemberName] string source = "")
            => _provider?.Write(LogLevel.Error, source, message, ex, eventId);

        /// <summary>
        /// Warning Level Log Entry.
        /// </summary>
        public static void Warn(string message, int eventId = 0, [CallerMemberName] string source = "")
            => _provider?.Write(LogLevel.Warning, source, message, null, eventId);
    }

    /// <summary>
    /// Static Gateway for Metrics.
    /// </summary>
    public static class SysMetrics
    {
        private static IMetricsProvider? _provider;

        /// <summary>
        /// Initializes the gateway with a specific provider (Called by Host).
        /// </summary>
        /// <param name="provider"></param>
        public static void Initialize(IMetricsProvider provider) => _provider = provider;

        /// <summary>
        /// Count Metric.
        /// </summary>
        public static void Count(string metricName, int value = 1)
            => _provider?.IncrementCounter(metricName, value);

        /// <summary>
        /// Gauge Metric.
        /// </summary>
        public static void Gauge(string metricName, double value)
            => _provider?.RecordMetric(metricName, value);

        /// <summary>
        /// Time Metric (Duration in ms).
        /// </summary>
        public static void Time(string metricName, double ms)
            => _provider?.RecordMetric(metricName, ms);
    }
}