using DataWarehouse.SDK.Contracts;
using System.Collections.Concurrent;
using System.Diagnostics;

namespace DataWarehouse.Kernel.Monitoring
{
    /// <summary>
    /// Real-time performance monitoring and metrics collection.
    /// Tracks system health, throughput, latency, and resource utilization.
    /// </summary>
    public class PerformanceMonitor
    {
        private readonly IKernelContext _context;
        private readonly ConcurrentDictionary<string, MetricTimeSeries> _metrics = new();
        private readonly ConcurrentDictionary<string, PerformanceCounter> _counters = new();
        private readonly object _lock = new();

        // Metrics Configuration
        private readonly TimeSpan _retentionPeriod = TimeSpan.FromHours(24);
        private readonly TimeSpan _aggregationInterval = TimeSpan.FromMinutes(1);

        public PerformanceMonitor(IKernelContext context)
        {
            _context = context ?? throw new ArgumentNullException(nameof(context));
            InitializeStandardMetrics();
        }

        private void InitializeStandardMetrics()
        {
            // System Metrics
            RegisterMetric("system.cpu.usage", MetricType.Gauge, "CPU Usage %");
            RegisterMetric("system.memory.used", MetricType.Gauge, "Memory Used MB");
            RegisterMetric("system.memory.available", MetricType.Gauge, "Memory Available MB");
            RegisterMetric("system.disk.read.bytes", MetricType.Counter, "Disk Read Bytes");
            RegisterMetric("system.disk.write.bytes", MetricType.Counter, "Disk Write Bytes");

            // Storage Metrics
            RegisterMetric("storage.operations.total", MetricType.Counter, "Total Storage Operations");
            RegisterMetric("storage.operations.success", MetricType.Counter, "Successful Operations");
            RegisterMetric("storage.operations.failed", MetricType.Counter, "Failed Operations");
            RegisterMetric("storage.latency.avg", MetricType.Gauge, "Average Latency ms");
            RegisterMetric("storage.latency.p95", MetricType.Gauge, "95th Percentile Latency ms");
            RegisterMetric("storage.latency.p99", MetricType.Gauge, "99th Percentile Latency ms");
            RegisterMetric("storage.throughput.read", MetricType.Gauge, "Read Throughput MB/s");
            RegisterMetric("storage.throughput.write", MetricType.Gauge, "Write Throughput MB/s");

            // RAID Metrics
            RegisterMetric("raid.rebuild.progress", MetricType.Gauge, "RAID Rebuild Progress %");
            RegisterMetric("raid.degraded.arrays", MetricType.Gauge, "Degraded RAID Arrays");
            RegisterMetric("raid.failed.disks", MetricType.Counter, "Failed Disks");

            // Plugin Metrics
            RegisterMetric("plugins.loaded", MetricType.Gauge, "Loaded Plugins");
            RegisterMetric("plugins.initializing", MetricType.Gauge, "Initializing Plugins");
            RegisterMetric("plugins.failed", MetricType.Counter, "Failed Plugin Loads");

            // Network Metrics (for distributed mode)
            RegisterMetric("network.requests.total", MetricType.Counter, "Total Network Requests");
            RegisterMetric("network.requests.success", MetricType.Counter, "Successful Requests");
            RegisterMetric("network.bandwidth.inbound", MetricType.Gauge, "Inbound Bandwidth MB/s");
            RegisterMetric("network.bandwidth.outbound", MetricType.Gauge, "Outbound Bandwidth MB/s");

            // Cache Metrics
            RegisterMetric("cache.hits", MetricType.Counter, "Cache Hits");
            RegisterMetric("cache.misses", MetricType.Counter, "Cache Misses");
            RegisterMetric("cache.evictions", MetricType.Counter, "Cache Evictions");
            RegisterMetric("cache.size.bytes", MetricType.Gauge, "Cache Size Bytes");
        }

        /// <summary>
        /// Register a new metric for tracking.
        /// </summary>
        public void RegisterMetric(string name, MetricType type, string description)
        {
            _metrics.TryAdd(name, new MetricTimeSeries
            {
                Name = name,
                Type = type,
                Description = description,
                RetentionPeriod = _retentionPeriod
            });
        }

        /// <summary>
        /// Record a metric value.
        /// </summary>
        public void RecordMetric(string name, double value, Dictionary<string, string>? tags = null)
        {
            if (_metrics.TryGetValue(name, out var metric))
            {
                metric.AddDataPoint(new MetricDataPoint
                {
                    Timestamp = DateTime.UtcNow,
                    Value = value,
                    Tags = tags ?? new Dictionary<string, string>()
                });
            }
        }

        /// <summary>
        /// Increment a counter metric.
        /// </summary>
        public void IncrementCounter(string name, double delta = 1.0, Dictionary<string, string>? tags = null)
        {
            RecordMetric(name, delta, tags);
        }

