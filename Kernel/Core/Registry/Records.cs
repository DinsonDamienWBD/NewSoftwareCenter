using Core.Primitives;

namespace Core.Registry
{
    /// <summary>
    /// Base record for any capability registered by a module.
    /// </summary>
    public abstract class RegistryRecord
    {
        /// <summary>
        /// Internal unique identifier for this record.
        /// </summary>
        public Guid Id { get; } = Guid.NewGuid();

        /// <summary>Module that owns this resource.</summary>
        public string? OwnerModuleId { get; set; }

        /// <summary>Default access level for this resource.</summary>
        public AccessLevel AccessLevel { get; set; } = AccessLevel.Public;
    }

    /// <summary>
    /// Registration record for a Backend Capability (Command/Query Handler).
    /// </summary>
    public class ServiceRecord : RegistryRecord
    {
        /// <summary>
        /// Type of Message (Command or Query) that this handler processes.
        /// </summary>
        public Type? MessageType { get; set; }

        /// <summary>
        /// Type of the Handler that processes the Message.
        /// </summary>
        public Type? HandlerType { get; set; }

        /// <summary>
        /// Priority of this handler when multiple handlers exist for the same Message type.
        /// </summary>
        public Priority Priority { get; set; }

        /// <summary>
        /// Fallback Handler type if the primary handler fails.
        /// </summary>
        public Type? FallbackHandler { get; set; }
    }

    /// <summary>
    /// Registration record for a Frontend Fragment (UI Mosaic).
    /// </summary>
    public class UiRecord : RegistryRecord
    {
        /// <summary>
        /// Zone in the UI where this fragment should be rendered.
        /// </summary>
        public string? ZoneId { get; set; }

        /// <summary>
        /// Template identifier for rendering this fragment.
        /// </summary>
        public string? TemplateId { get; set; }

        /// <summary>
        /// Order of this fragment within its zone.
        /// </summary>
        public int Order { get; set; }

        // Rich Metadata

        /// <summary>
        /// Icon representing this UI fragment.
        /// </summary>
        public string? Icon { get; set; }

        /// <summary>
        /// Label for this UI fragment.
        /// </summary>
        public string? Label { get; set; }

        /// <summary>
        /// Required permission to access this UI fragment.
        /// </summary>
        public string? RequiredPermission { get; set; }

        // Hydration data for the template

        /// <summary>
        /// ViewModel data to be used when rendering the template.
        /// </summary>
        public object? ViewModel { get; set; }
    }
}