namespace SoftwareCenter.Kernel.Data
{
    public enum RouteStatus
    {
        Active,
        Deprecated,
        Obsolete,
        Experimental
    }

    /// <summary>
    /// Defines the capabilities and status of a registered command.
    /// Returns a self-contained description for the Discovery API.
    /// </summary>
    public class RouteMetadata
    {
        /// <summary>
        /// The unique command key (e.g., "User.Save").
        /// Essential for the UI/AI to know what to invoke.
        /// </summary>
        public string CommandId { get; set; } = string.Empty;

        /// <summary>
        /// Human-readable explanation.
        /// </summary>
        public string Description { get; set; } = string.Empty;

        /// <summary>
        /// The version of the implementation (e.g., "1.0.0").
        /// </summary>
        public string Version { get; set; } = "1.0.0";

        /// <summary>
        /// The lifecycle status.
        /// </summary>
        public RouteStatus Status { get; set; } = RouteStatus.Active;

        /// <summary>
        /// Advisory message (e.g., "Use 'User.SaveV2' instead.").
        /// </summary>
        public string? DeprecationMessage { get; set; }

        /// <summary>
        /// The ID of the module providing this implementation.
        /// (Renamed from OwnerModuleId to match your Unregister logic).
        /// </summary>
        public string SourceModule { get; set; } = string.Empty;

        /// <summary>
        /// The priority of this specific handler implementation.
        /// Higher numbers are invoked first.
        /// </summary>
        public int Priority { get; set; }

        /// <summary>
        /// True if this handler is the current highest-priority active handler for the command.
        /// </summary>
        public bool IsActiveSelection { get; set; }
    }
}