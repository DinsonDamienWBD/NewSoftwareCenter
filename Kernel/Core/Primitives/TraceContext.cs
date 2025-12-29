namespace Core.Primitives
{
    /// <summary>
    /// W3C Trace Context compliant structure for distributed tracing.
    /// Replaces simple strings to ensure interoperability with OpenTelemetry.
    /// </summary>
    /// <remarks>
    /// Constructor
    /// </remarks>
    /// <param name="traceId"></param>
    /// <param name="spanId"></param>
    /// <param name="traceState"></param>
    /// <param name="isSampled"></param>
    public readonly struct TraceContext(string traceId, string spanId, string traceState = "", bool isSampled = true)
    {
        /// <summary>
        /// Trace ID
        /// </summary>
        public string TraceId { get; } = traceId;

        /// <summary>
        /// Span ID
        /// </summary>
        public string SpanId { get; } = spanId;

        /// <summary>
        /// State of the Trace
        /// </summary>
        public string TraceState { get; } = traceState;

        /// <summary>
        /// Checks if it is sampled or not
        /// </summary>
        public bool IsSampled { get; } = isSampled;

        /// <summary>
        /// Create a new trace context
        /// </summary>
        /// <returns></returns>
        public static TraceContext New()
            => new(Guid.NewGuid().ToString("N"), Guid.NewGuid().ToString("N")[..16]);

        /// <summary>
        /// Parses a W3C 'traceparent' header value.
        /// Format: 00-{traceId}-{spanId}-{flags}
        /// </summary>
        public static bool TryParse(string header, out TraceContext context)
        {
            context = default;
            if (string.IsNullOrWhiteSpace(header)) return false;

            var parts = header.Split('-');
            if (parts.Length < 4) return false;

            context = new TraceContext(parts[1], parts[2], "", parts[3] == "01");
            return true;
        }

        /// <summary>
        /// Cast trace context to string
        /// </summary>
        /// <returns></returns>
        public override string ToString() => $"00-{TraceId}-{SpanId}-{(IsSampled ? "01" : "00")}";
    }
}