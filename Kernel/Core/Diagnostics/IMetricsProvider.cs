using System;

namespace Core.Diagnostics
{
    /// <summary>
    /// Metrics provider
    /// Decoupled Observability.
    /// </summary>
    public interface IMetricsProvider
    {
        /// <summary>
        /// Record a metric (Gauge, Histogram, Distribution)
        /// </summary>
        /// <param name="metricName"></param>
        /// <param name="value"></param>
        /// <param name="tags"></param>
        void RecordMetric(string metricName, double value, params string[] tags);

        /// <summary>
        /// Increment counter
        /// </summary>
        /// <param name="metricName"></param>
        /// <param name="value">Amount to increment by (default 1)</param>
        /// <param name="tags"></param>
        void IncrementCounter(string metricName, int value = 1, params string[] tags);

        /// <summary>
        /// Track duration
        /// Returns a disposable timer.
        /// </summary>
        /// <param name="metricName"></param>
        IDisposable TrackDuration(string metricName);
    }
}