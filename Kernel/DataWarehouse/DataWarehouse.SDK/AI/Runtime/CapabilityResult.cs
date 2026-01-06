// In SDK/AI/Runtime/CapabilityResult.cs

using DataWarehouse.SDK.Primitives;

namespace DataWarehouse.SDK.AI.Runtime
{
    /// <summary>
    /// Represents the result of a capability execution with rich metadata
    /// for AI interpretation and user feedback.
    /// </summary>
    public class CapabilityResult
    {
        /// <summary>
        /// Whether the capability executed successfully.
        /// </summary>
        public bool IsSuccessful { get; init; }

        /// <summary>
        /// Human-readable message describing the result.
        /// </summary>
        public string? Message { get; init; }

        /// <summary>
        /// Structured return value (can be primitive, object, or collection).
        /// </summary>
        public object? Data { get; init; }

        /// <summary>
        /// Error details if execution failed.
        /// </summary>
        public CapabilityError? Error { get; init; }

        /// <summary>
        /// Execution metadata for AI analysis and optimization.
        /// </summary>
        public ExecutionMetadata Metadata { get; init; } = new();

        /// <summary>
        /// Whether this result requires user approval before proceeding.
        /// Used for multi-step plans where AI discovers additional actions needed.
        /// </summary>
        public bool RequiresFollowUpApproval { get; init; }

        /// <summary>
        /// Suggested follow-up actions based on execution result.
        /// AI can use these to build multi-step plans.
        /// </summary>
        public List<SuggestedAction> SuggestedActions { get; init; } = [];

        // Factory methods for common scenarios

        public static CapabilityResult Success(
            string? message = null,
            object? data = null)
        {
            return new CapabilityResult
            {
                IsSuccessful = true,
                Message = message ?? "Operation completed successfully",
                Data = data
            };
        }

        public static CapabilityResult Failure(
            string errorMessage,
            string? errorCode = null,
            Exception? exception = null,
            bool isRetryable = false)
        {
            return new CapabilityResult
            {
                IsSuccessful = false,
                Message = errorMessage,
                Error = new CapabilityError
                {
                    ErrorCode = errorCode ?? "CAPABILITY_ERROR",
                    ErrorMessage = errorMessage,
                    Exception = exception,
                    IsRetryable = isRetryable,
                    Timestamp = DateTime.UtcNow
                }
            };
        }

        public static CapabilityResult PartialSuccess(
            string message,
            object? data = null,
            List<CapabilityError>? errors = null)
        {
            return new CapabilityResult
            {
                IsSuccessful = true, // Still considered success
                Message = message,
                Data = data,
                Metadata = new ExecutionMetadata
                {
                    PartialFailures = errors ?? []
                }
            };
        }

        public static CapabilityResult RequiresApproval(
            string message,
            ProposedAction proposedAction)
        {
            return new CapabilityResult
            {
                IsSuccessful = false, // Not executed yet
                Message = message,
                RequiresFollowUpApproval = true,
                SuggestedActions =
                [
                    new SuggestedAction
                    {
                        Action = proposedAction,
                        Confidence = 1.0,
                        Reason = message
                    }
                ]
            };
        }

        public static CapabilityResult Cancelled(string reason)
        {
            return new CapabilityResult
            {
                IsSuccessful = false,
                Message = $"Operation cancelled: {reason}",
                Error = new CapabilityError
                {
                    ErrorCode = "CANCELLED",
                    ErrorMessage = reason,
                    IsRetryable = false
                }
            };
        }

        // Typed result wrapper
        public static CapabilityResult<T> Success<T>(T data, string? message = null)
        {
            return new CapabilityResult<T>
            {
                IsSuccessful = true,
                Message = message ?? "Operation completed successfully",
                Data = data,
                TypedData = data
            };
        }

        public static CapabilityResult<T> Failure<T>(
            string errorMessage,
            string? errorCode = null,
            Exception? exception = null)
        {
            return new CapabilityResult<T>
            {
                IsSuccessful = false,
                Message = errorMessage,
                Error = new CapabilityError
                {
                    ErrorCode = errorCode ?? "CAPABILITY_ERROR",
                    ErrorMessage = errorMessage,
                    Exception = exception
                }
            };
        }
    }

