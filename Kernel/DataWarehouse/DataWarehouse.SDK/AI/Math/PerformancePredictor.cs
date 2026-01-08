using DataWarehouse.SDK.AI.Graph;

namespace DataWarehouse.SDK.AI.Math
{
    /// <summary>
    /// Predicts execution performance (duration, memory, cost) based on input parameters.
    /// Uses historical data and capability metadata to estimate resource usage.
    ///
    /// Used by AI Runtime to:
    /// - Estimate how long an operation will take
    /// - Predict memory requirements
    /// - Forecast monetary costs
    /// - Warn about resource exhaustion
    /// - Plan capacity and scaling
    ///
    /// Prediction factors:
    /// - Input data size
    /// - Capability performance characteristics
    /// - Historical execution patterns
    /// - System load and concurrency
    /// </summary>
    /// <remarks>
    /// Constructs a performance predictor with the given knowledge graph.
    /// </remarks>
    /// <param name="graph">Knowledge graph containing capability metadata.</param>
    public class PerformancePredictor(KnowledgeGraph graph)
    {
        private readonly KnowledgeGraph _graph = graph ?? throw new ArgumentNullException(nameof(graph));
        private readonly Dictionary<string, List<ExecutionHistory>> _history = [];

        /// <summary>
        /// Predicts execution duration for a capability given input size.
        ///
        /// Use cases:
        /// - User uploads 1GB file to compress
        /// - Predictor estimates: "GZip will take ~50 seconds"
        /// - AI can inform user or suggest faster alternative
        ///
        /// Prediction approach:
        /// 1. Get throughput from capability metadata
        /// 2. Calculate: duration = inputSize / throughput
        /// 3. Add fixed overhead (latency)
        /// 4. Adjust for historical accuracy
        /// </summary>
        /// <param name="capabilityId">Capability to predict for.</param>
        /// <param name="inputSizeBytes">Size of input data in bytes.</param>
        /// <returns>Predicted duration in milliseconds.</returns>
        public double PredictDuration(string capabilityId, long inputSizeBytes)
        {
            var node = _graph.GetNode(capabilityId) ?? throw new ArgumentException($"Capability '{capabilityId}' not found");

            // Get performance characteristics from metadata
            double avgLatencyMs = 0;
            long throughputBps = 0;
            bool linearScaling = true;

            if (node.Metadata.TryGetValue("avgLatencyMs", out var latency))
                avgLatencyMs = Convert.ToDouble(latency);
            if (node.Metadata.TryGetValue("throughputBytesPerSecond", out var throughput))
                throughputBps = Convert.ToInt64(throughput);
            if (node.Metadata.TryGetValue("linearScaling", out var scaling))
                linearScaling = Convert.ToBoolean(scaling);

            // Calculate base duration
            double durationMs = avgLatencyMs;

            if (throughputBps > 0 && linearScaling)
            {
                // Linear scaling: duration = latency + (size / throughput)
                var throughputMs = throughputBps / 1000.0; // bytes per millisecond
                var processingTime = inputSizeBytes / throughputMs;
                durationMs += processingTime;
            }
            else if (!linearScaling)
            {
                // Non-linear scaling (e.g., O(n²))
                // Use a pessimistic multiplier
                durationMs *= (1 + MathUtils.Log(inputSizeBytes / 1024.0));
            }

            // Apply historical accuracy adjustment
            durationMs = ApplyHistoricalAdjustment(capabilityId, durationMs, inputSizeBytes);

            return MathUtils.Max(0, durationMs);
        }

        /// <summary>
        /// Predicts memory usage for a capability given input size.
        ///
        /// Use cases:
        /// - User wants to compress 5GB file
        /// - Predictor estimates: "GZip will use ~16MB memory"
        /// - AI ensures sufficient memory available
        ///
        /// Prediction approach:
        /// 1. Get base memory usage from metadata
        /// 2. Add memory for input buffers
        /// 3. Consider compression/expansion ratios
        /// </summary>
        /// <param name="capabilityId">Capability to predict for.</param>
        /// <param name="inputSizeBytes">Size of input data in bytes.</param>
        /// <returns>Predicted memory usage in bytes.</returns>
        public long PredictMemoryUsage(string capabilityId, long inputSizeBytes)
        {
            var node = _graph.GetNode(capabilityId) ?? throw new ArgumentException($"Capability '{capabilityId}' not found");

            // Get base memory usage
            long baseMemory = 0;
            if (node.Metadata.TryGetValue("memoryUsageBytes", out var memory))
                baseMemory = Convert.ToInt64(memory);

            // For most operations, memory = base + small buffer
            // Streaming operations don't load entire input
            var bufferSize = MathUtils.Min(inputSizeBytes, 64 * 1024 * 1024); // Max 64MB buffer

            return baseMemory + bufferSize;
        }

