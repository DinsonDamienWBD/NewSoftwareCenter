using System;

namespace DataWarehouse.SDK.Contracts
{
    /// <summary>
    /// Standard response format for all plugin message handling operations.
    /// Provides a consistent structure for success/error responses across all plugins.
    /// Used by the message-based communication system between Kernel and plugins.
    /// </summary>
    public class MessageResponse
    {
        /// <summary>
        /// Indicates whether the message was handled successfully.
        /// True = operation completed without errors.
        /// False = operation failed (see ErrorMessage for details).
        /// </summary>
        public bool Success { get; init; }

        /// <summary>
        /// The result data returned by the operation (if any).
        /// Can be null for operations that don't return data (e.g., delete).
        /// Type varies based on capability being invoked.
        /// </summary>
        public object? Data { get; init; }

        /// <summary>
        /// Human-readable error message if Success is false.
        /// Null or empty when Success is true.
        /// Should clearly describe what went wrong.
        /// </summary>
        public string? ErrorMessage { get; init; }

        /// <summary>
        /// When this response was created (UTC).
        /// Useful for debugging, logging, and performance tracking.
        /// </summary>
        public DateTime Timestamp { get; init; } = DateTime.UtcNow;

        /// <summary>
        /// Creates a successful response with optional data payload.
        /// </summary>
        /// <param name="data">Optional result data to include in the response.</param>
        /// <returns>A MessageResponse indicating success.</returns>
        public static MessageResponse SuccessResponse(object? data = null) =>
            new() { Success = true, Data = data };

        /// <summary>
        /// Creates a failed response with an error message.
        /// </summary>
        /// <param name="errorMessage">Description of what went wrong.</param>
        /// <returns>A MessageResponse indicating failure.</returns>
        public static MessageResponse ErrorResponse(string errorMessage) =>
            new() { Success = false, ErrorMessage = errorMessage };

        /// <summary>
        /// Creates a failed response from an exception.
        /// Automatically extracts exception type and message.
        /// </summary>
        /// <param name="ex">The exception that occurred.</param>
        /// <returns>A MessageResponse indicating failure with exception details.</returns>
        public static MessageResponse FromException(Exception ex) =>
            new() { Success = false, ErrorMessage = $"{ex.GetType().Name}: {ex.Message}" };
    }
}
