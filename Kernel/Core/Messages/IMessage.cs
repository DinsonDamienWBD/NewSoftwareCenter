using Core.Primitives;
using System.Globalization;

namespace Core.Messages
{
    /// <summary>
    /// The fundamental contract for all data packets in the system.
    /// </summary>
    public interface IMessage
    {
        // --- Identification ---

        /// <summary>Unique Identifier for this specific message instance.</summary>
        Guid Id { get; }

        /// <summary>UTC Timestamp of creation.</summary>
        DateTimeOffset? Timestamp { get; }

        /// <summary>
        /// Time to live
        /// </summary>
        DateTimeOffset? ExpiresAt { get; set; }

        /// <summary>Identity of the creator/sender.</summary>
        string? Sender { get; }

        // --- Tracing ---

        /// <summary>Links a Response to its Request.</summary>
        string? CorrelationId { get; }

        /// <summary>Links this message to the message that caused it (Directed Graph).</summary>
        string? CausationId { get; }

        /// <summary>W3C Trace Context Activity ID for distributed tracing.</summary>
        string? ActivityId { get; set; }

        /// <summary>
        /// W3C standard
        /// </summary>
        TraceContext TraceContext { get; set; }

        /// <summary>
        /// Tenant scope
        /// </summary>
        TenantScope Scope { get; set; }

        /// <summary>
        /// Check if it is a dry run
        /// </summary>
        bool IsDryRun { get; set; } // NEW

        // --- Reliability & Routing ---

        /// <summary>
        /// Retry attempt count for processing this message.
        /// </summary>
        int RetryCount { get; set; }

        /// <summary>
        /// For sharding
        /// </summary>
        string? PartitionKey { get; }

        // --- Metadata ---

        /// <summary>Schema version for data evolution handling.</summary>
        int SchemaVersion { get; }

        /// <summary>Assembly Qualified Name of the concrete message class (for deserialization).</summary>
        string? MessageTypeName { get; }

        /// <summary>Assembly Qualified Name of the payload object type.</summary>
        string? PayloadTypeName { get; }

        /// <summary>Localization context for this message.</summary>
        CultureInfo Culture { get; set; }

        /// <summary>Persistent key-value headers (Persisted to disk/queue).</summary>
        IDictionary<string, string> Metadata { get; }

        /// <summary>
        /// Detailed audit diffs
        /// </summary>
        List<AuditEntry> AuditLog { get; }

        // --- Payload ---

        /// <summary>The core data content.</summary>
        object? Payload { get; }

        /// <summary>Allows middleware to replace the payload (e.g. Encryption).</summary>
        void SetPayload(object? payload);

        // --- Immutability ---

        /// <summary>
        /// Check if messaage is read-only
        /// </summary>
        bool IsReadOnly { get; }

        /// <summary>
        /// Lock the message against further changes
        /// </summary>
        void Lock();

        /// <summary>
        /// Immutable Cloning
        /// </summary>
        /// <returns></returns>
        IMessage Clone();

        // --- Audit ---

        /// <summary>Adds an entry to the in-memory audit trail.</summary>
        void AddTrace(string actor, string action, string? details = null);

        /// <summary>Retrieves the full in-memory audit log.</summary>
        IReadOnlyList<string> GetTraceDump();
    }

    /// <summary>
    /// Helper interface for messages bound to a specific user
    /// </summary>
    public interface IUserBoundMessage
    {
        /// <summary>
        /// ID of a user
        /// </summary>
        string? UserId { get; }
    }
}