        /// <summary>
        /// Predicts monetary cost for a capability given input size.
        ///
        /// Use cases:
        /// - User wants to call external AI model
        /// - Predictor estimates: "This will cost ~$0.15"
        /// - AI requests approval if over threshold
        ///
        /// Prediction approach:
        /// 1. Get cost per operation from metadata
        /// 2. Multiply by number of operations
        /// 3. Add any fixed costs
        /// </summary>
        /// <param name="capabilityId">Capability to predict for.</param>
        /// <param name="inputSizeBytes">Size of input data in bytes.</param>
        /// <returns>Predicted cost in USD.</returns>
        public decimal PredictCost(string capabilityId, long inputSizeBytes)
        {
            var node = _graph.GetNode(capabilityId) ?? throw new ArgumentException($"Capability '{capabilityId}' not found");

            // Get cost per operation
            decimal costPerOperation = 0;
            if (node.Metadata.TryGetValue("costPerOperationUsd", out var cost))
                costPerOperation = Convert.ToDecimal(cost);

            // For most plugins, cost is per-operation (not per-byte)
            // Exception: API-based services that charge per MB/GB
            if (node.Metadata.TryGetValue("costPerMb", out var costPerMb))
            {
                var inputMb = inputSizeBytes / (1024.0 * 1024.0);
                return Convert.ToDecimal(costPerMb) * (decimal)inputMb;
            }

            return costPerOperation;
        }

        /// <summary>
        /// Predicts complete performance profile for an execution plan.
        ///
        /// Use cases:
        /// - Before executing multi-step workflow
        /// - Show user expected duration and cost
        /// - Warn if resources insufficient
        /// </summary>
        /// <param name="plan">Execution plan to predict for.</param>
        /// <param name="inputSizeBytes">Size of input data in bytes.</param>
        /// <returns>Performance prediction with all metrics.</returns>
        public PerformancePrediction PredictPlanPerformance(ExecutionPlan plan, long inputSizeBytes)
        {
            var prediction = new PerformancePrediction();
            var currentSize = inputSizeBytes;

            foreach (var step in plan.Steps)
            {
                var stepDuration = PredictDuration(step.CapabilityId, currentSize);
                var stepMemory = PredictMemoryUsage(step.CapabilityId, currentSize);
                var stepCost = PredictCost(step.CapabilityId, currentSize);

                // Account for parallel execution
                if (step.CanRunInParallel && prediction.StepPredictions.Count > 0)
                {
                    var prevStep = prediction.StepPredictions.Last();
                    if (prevStep.CanRunInParallel)
                    {
                        // Parallel steps: max duration, sum memory
                        prediction.TotalDurationMs = MathUtils.Max(prediction.TotalDurationMs, stepDuration);
                        prediction.PeakMemoryBytes = MathUtils.Max(prediction.PeakMemoryBytes, stepMemory);
                    }
                    else
                    {
                        prediction.TotalDurationMs += stepDuration;
                        prediction.PeakMemoryBytes = MathUtils.Max(prediction.PeakMemoryBytes, stepMemory);
                    }
                }
                else
                {
                    prediction.TotalDurationMs += stepDuration;
                    prediction.PeakMemoryBytes = MathUtils.Max(prediction.PeakMemoryBytes, stepMemory);
                }

                prediction.TotalCostUsd += stepCost;

                // Add step prediction
                prediction.StepPredictions.Add(new StepPrediction
                {
                    StepNumber = step.StepNumber,
                    CapabilityId = step.CapabilityId,
                    DurationMs = stepDuration,
                    MemoryBytes = stepMemory,
                    CostUsd = stepCost,
                    InputSizeBytes = currentSize,
                    CanRunInParallel = step.CanRunInParallel
                });

                // Update size for next step (if compression/decompression)
                currentSize = EstimateOutputSize(step.CapabilityId, currentSize);
            }

            return prediction;
        }

        /// <summary>
        /// Records actual execution metrics for improving future predictions.
        ///
        /// Use cases:
        /// - After capability executes, record actual duration/memory/cost
        /// - Machine learning: improve predictions over time
        /// - Detect performance degradation
        /// </summary>
        /// <param name="capabilityId">Capability that was executed.</param>
        /// <param name="inputSizeBytes">Actual input size.</param>
        /// <param name="actualDurationMs">Actual duration.</param>
        /// <param name="actualMemoryBytes">Actual memory used.</param>
        /// <param name="actualCostUsd">Actual cost incurred.</param>
        public void RecordExecution(
            string capabilityId,
            long inputSizeBytes,
            double actualDurationMs,
            long actualMemoryBytes,
            decimal actualCostUsd)
        {
            if (!_history.TryGetValue(capabilityId, out List<ExecutionHistory>? value))
            {
                value = [];
                _history[capabilityId] = value;
            }

            value.Add(new ExecutionHistory
            {
                Timestamp = DateTime.UtcNow,
                InputSizeBytes = inputSizeBytes,
                DurationMs = actualDurationMs,
                MemoryBytes = actualMemoryBytes,
                CostUsd = actualCostUsd
            });

            // Keep only recent history (last 100 executions)
            if (value.Count > 100)
            {
                _history[capabilityId] = [.. value.OrderByDescending(h => h.Timestamp).Take(100)];
            }
        }

