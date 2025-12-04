using SoftwareCenter.Core.Diagnostics;

namespace SoftwareCenter.Core.Commands
{
    /// <summary>
    /// The universal response envelope.
    /// Every command must return this, ensuring the app never crashes from unhandled exceptions.
    /// </summary>
    public interface IResult
    {
        /// <summary>
        /// True if the operation completed successfully.
        /// </summary>
        bool Success { get; }

        /// <summary>
        /// Human-readable feedback.
        /// If Success is false, this contains the error message.
        /// </summary>
        string Message { get; }

        /// <summary>
        /// The return payload. Can be null if the operation has no return value.
        /// The receiver is responsible for casting this to the expected type.
        /// </summary>
        object? Data { get; }

        /// <summary>
        /// TRACING
        /// </summary>
        Guid TraceId { get; }

        /// <summary>
        /// The Audit Trail.
        /// The Kernel's Proxy automatically adds to this list.
        /// Modules generally do not touch this.
        /// </summary>
        List<TraceHop> History { get; }
    }
}