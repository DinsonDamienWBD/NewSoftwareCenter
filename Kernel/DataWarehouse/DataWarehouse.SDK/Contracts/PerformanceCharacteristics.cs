namespace DataWarehouse.SDK.Contracts
{
    /// <summary>
    /// Performance characteristics of a plugin or capability.
    /// Used by AI for optimization decisions, cost estimation, and execution planning.
    ///
    /// Plugins should provide accurate metrics based on:
    /// - Benchmarking results
    /// - Historical execution data
    /// - Theoretical performance analysis
    /// </summary>
    public class PerformanceCharacteristics
    {
        /// <summary>
        /// Average latency in milliseconds for a typical operation.
        /// Used by AI to estimate execution time.
        ///
        /// Examples:
        /// - GZip compression: 50ms
        /// - AES encryption: 10ms
        /// - S3 upload: 500ms
        /// - Database query: 20ms
        /// </summary>
        public double AverageLatencyMs { get; init; }

        /// <summary>
        /// Throughput in bytes per second.
        /// Used by AI to estimate processing time for large data.
        ///
        /// Examples:
        /// - GZip compression: 100 MB/s
        /// - AES encryption: 500 MB/s
        /// - Network transfer: 10 MB/s
        /// - Disk I/O: 200 MB/s
        /// </summary>
        public long ThroughputBytesPerSecond { get; init; }

        /// <summary>
        /// Memory usage in bytes during typical operation.
        /// Used by AI to avoid out-of-memory scenarios.
        ///
        /// Examples:
        /// - Streaming compression: 64 KB
        /// - In-memory index: 100 MB
        /// - ML model: 2 GB
        /// </summary>
        public long MemoryUsageBytes { get; init; }

        /// <summary>
        /// CPU usage as percentage (0-100) during operation.
        /// Used by AI to balance load across capabilities.
        ///
        /// Examples:
        /// - I/O-bound operation: 10%
        /// - Compression: 80%
        /// - Encryption: 60%
        /// </summary>
        public double CpuUsagePercent { get; init; }

        /// <summary>
        /// Monetary cost per operation in USD.
        /// Used by AI for cost optimization.
        ///
        /// Examples:
        /// - Local operation: $0
        /// - S3 API call: $0.0004
        /// - LLM call: $0.01
        /// - GPU compute: $0.10
        /// </summary>
        public decimal CostPerOperationUsd { get; init; }

        /// <summary>
        /// Whether performance scales linearly with data size.
        /// Used by AI to predict performance for different data sizes.
        /// </summary>
        public bool LinearScaling { get; init; } = true;

        /// <summary>
        /// Minimum data size for efficient operation (bytes).
        /// Below this size, overhead dominates performance.
        /// Used by AI to choose appropriate capability for data size.
        /// </summary>
        public long MinimumEfficientSizeBytes { get; init; } = 0;

        /// <summary>
        /// Maximum recommended data size (bytes).
        /// Above this size, alternative approaches may be better.
        /// Used by AI to avoid performance cliffs.
        /// </summary>
        public long MaximumRecommendedSizeBytes { get; init; } = long.MaxValue;

        /// <summary>
        /// Reliability score (0.0 to 1.0).
        /// 1.0 = never fails, 0.5 = fails 50% of time.
        /// Used by AI for risk assessment.
        /// </summary>
        public double ReliabilityScore { get; init; } = 1.0;
    }
}
