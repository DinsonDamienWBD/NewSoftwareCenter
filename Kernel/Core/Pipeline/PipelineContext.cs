using Core.Messages;
using System.Security.Claims;

namespace Core.Pipeline
{
    /// <summary>
    /// The "Context" that travels through the middleware onion.
    /// Carries the Message, the Response, and the Request Environment.
    /// </summary>
    /// <remarks>
    /// Constructor.
    /// </remarks>
    /// <param name="message"></param>
    /// <param name="scope"></param>
    /// <param name="ct"></param>
    public class PipelineContext(IMessage message, IServiceProvider scope, CancellationToken ct)
    {
        /// <summary>
        /// Message being processed.
        /// </summary>
        public IMessage? Message { get; } = message;

        /// <summary>
        /// Response being built.
        /// </summary>
        public IMessage? Response { get; set; }

        // Environment

        /// <summary>
        /// User associated with the request.
        /// </summary>
        public ClaimsPrincipal? User { get; set; }

        /// <summary>
        /// Multi-Tenancy: Tenant Id associated with the request.
        /// </summary>
        public string? TenantId { get; set; }

        /// <summary>
        /// Token to monitor for cancellation requests.
        /// </summary>
        public CancellationToken? CancellationToken { get; } = ct;

        /// <summary>
        /// Service Provider Scope for Dependency Injection.
        /// </summary>
        public IServiceProvider? Scope { get; } = scope;

        // Transient Items (Scope of Request Only) for Middleware communication

        /// <summary>
        /// Items dictionary for sharing data between middleware components.
        /// </summary>
        public IDictionary<string, object>? Items { get; } = new Dictionary<string, object>();

        /// <summary>
        /// Context Propagation (OpenTelemetry / External Services)
        /// </summary>
        public IDictionary<string, string> Baggage { get; } = new Dictionary<string, string>();
    }
}