        /// <summary>
        /// Record operation timing.
        /// </summary>
        public void RecordTiming(string operation, TimeSpan duration, Dictionary<string, string>? tags = null)
        {
            RecordMetric($"{operation}.duration", duration.TotalMilliseconds, tags);
            IncrementCounter($"{operation}.count", 1.0, tags);
        }

        /// <summary>
        /// Start a performance counter for an operation.
        /// </summary>
        public IDisposable MeasureOperation(string operation, Dictionary<string, string>? tags = null)
        {
            return new OperationTimer(this, operation, tags);
        }

        /// <summary>
        /// Get current snapshot of all metrics.
        /// </summary>
        public PerformanceSnapshot GetSnapshot()
        {
            var snapshot = new PerformanceSnapshot
            {
                Timestamp = DateTime.UtcNow,
                Metrics = new Dictionary<string, MetricSnapshot>()
            };

            foreach (var (name, timeSeries) in _metrics)
            {
                var recent = timeSeries.GetRecentDataPoints(TimeSpan.FromMinutes(5));
                if (recent.Any())
                {
                    snapshot.Metrics[name] = new MetricSnapshot
                    {
                        Name = name,
                        Type = timeSeries.Type,
                        Current = recent.Last().Value,
                        Average = recent.Average(dp => dp.Value),
                        Min = recent.Min(dp => dp.Value),
                        Max = recent.Max(dp => dp.Value),
                        Count = recent.Count
                    };
                }
            }

            return snapshot;
        }

        /// <summary>
        /// Get metric history for a specific time range.
        /// </summary>
        public List<MetricDataPoint> GetMetricHistory(string name, TimeSpan lookback)
        {
            if (_metrics.TryGetValue(name, out var metric))
            {
                return metric.GetRecentDataPoints(lookback);
            }
            return [];
        }

        /// <summary>
        /// Get aggregated statistics for a metric.
        /// </summary>
        public MetricStatistics GetMetricStatistics(string name, TimeSpan period)
        {
            if (!_metrics.TryGetValue(name, out var metric))
            {
                throw new ArgumentException($"Metric '{name}' not found");
            }

            var dataPoints = metric.GetRecentDataPoints(period);
            if (!dataPoints.Any())
            {
                return new MetricStatistics { Name = name, DataPointCount = 0 };
            }

            var values = dataPoints.Select(dp => dp.Value).OrderBy(v => v).ToList();

            return new MetricStatistics
            {
                Name = name,
                DataPointCount = values.Count,
                Min = values.First(),
                Max = values.Last(),
                Average = values.Average(),
                Median = GetPercentile(values, 50),
                P95 = GetPercentile(values, 95),
                P99 = GetPercentile(values, 99),
                StandardDeviation = CalculateStandardDeviation(values)
            };
        }

        /// <summary>
        /// Generate health score based on current metrics.
        /// </summary>
        public HealthScore CalculateHealthScore()
        {
            var score = 100.0;
            var issues = new List<string>();

            // Check CPU usage
            var cpuMetric = GetMetricStatistics("system.cpu.usage", TimeSpan.FromMinutes(5));
            if (cpuMetric.Average > 90)
            {
                score -= 20;
                issues.Add("High CPU usage");
            }
            else if (cpuMetric.Average > 75)
            {
                score -= 10;
                issues.Add("Elevated CPU usage");
            }

            // Check memory
            var memUsed = GetLatestValue("system.memory.used");
            var memAvail = GetLatestValue("system.memory.available");
            if (memAvail.HasValue && memAvail.Value < 512) // Less than 512MB available
            {
                score -= 25;
                issues.Add("Low memory available");
            }

            // Check storage failures
            var storageFailures = GetMetricStatistics("storage.operations.failed", TimeSpan.FromMinutes(5));
            var storageTotal = GetMetricStatistics("storage.operations.total", TimeSpan.FromMinutes(5));
            if (storageTotal.DataPointCount > 0)
            {
                double failureRate = storageFailures.DataPointCount / (double)storageTotal.DataPointCount;
                if (failureRate > 0.1) // More than 10% failures
                {
                    score -= 30;
                    issues.Add($"High storage failure rate: {failureRate:P1}");
                }
                else if (failureRate > 0.05)
                {
                    score -= 15;
                    issues.Add($"Elevated storage failure rate: {failureRate:P1}");
                }
            }

            // Check RAID health
            var degradedArrays = GetLatestValue("raid.degraded.arrays");
            if (degradedArrays.HasValue && degradedArrays.Value > 0)
            {
                score -= 35;
                issues.Add($"Degraded RAID arrays detected: {degradedArrays.Value}");
            }

            // Check latency
            var latencyP99 = GetLatestValue("storage.latency.p99");
            if (latencyP99.HasValue && latencyP99.Value > 1000) // More than 1 second
            {
                score -= 15;
                issues.Add("High storage latency");
            }

            return new HealthScore
            {
                Score = Math.Max(0, score),
                Status = score >= 90 ? HealthStatus.Healthy :
                         score >= 70 ? HealthStatus.Degraded :
                         score >= 50 ? HealthStatus.Warning :
                         HealthStatus.Critical,
                Issues = issues,
                Timestamp = DateTime.UtcNow
            };
        }