    /// <summary>
    /// Strongly-typed variant of CapabilityResult for capabilities
    /// with known return types.
    /// </summary>
    public class CapabilityResult<T> : CapabilityResult
    {
        /// <summary>
        /// Strongly-typed result data.
        /// </summary>
        public T? TypedData { get; init; }

        /// <summary>
        /// Safely gets the typed data or throws if failed.
        /// </summary>
        public T GetDataOrThrow()
        {
            if (!IsSuccessful || TypedData == null)
            {
                throw new InvalidOperationException(
                    $"Cannot get data from failed result: {Error?.ErrorMessage ?? Message}");
            }
            return TypedData;
        }

        /// <summary>
        /// Gets the typed data or returns a default value.
        /// </summary>
        public T? GetDataOrDefault(T? defaultValue = default)
        {
            return IsSuccessful ? TypedData : defaultValue;
        }
    }

    /// <summary>
    /// Detailed error information for failed capability executions.
    /// </summary>
    public class CapabilityError
    {
        /// <summary>
        /// Machine-readable error code (e.g., "PERMISSION_DENIED", "NOT_FOUND").
        /// </summary>
        public string ErrorCode { get; init; } = string.Empty;

        /// <summary>
        /// Human-readable error message.
        /// </summary>
        public string ErrorMessage { get; init; } = string.Empty;

        /// <summary>
        /// Underlying exception if available.
        /// </summary>
        public Exception? Exception { get; init; }

        /// <summary>
        /// Whether the operation can be retried.
        /// </summary>
        public bool IsRetryable { get; init; }

        /// <summary>
        /// Suggested retry delay if retryable.
        /// </summary>
        public TimeSpan? RetryAfter { get; init; }

        /// <summary>
        /// When the error occurred.
        /// </summary>
        public DateTime Timestamp { get; init; }

        /// <summary>
        /// Additional context for debugging (parameter values, state, etc.).
        /// </summary>
        public Dictionary<string, object>? Context { get; init; }

        /// <summary>
        /// Stack trace or call chain for debugging.
        /// </summary>
        public string? StackTrace => Exception?.StackTrace;
    }

    /// <summary>
    /// Execution metadata for performance tracking, cost estimation,
    /// and AI learning.
    /// </summary>
    public class ExecutionMetadata
    {
        /// <summary>
        /// When execution started.
        /// </summary>
        public DateTime StartTime { get; init; } = DateTime.UtcNow;

        /// <summary>
        /// When execution completed.
        /// </summary>
        public DateTime EndTime { get; init; }

        /// <summary>
        /// Total execution duration.
        /// </summary>
        public TimeSpan Duration => EndTime - StartTime;

        /// <summary>
        /// Plugin that executed the capability.
        /// </summary>
        public string? PluginId { get; init; }

        /// <summary>
        /// Capability that was executed.
        /// </summary>
        public string? CapabilityId { get; init; }

        /// <summary>
        /// User or agent that initiated the execution.
        /// </summary>
        public string? Initiator { get; init; }

        /// <summary>
        /// Estimated or actual cost of execution (in USD).
        /// Useful for AI cost optimization.
        /// </summary>
        public decimal? CostEstimate { get; init; }

        /// <summary>
        /// Resources consumed (bytes read/written, API calls, etc.).
        /// </summary>
        public ResourceUsage Resources { get; init; } = new();

        /// <summary>
        /// Performance metrics for AI optimization.
        /// </summary>
        public PerformanceMetrics Performance { get; init; } = new();

        /// <summary>
        /// Partial failures in batch operations.
        /// </summary>
        public List<CapabilityError> PartialFailures { get; init; } = [];

        /// <summary>
        /// Whether this execution was cached.
        /// </summary>
        public bool WasCached { get; init; }

        /// <summary>
        /// Whether user approval was required and granted.
        /// </summary>
        public bool WasApproved { get; init; }

