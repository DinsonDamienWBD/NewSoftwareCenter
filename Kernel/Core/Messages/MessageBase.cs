using Core.Primitives;
using System.Collections.Concurrent;
using System.Globalization;

namespace Core.Messages
{
    /// <summary>
    /// The "Smart Packet" implementation.
    /// Includes the inescapable "Flight Recorder" audit trail and infrastructure plumbing.
    /// Modules MUST inherit from specialized versions of this (Command/Query/Event).
    /// </summary>
    public abstract class MessageBase : IMessage
    {
        /// <summary>
        /// Instance Unique Identifier
        /// </summary>
        public Guid Id { get; private set; } = Guid.NewGuid();

        /// <summary>
        /// Timestamp of Creation (UTC)
        /// </summary>
        public DateTimeOffset? Timestamp { get; private set; } = SystemTime.UtcNow;

        /// <summary>
        /// Time to live
        /// </summary>
        public DateTimeOffset? ExpiresAt { get; set; }

        /// <summary>
        /// Sender of the Message
        /// </summary>
        public string? Sender { get; set; } = "Anonymous";

        // Tracing & Graphing

        /// <summary>
        /// Correlation Identifier
        /// </summary>
        public string? CorrelationId { get; set; }

        /// <summary>
        /// Causation Identifier
        /// </summary>
        public string? CausationId { get; set; }

        /// <summary>
        /// W3C Standard for trace/audit
        /// </summary>
        public TraceContext TraceContext { get; set; } = TraceContext.New();

        /// <summary>
        /// Activity Identifier for Backwards compatibility/Interface implementation
        /// </summary>
        public string? ActivityId 
        {
            get => TraceContext.ToString();
            set { /* No-op or parse logic if needed */ }
        }

        // Reliability

        /// <summary>
        /// Retry Count for Processing Attempts
        /// </summary>
        public int RetryCount { get; set; } = 0;

        /// <summary>
        /// Override the priority
        /// </summary>
        public Priority? PriorityOverride { get; set; }

        /// <summary>
        /// Default to Sender, override for Tenant
        /// </summary>
        public virtual string? PartitionKey => Sender;

        // NEW: Scope & Safety
        
        /// <summary>
        /// Tenent scope
        /// </summary>
        public TenantScope Scope { get; set; } = TenantScope.TenantSpecific;

        /// <summary>
        /// Check if it is a dry run
        /// </summary>
        public bool IsDryRun { get; set; } = false;

        // Data Evolution

        /// <summary>
        /// Schema Version of the Message Payload
        /// </summary>
        public virtual int SchemaVersion => 1;

        /// <summary>
        /// Culture Information
        /// </summary>
        public CultureInfo Culture { get; set; } = CultureInfo.CurrentCulture;

        /// <summary>
        /// Metadata Key-Value Pairs
        /// </summary>
        public IDictionary<string, string> Metadata { get; private set; } = new Dictionary<string, string>();

        // Hybrid Payload Support

        /// <summary>
        /// Attachments (Large/Binary Data)
        /// </summary>
        public IDictionary<string, Stream> Attachments { get; private set; } = new Dictionary<string, Stream>();

        // Detailed Audit Diffs

        /// <summary>
        /// Detailed audit diffs
        /// </summary>
        public List<AuditEntry> AuditLog { get; private set; } = [];

        // Security Forensics

        /// <summary>
        /// Client IP Address
        /// </summary>
        public string ClientIp { get; set; } = string.Empty;

        /// <summary>
        /// User Agent String
        /// </summary>
        public string UserAgent { get; set; } = string.Empty;

        // Payload Management

        /// <summary>
        /// Payload of the Message
        /// </summary>
        public virtual object? Payload => this;

        private object? _swappedPayload;

        /// <summary>
        /// Set a Swapped Payload (for versioning, etc.)
        /// </summary>
        /// <param name="payload"></param>
        public void SetPayload(object? payload) => _swappedPayload = payload;

        object? IMessage.Payload => _swappedPayload ?? Payload;

        // Type Hints for Serializers

        /// <summary>
        /// Type Name of the Message
        /// </summary>
        public string? MessageTypeName => GetType().AssemblyQualifiedName;

        /// <summary>
        /// Type Name of the Payload
        /// </summary>
        public string? PayloadTypeName => Payload?.GetType().AssemblyQualifiedName;

        // Immutability Logic
        
        /// <summary>
        /// Check if message is read only
        /// </summary>
        public bool IsReadOnly { get; private set; }

        /// <summary>
        /// Lock the message to prevent further change
        /// </summary>
        public void Lock() => IsReadOnly = true;

        /// <summary>
        /// Throw exception if anyone tries to change a read-only message
        /// </summary>
        /// <exception cref="InvalidOperationException"></exception>
        protected void ThrowIfReadOnly()
        {
            if (IsReadOnly) throw new InvalidOperationException("Message is read-only.");
        }

        /// <summary>
        /// Cloning Implementation
        /// </summary>
        /// <returns></returns>
        public virtual IMessage Clone()
        {
            // Shallow Copy
            var clone = (MessageBase)this.MemberwiseClone();

            // Deep Copy Collections
            clone.Metadata = new Dictionary<string, string>(this.Metadata);
            clone.Attachments = new Dictionary<string, Stream>(this.Attachments);
            clone.AuditLog = [.. this.AuditLog];

            // Generate New Identity for the Clone (It's a new message instance)
            clone.Id = Guid.NewGuid();
            clone.Timestamp = SystemTime.UtcNow;

            // Unlock the clone so it can be modified (e.g. updating RetryCount)
            clone.IsReadOnly = false;

            return clone;
        }

        // ---------------------------------------------------------------------
        // FLIGHT RECORDER (In-Memory Audit)
        // ---------------------------------------------------------------------
        private readonly ConcurrentQueue<string> _traceLog = new();

        /// <summary>
        /// Constructor
        /// </summary>
        protected MessageBase()
        {
            AddTrace("System.Core", "Created", $"Type: {GetType().Name}");
        }


        /// <summary>
        /// Add a Trace Entry to the Flight Recorder.
        /// Redacts any occurrence of "Password" or "Secret" in the details.
        /// </summary>
        /// <param name="actor"></param>
        /// <param name="action"></param>
        /// <param name="details"></param>
        public void AddTrace(string actor, string action, string? details = null)
        {
            if (details != null && (details.Contains("Password", StringComparison.OrdinalIgnoreCase) || details.Contains("Secret", StringComparison.OrdinalIgnoreCase)))
            {
                details = "[REDACTED]";
            }
            var log = $"[{SystemTime.UtcNow:HH:mm:ss.fff}] [{actor}] {action}: {details ?? ""}";
            _traceLog.Enqueue(log);
        }

        /// <summary>
        /// Get the Trace Dump from the Flight Recorder
        /// </summary>
        /// <returns></returns>
        public IReadOnlyList<string> GetTraceDump() => [.. _traceLog];
    }
}