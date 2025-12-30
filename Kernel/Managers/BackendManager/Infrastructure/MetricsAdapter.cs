using DW = DataWarehouse;
using CD = Core.Diagnostics;

namespace Manager.Infrastructure
{
    /// <summary>
    /// Allows the Standalone DW to report metrics to the Core Host.
    /// </summary>
    public class MetricsAdapter : DW.IMetricsProvider
    {
        private readonly CD.IMetricsProvider _coreMetrics;

        public MetricsAdapter(CD.IMetricsProvider coreMetrics)
        {
            _coreMetrics = coreMetrics;
        }

        public void IncrementCounter(string metric) => _coreMetrics.IncrementCounter(metric);
        public void RecordMetric(string metric, double value) => _coreMetrics.RecordMetric(metric, value);
        public System.IDisposable TrackDuration(string metric) => _coreMetrics.TrackDuration(metric);
    }
}