        /// <summary>
        /// Custom metadata from plugins.
        /// </summary>
        public Dictionary<string, object> CustomMetadata { get; init; } = [];
    }

    /// <summary>
    /// Resource usage tracking for cost optimization and quotas.
    /// </summary>
    public class ResourceUsage
    {
        public long BytesRead { get; init; }
        public long BytesWritten { get; init; }
        public int ApiCallsExecuted { get; init; }
        public int DatabaseQueriesExecuted { get; init; }
        public long MemoryUsedBytes { get; init; }
        public long NetworkBytesTransferred { get; init; }
        public int CpuMilliseconds { get; init; }

        public decimal TotalCostUsd { get; init; }
    }

    /// <summary>
    /// Performance metrics for AI-driven optimization.
    /// </summary>
    public class PerformanceMetrics
    {
        /// <summary>
        /// Time spent waiting for I/O.
        /// </summary>
        public TimeSpan IoWaitTime { get; init; }

        /// <summary>
        /// Time spent in CPU computation.
        /// </summary>
        public TimeSpan CpuTime { get; init; }

        /// <summary>
        /// Time spent waiting for network.
        /// </summary>
        public TimeSpan NetworkTime { get; init; }

        /// <summary>
        /// Number of cache hits (if applicable).
        /// </summary>
        public int CacheHits { get; init; }

        /// <summary>
        /// Number of cache misses (if applicable).
        /// </summary>
        public int CacheMisses { get; init; }

        /// <summary>
        /// Cache hit ratio (0.0 to 1.0).
        /// </summary>
        public double CacheHitRatio =>
            CacheHits + CacheMisses > 0
                ? (double)CacheHits / (CacheHits + CacheMisses)
                : 0;

        /// <summary>
        /// Throughput in operations per second.
        /// </summary>
        public double ThroughputOpsPerSecond { get; init; }

        /// <summary>
        /// Latency percentiles for statistical analysis.
        /// </summary>
        public LatencyPercentiles? Latency { get; init; }
    }

    /// <summary>
    /// Latency distribution for performance analysis.
    /// </summary>
    public class LatencyPercentiles
    {
        public TimeSpan P50 { get; init; }  // Median
        public TimeSpan P90 { get; init; }
        public TimeSpan P95 { get; init; }
        public TimeSpan P99 { get; init; }
        public TimeSpan Max { get; init; }
    }

    /// <summary>
    /// AI-suggested follow-up action based on execution result.
    /// </summary>
    public class SuggestedAction
    {
        /// <summary>
        /// The proposed action to take.
        /// </summary>
        public ProposedAction Action { get; init; } = new();

        /// <summary>
        /// AI's confidence in this suggestion (0.0 to 1.0).
        /// </summary>
        public double Confidence { get; init; }

        /// <summary>
        /// Why the AI is suggesting this action.
        /// </summary>
        public string Reason { get; init; } = string.Empty;

        /// <summary>
        /// Expected benefit (cost savings, performance improvement, etc.).
        /// </summary>
        public string? ExpectedBenefit { get; init; }

        /// <summary>
        /// Priority/urgency of this action.
        /// </summary>
        public ActionPriority Priority { get; init; } = ActionPriority.Normal;
    }

    /// <summary>
    /// A proposed action that AI wants to execute (pending approval).
    /// </summary>
    public class ProposedAction
    {
        public string ActionId { get; init; } = Guid.NewGuid().ToString();
        public string Title { get; init; } = string.Empty;
        public string Description { get; init; } = string.Empty;
        public string CapabilityId { get; init; } = string.Empty;
        public Dictionary<string, object> Parameters { get; init; } = [];
        public bool RequiresApproval { get; init; } = true;
        public ActionSeverity Severity { get; init; } = ActionSeverity.Normal;
        public string? EstimatedCostSavings { get; init; }
        public string? EstimatedPerformanceImprovement { get; init; }
        public TimeSpan? EstimatedDuration { get; init; }
        public List<string> AffectedResources { get; init; } = [];
        public DateTime ProposedAt { get; init; } = DateTime.UtcNow;
        public DateTime? ExpiresAt { get; init; }
    }
}