        private double? GetLatestValue(string metricName)
        {
            if (_metrics.TryGetValue(metricName, out var metric))
            {
                var recent = metric.GetRecentDataPoints(TimeSpan.FromMinutes(1));
                return recent.LastOrDefault()?.Value;
            }
            return null;
        }

        private static double GetPercentile(List<double> sortedValues, int percentile)
        {
            if (sortedValues.Count == 0) return 0;
            int index = (int)Math.Ceiling(sortedValues.Count * percentile / 100.0) - 1;
            index = Math.Max(0, Math.Min(sortedValues.Count - 1, index));
            return sortedValues[index];
        }

        private static double CalculateStandardDeviation(List<double> values)
        {
            if (values.Count < 2) return 0;
            double avg = values.Average();
            double sumOfSquares = values.Sum(v => Math.Pow(v - avg, 2));
            return Math.Sqrt(sumOfSquares / (values.Count - 1));
        }

        /// <summary>
        /// Cleanup old data points beyond retention period.
        /// </summary>
        public void PruneOldData()
        {
            foreach (var metric in _metrics.Values)
            {
                metric.PruneOldData();
            }
        }

        private class OperationTimer : IDisposable
        {
            private readonly PerformanceMonitor _monitor;
            private readonly string _operation;
            private readonly Dictionary<string, string>? _tags;
            private readonly Stopwatch _stopwatch;

            public OperationTimer(PerformanceMonitor monitor, string operation, Dictionary<string, string>? tags)
            {
                _monitor = monitor;
                _operation = operation;
                _tags = tags;
                _stopwatch = Stopwatch.StartNew();
            }

            public void Dispose()
            {
                _stopwatch.Stop();
                _monitor.RecordTiming(_operation, _stopwatch.Elapsed, _tags);
            }
        }
    }

    /// <summary>
    /// Time series data for a single metric.
    /// </summary>
    public class MetricTimeSeries
    {
        public required string Name { get; init; }
        public required MetricType Type { get; init; }
        public required string Description { get; init; }
        public TimeSpan RetentionPeriod { get; init; } = TimeSpan.FromHours(24);

        private readonly List<MetricDataPoint> _dataPoints = [];
        private readonly object _lock = new();

        public void AddDataPoint(MetricDataPoint point)
        {
            lock (_lock)
            {
                _dataPoints.Add(point);
            }
        }

        public List<MetricDataPoint> GetRecentDataPoints(TimeSpan lookback)
        {
            var cutoff = DateTime.UtcNow - lookback;
            lock (_lock)
            {
                return _dataPoints.Where(dp => dp.Timestamp >= cutoff).ToList();
            }
        }

        public void PruneOldData()
        {
            var cutoff = DateTime.UtcNow - RetentionPeriod;
            lock (_lock)
            {
                _dataPoints.RemoveAll(dp => dp.Timestamp < cutoff);
            }
        }
    }

    public class MetricDataPoint
    {
        public DateTime Timestamp { get; init; }
        public double Value { get; init; }
        public Dictionary<string, string> Tags { get; init; } = new();
    }

    public enum MetricType
    {
        Counter,  // Monotonically increasing value
        Gauge,    // Point-in-time value that can go up or down
        Histogram // Distribution of values
    }

    public class PerformanceSnapshot
    {
        public DateTime Timestamp { get; init; }
        public Dictionary<string, MetricSnapshot> Metrics { get; init; } = new();
    }

    public class MetricSnapshot
    {
        public required string Name { get; init; }
        public MetricType Type { get; init; }
        public double Current { get; init; }
        public double Average { get; init; }
        public double Min { get; init; }
        public double Max { get; init; }
        public int Count { get; init; }
    }

    public class MetricStatistics
    {
        public required string Name { get; init; }
        public int DataPointCount { get; init; }
        public double Min { get; init; }
        public double Max { get; init; }
        public double Average { get; init; }
        public double Median { get; init; }
        public double P95 { get; init; }
        public double P99 { get; init; }
        public double StandardDeviation { get; init; }
    }

    public class HealthScore
    {
        public double Score { get; init; }
        public HealthStatus Status { get; init; }
        public List<string> Issues { get; init; } = [];
        public DateTime Timestamp { get; init; }
    }

    public enum HealthStatus
    {
        Healthy,
        Degraded,
        Warning,
        Critical
    }
}
