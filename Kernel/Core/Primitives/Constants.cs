namespace Core.Primitives
{
    /// <summary>
    /// Central repository for system-wide constant strings to avoid "magic strings".
    /// </summary>
    public static class CoreConstants
    {
        /// <summary>
        /// System user name
        /// </summary>
        public const string SystemUser = "System";

        /// <summary>
        /// Anonymous User name
        /// </summary>
        public const string AnonymousUser = "Anonymous";

        /// <summary>
        /// Trace source name
        /// </summary>
        public const string TraceSource = "SoftwareCenter.Core";

        /// <summary>
        /// Standard error codes
        /// </summary>
        public static class ErrorCodes
        {
            /// <summary>
            /// General error
            /// </summary>
            public const string GeneralError = "GENERAL_ERROR";

            /// <summary>
            /// Exception
            /// </summary>
            public const string Exception = "EXCEPTION";

            /// <summary>
            /// Validation failure
            /// </summary>
            public const string Validation = "VALIDATION_FAILED";

            /// <summary>
            /// Unauthorized access failure
            /// </summary>
            public const string Unauthorized = "UNAUTHORIZED";

            /// <summary>
            /// Item Not found failure
            /// </summary>
            public const string NotFound = "NOT_FOUND";

            /// <summary>
            /// Circuit open failure
            /// </summary>
            public const string CircuitOpen = "CIRCUIT_OPEN";
        }
    }

    /// <summary>
    /// Core content types
    /// </summary>
    public static class CoreContentTypes
    {
        /// <summary>
        /// JSON type
        /// </summary>
        public const string Json = "application/json";

        /// <summary>
        /// XML type
        /// </summary>
        public const string Xml = "application/xml";

        /// <summary>
        /// Plaintext type
        /// </summary>
        public const string Text = "text/plain";

        /// <summary>
        /// Binary type
        /// </summary>
        public const string Binary = "application/octet-stream";

        /// <summary>
        /// Compressed archive type
        /// </summary>
        public const string Zip = "application/zip";

        /// <summary>
        /// PNG type
        /// </summary>
        public const string Png = "image/png";

        /// <summary>
        /// JPG type
        /// </summary>
        public const string Jpeg = "image/jpeg";
    }

    /// <summary>
    /// Standardized Claim Types for Identity Management.
    /// </summary>
    public static class ClaimTypes
    {
        /// <summary>
        /// Tenant ID
        /// </summary>
        public const string TenantId = "sc_tenant_id";

        /// <summary>
        /// Identity of the user
        /// </summary>
        public const string UserId = "sc_user_id";

        /// <summary>
        /// Permission for the user
        /// </summary>
        public const string Permission = "sc_permission";

        /// <summary>
        /// Role of the user
        /// </summary>
        public const string Role = "sc_role";

        /// <summary>
        /// Full name of the user
        /// </summary>
        public const string FullName = "sc_fullname";
    }

    /// <summary>
    /// New constants for HTTP interoperability
    /// </summary>
    public static class CoreHeaderNames
    {
        /// <summary>
        /// Trace parent
        /// </summary>
        public const string TraceParent = "traceparent";

        /// <summary>
        /// trace state
        /// </summary>
        public const string TraceState = "tracestate";

        /// <summary>
        /// Correlation ID
        /// </summary>
        public const string CorrelationId = "x-correlation-id";

        /// <summary>
        /// Request ID
        /// </summary>
        public const string RequestId = "x-request-id";

        /// <summary>
        /// Tenant ID
        /// </summary>
        public const string TenantId = "x-tenant-id";

        /// <summary>
        /// User ID
        /// </summary>
        public const string UserId = "x-user-id";

        /// <summary>
        /// Authorization
        /// </summary>
        public const string Authorization = "Authorization";
    }

    /// <summary>
    /// Standard constants
    /// </summary>
    public static class MetaKeys
    {
        // Network

        /// <summary>
        /// Source IP
        /// </summary>
        public const string SourceIp = "net.source_ip";

        /// <summary>
        /// Origin Node
        /// </summary>
        public const string OriginNode = "net.origin_node";

        /// <summary>
        /// User Agent
        /// </summary>
        public const string UserAgent = "http.user_agent";

        // Identity

        /// <summary>
        /// User ID
        /// </summary>
        public const string UserId = "identity.user_id";

        /// <summary>
        /// Tenant ID
        /// </summary>
        public const string TenantId = "identity.tenant_id";

        /// <summary>
        /// Roles
        /// </summary>
        public const string Roles = "identity.roles";

        // Context

        /// <summary>
        /// Correlation ID
        /// </summary>
        public const string CorrelationId = "ctx.correlation_id";

        /// <summary>
        /// Causation ID
        /// </summary>
        public const string CausationId = "ctx.causation_id";

        /// <summary>
        /// Trace Parent
        /// </summary>
        public const string TraceParent = "ctx.trace_parent";
    }
}