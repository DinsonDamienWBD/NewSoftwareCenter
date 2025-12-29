using Core.Primitives;

namespace Core.Messages
{
    /// <summary>
    /// Standard envelope for operation results.
    /// </summary>
    public class Response<T> : MessageBase, IDisposable
    {
        /// <summary>
        /// Data returned from the operation.
        /// </summary>
        public T? Data { get; }

        /// <summary>
        /// Status of the operation.
        /// </summary>
        public bool IsSuccess { get; }

        /// <summary>
        /// Error message if the operation failed.
        /// </summary>
        public string? ErrorMessage { get; }

        /// <summary>
        /// Aggregate errors
        /// </summary>
        public List<string> AdditionalErrors { get; } = [];

        /// <summary>
        /// Payload of the response.
        /// </summary>
        public override object? Payload => Data;

        // RFC 7807 Fields

        /// <summary>
        /// URI reference
        /// </summary>
        public string? Type { get; }

        /// <summary>
        /// Title
        /// </summary>
        public string? Title { get; }

        /// <summary>
        /// HTTP Status equivalent
        /// </summary>
        public int Status { get; }

        /// <summary>
        /// URI reference
        /// </summary>
        public string? Instance { get; }

        /// <summary>Machine-readable error code (e.g., "AUTH_001").</summary>
        public string? ErrorCode { get; }

        /// <summary>
        /// Category of failure
        /// </summary>
        public FailureCategory Category { get; }

        /// <summary>
        /// Creates a successful response.
        /// </summary>
        /// <param name="data"></param>
        /// <param name="requestId"></param>
        public Response(T? data, Guid requestId)
        {
            Data = data;
            IsSuccess = true;
            CorrelationId = requestId.ToString();
            AddTrace("System.Core", "Response", "Success");
        }

        /// <summary>
        /// Protected Failure Constructor (Now accepts rich metadata)
        /// </summary>
        /// <param name="error"></param>
        /// <param name="errorCode"></param>
        /// <param name="category"></param>
        /// <param name="status"></param>
        /// <param name="requestId"></param>
        protected Response(
            string error,
            string errorCode,
            FailureCategory category,
            int status,
            Guid requestId)
        {
            IsSuccess = false;
            ErrorMessage = error;
            ErrorCode = errorCode;
            Category = category;
            Status = status;
            Title = category.ToString();
            Type = $"urn:error:{errorCode}";
            Instance = $"urn:request:{requestId}";
            CorrelationId = requestId.ToString();
            AddTrace(CoreConstants.TraceSource, "Response", $"Failed: {error}");
        }

        /// <summary>
        /// Convenience internal constructor for mapping
        /// </summary>
        /// <param name="error"></param>
        /// <param name="errorCode"></param>
        /// <param name="category"></param>
        /// <param name="status"></param>
        /// <param name="requestId"></param>
        /// <returns></returns>
        internal static Response<T> CreateFailure(
            string error,
            string errorCode,
            FailureCategory category,
            int status,
            Guid requestId)
        {
            return new Response<T>(error, errorCode, category, status, requestId);
        }

        /// <summary>
        /// Safe disposal
        /// </summary>
        public void Dispose()
        {
            if (Data is IDisposable d)
            {
                d.Dispose();
            }
            GC.SuppressFinalize(this);
        }
    }

    /// <summary>
    /// Batch Response with Partial Failures
    /// </summary>
    /// <typeparam name="T"></typeparam>
    public class BatchResponseItem<T>
    {
        /// <summary>
        /// Item
        /// </summary>
        public T? Item { get; set; }

        /// <summary>
        /// Is it a success or not
        /// </summary>
        public bool IsSuccess { get; set; }

        /// <summary>
        /// Error message
        /// </summary>
        public string Error { get; set; } = string.Empty;

        /// <summary>
        /// ID
        /// </summary>
        public string Id { get; set; } = string.Empty;
    }

    /// <summary>
    /// Batch response
    /// </summary>
    /// <typeparam name="T"></typeparam>
    public class BatchResponse<T> : Response<List<BatchResponseItem<T>>>
    {
        /// <summary>
        /// Count of successes
        /// </summary>
        public int SuccessCount { get; }

        /// <summary>
        /// Count of failures
        /// </summary>
        public int FailureCount { get; }

        /// <summary>
        /// Constructor
        /// </summary>
        /// <param name="items"></param>
        /// <param name="requestId"></param>
        public BatchResponse(List<BatchResponseItem<T>> items, Guid requestId) : base(items, requestId)
        {
            SuccessCount = 0;
            FailureCount = 0;
            foreach (var item in items)
            {
                if (item.IsSuccess) SuccessCount++; else FailureCount++;
            }
        }
    }

    /// <summary>
    /// Helper for void commands (Success with no data).
    /// </summary>
    /// <remarks>
    /// Creates a successful response.
    /// </remarks>
    /// <param name="requestId"></param>
    public class SuccessResponse(Guid requestId) : Response<bool>(true, requestId)
    {
    }

    /// <summary>
    /// Standard Failure envelope with structured error codes.
    /// </summary>
    public class FailureResponse : Response<object?>
    {
        /// <summary>
        /// The error message
        /// </summary>
        public string? Detail => ErrorMessage;

        /// <summary>
        /// Creates a failed response.
        /// </summary>
        /// <param name="error"></param>
        /// <param name="errorCode"></param>
        /// <param name="category"></param>
        /// <param name="status"></param>
        /// <param name="requestId"></param>
        public FailureResponse(
            string error,
            string errorCode = CoreConstants.ErrorCodes.GeneralError,
            FailureCategory category = FailureCategory.Logical,
            int status = 400,
            Guid requestId = default)
            : base(error, errorCode, category, status, requestId)
        {
        }

        /// <summary>
        /// Creates a failed response from an exception.
        /// </summary>
        /// <param name="ex"></param>
        /// <param name="requestId"></param>
        public FailureResponse(Exception ex, Guid requestId)
            : base(ex.Message, CoreConstants.ErrorCodes.Exception, FailureCategory.System, 500, requestId)
        {
            AddTrace(CoreConstants.TraceSource, "Exception", ex.ToString());
        }
    }
}