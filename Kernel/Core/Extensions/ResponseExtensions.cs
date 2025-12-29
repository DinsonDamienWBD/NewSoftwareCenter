using Core.Messages;
using Core.Primitives;

namespace Core.Extensions
{
    /// <summary>
    /// Extension methods for the Response class.
    /// </summary>
    public static class ResponseExtensions
    {
        /// <summary>
        /// Conditionally executes one of the provided functions based on whether the response indicates success or failure.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <typeparam name="TResult"></typeparam>
        /// <param name="response"></param>
        /// <param name="onSuccess"></param>
        /// <param name="onFailure"></param>
        /// <returns></returns>
        public static TResult Match<T, TResult>(
            this Response<T> response,
            Func<T, TResult> onSuccess,
            Func<FailureResponse, TResult> onFailure)
        {
            if (response.IsSuccess) return onSuccess(response.Data!);

            // Reconstruct FailureResponse from base properties
            var errorDetails = response as FailureResponse ?? new FailureResponse(
                response.ErrorMessage ?? "Unknown Error",
                response.ErrorCode ?? CoreConstants.ErrorCodes.GeneralError,
                response.Category,
                response.Status,
                ParseGuid(response.CorrelationId)
            );
            return onFailure(errorDetails);
        }

        /// <summary>
        /// Functional mapping
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <typeparam name="TNew"></typeparam>
        /// <param name="response"></param>
        /// <param name="mapper"></param>
        /// <returns></returns>
        public static Response<TNew> Map<T, TNew>(this Response<T> response, Func<T, TNew> mapper)
        {
            var requestId = ParseGuid(response.CorrelationId);

            if (response.IsSuccess)
            {
                try
                {
                    // Success Path
                    return new Response<TNew>(mapper(response.Data!), requestId);
                }
                catch (Exception ex)
                {
                    // Mapper threw exception -> Convert to System Failure
                    return Response<TNew>.CreateFailure(
                        ex.Message,
                        CoreConstants.ErrorCodes.Exception,
                        FailureCategory.System,
                        500,
                        requestId
                    );
                }
            }

            // Preserve original error details in new generic type
            // create the correct generic type
            return Response<TNew>.CreateFailure(
                response.ErrorMessage ?? "Operation Failed",
                response.ErrorCode ?? CoreConstants.ErrorCodes.GeneralError,
                response.Category,
                response.Status,
                requestId
            );
        }

        // Helper to fix CS8604 (Safe Parsing)
        private static Guid ParseGuid(string? input)
        {
            if (string.IsNullOrEmpty(input) || !Guid.TryParse(input, out var result))
            {
                return Guid.Empty;
            }
            return result;
        }
    }
}