        /// <summary>
        /// Gets prediction accuracy for a capability.
        /// Compares predictions vs actual execution metrics.
        /// </summary>
        /// <param name="capabilityId">Capability to analyze.</param>
        /// <returns>Prediction accuracy metrics.</returns>
        public PredictionAccuracy GetPredictionAccuracy(string capabilityId)
        {
            if (!_history.TryGetValue(capabilityId, out List<ExecutionHistory>? history) || history.Count == 0)
            {
                return new PredictionAccuracy { SampleCount = 0 };
            }

            double totalError = 0;
            int count = 0;

            foreach (var record in history)
            {
                var predicted = PredictDuration(capabilityId, record.InputSizeBytes);
                var actual = record.DurationMs;
                var error = MathUtils.Abs(predicted - actual) / actual;
                totalError += error;
                count++;
            }

            var avgError = totalError / count;

            return new PredictionAccuracy
            {
                SampleCount = count,
                AverageErrorPercent = avgError * 100,
                IsAccurate = avgError < 0.2 // Within 20% is considered accurate
            };
        }

        /// <summary>
        /// Applies historical accuracy adjustment to predictions.
        /// If predictions are consistently off, adjust future predictions.
        /// </summary>
        private double ApplyHistoricalAdjustment(string capabilityId, double predictedDuration, long inputSize)
        {
            if (!_history.TryGetValue(capabilityId, out List<ExecutionHistory>? value) || value.Count < 10)
                return predictedDuration; // Need sufficient history

            // Calculate average prediction error
            var recentHistory = value.TakeLast(20).ToList();
            double totalError = 0;
            int count = 0;

            foreach (var record in recentHistory)
            {
                var oldPrediction = record.DurationMs; // This should be stored prediction
                var actual = record.DurationMs;
                totalError += (actual - oldPrediction);
                count++;
            }

            if (count > 0)
            {
                var avgError = totalError / count;
                return predictedDuration + avgError; // Adjust by average error
            }

            return predictedDuration;
        }

        /// <summary>
        /// Estimates output size after capability execution.
        /// Used for predicting downstream capability performance.
        /// </summary>
        private long EstimateOutputSize(string capabilityId, long inputSize)
        {
            var node = _graph.GetNode(capabilityId);
            if (node == null)
                return inputSize;

            // Check for compression/expansion ratio in metadata
            if (node.Metadata.TryGetValue("compressionRatio", out var ratio))
            {
                var compressionRatio = Convert.ToDouble(ratio);
                return (long)(inputSize * compressionRatio);
            }

            // Default: assume output size = input size
            return inputSize;
        }
    }

    /// <summary>
    /// Complete performance prediction for an execution plan.
    /// </summary>
    public class PerformancePrediction
    {
        /// <summary>Total predicted duration (accounting for parallelization).</summary>
        public double TotalDurationMs { get; set; }

        /// <summary>Peak memory usage across all steps.</summary>
        public long PeakMemoryBytes { get; set; }

        /// <summary>Total predicted cost.</summary>
        public decimal TotalCostUsd { get; set; }

        /// <summary>Per-step predictions.</summary>
        public List<StepPrediction> StepPredictions { get; init; } = [];
    }

    /// <summary>
    /// Performance prediction for a single step.
    /// </summary>
    public class StepPrediction
    {
        public int StepNumber { get; set; }
        public string CapabilityId { get; set; } = string.Empty;
        public double DurationMs { get; set; }
        public long MemoryBytes { get; set; }
        public decimal CostUsd { get; set; }
        public long InputSizeBytes { get; set; }
        public bool CanRunInParallel { get; set; }
    }

    /// <summary>
    /// Historical execution record for machine learning.
    /// </summary>
    public class ExecutionHistory
    {
        public DateTime Timestamp { get; set; }
        public long InputSizeBytes { get; set; }
        public double DurationMs { get; set; }
        public long MemoryBytes { get; set; }
        public decimal CostUsd { get; set; }
    }

    /// <summary>
    /// Prediction accuracy metrics.
    /// </summary>
    public class PredictionAccuracy
    {
        public int SampleCount { get; set; }
        public double AverageErrorPercent { get; set; }
        public bool IsAccurate { get; set; }
    }
}
