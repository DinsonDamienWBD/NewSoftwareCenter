namespace SoftwareCenter.Core.Routing
{
    /// <summary>
    /// Defines a contract for an API endpoint that can be discovered at runtime.
    /// </summary>
    public interface IApiEndpoint
    {
        /// <summary>
        /// Gets the unique identifier for this API endpoint.
        /// </summary>
        string Id { get; }

        /// <summary>
        /// Gets the HTTP method for this endpoint (e.g., "GET", "POST", "PUT", "DELETE").
        /// </summary>
        string HttpMethod { get; }

        /// <summary>
        /// Gets the relative path for this API endpoint (e.g., "/api/v1/users").
        /// </summary>
        string Path { get; }

        /// <summary>
        /// Gets a human-readable description of what this API endpoint does.
        /// </summary>
        string Description { get; }

        /// <summary>
        /// Gets the ID of the module that owns and implements this API endpoint.
        /// </summary>
        string OwningModuleId { get; }
    }
}
