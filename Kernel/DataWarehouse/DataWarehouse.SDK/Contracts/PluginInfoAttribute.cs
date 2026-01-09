namespace DataWarehouse.SDK.Contracts
{
    /// <summary>
    /// Declarative metadata attribute for plugins.
    /// Provides compile-time documentation and enables plugin discovery.
    /// </summary>
    /// <remarks>
    /// This attribute is optional but highly recommended. It provides:
    /// - IDE IntelliSense documentation
    /// - Plugin discovery through reflection
    /// - Compile-time validation of metadata
    /// - Automatic documentation generation
    /// </remarks>
    /// <example>
    /// <code>
    /// [PluginInfo(
    ///     name: "Local Storage Provider",
    ///     description: "Provides persistent local file system storage",
    ///     author: "DataWarehouse Team",
    ///     version: "1.0.0",
    ///     category: PluginCategory.Storage
    /// )]
    /// public class LocalStoragePlugin : PluginBase, IStorageProvider
    /// {
    ///     // Implementation
    /// }
    /// </code>
    /// </example>
    [AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = false)]
    public class PluginInfoAttribute : Attribute
    {
        /// <summary>
        /// Human-readable name of the plugin.
        /// </summary>
        public string Name { get; }

        /// <summary>
        /// Description of what the plugin does.
        /// </summary>
        public string Description { get; }

        /// <summary>
        /// Author or organization that created the plugin.
        /// </summary>
        public string Author { get; }

        /// <summary>
        /// Semantic version string (e.g., "1.0.0", "2.1.0-beta").
        /// </summary>
        public string Version { get; }

        /// <summary>
        /// Primary category this plugin belongs to.
        /// </summary>
        public PluginCategory Category { get; }

        /// <summary>
        /// URL to the plugin's homepage or documentation.
        /// </summary>
        public string? Url { get; set; }

        /// <summary>
        /// License identifier (e.g., "MIT", "Apache-2.0", "GPL-3.0").
        /// </summary>
        public string? License { get; set; }

        /// <summary>
        /// Comma-separated tags for search and categorization.
        /// </summary>
        public string? Tags { get; set; }

        /// <summary>
        /// Indicates whether this plugin is experimental/beta.
        /// </summary>
        public bool IsExperimental { get; set; }

        /// <summary>
        /// Minimum DataWarehouse version required (e.g., "1.0.0").
        /// </summary>
        public string? MinimumKernelVersion { get; set; }

        /// <summary>
        /// Initializes a new instance of the PluginInfo attribute.
        /// </summary>
        /// <param name="name">Human-readable plugin name</param>
        /// <param name="description">What the plugin does</param>
        /// <param name="author">Author or organization</param>
        /// <param name="version">Semantic version string</param>
        /// <param name="category">Primary plugin category</param>
        public PluginInfoAttribute(
            string name,
            string description,
            string author = "DataWarehouse Team",
            string version = "1.0.0",
            PluginCategory category = PluginCategory.Other)
        {
            if (string.IsNullOrWhiteSpace(name))
                throw new ArgumentException("Plugin name cannot be empty", nameof(name));
            if (string.IsNullOrWhiteSpace(description))
                throw new ArgumentException("Plugin description cannot be empty", nameof(description));

            Name = name;
            Description = description;
            Author = author;
            Version = version;
            Category = category;
        }

        /// <summary>
        /// Gets the PluginInfo attribute from a type, if present.
        /// </summary>
        /// <param name="type">Type to inspect</param>
        /// <returns>PluginInfo attribute or null if not found</returns>
        public static PluginInfoAttribute? GetFromType(Type type)
        {
            return type.GetCustomAttributes(typeof(PluginInfoAttribute), false)
                .FirstOrDefault() as PluginInfoAttribute;
        }

        /// <summary>
        /// Validates that a type has the PluginInfo attribute.
        /// </summary>
        /// <param name="type">Type to validate</param>
        /// <returns>True if attribute is present and valid</returns>
        public static bool IsValid(Type type)
        {
            var info = GetFromType(type);
            return info != null &&
                   !string.IsNullOrWhiteSpace(info.Name) &&
                   !string.IsNullOrWhiteSpace(info.Description);
        }

        /// <summary>
        /// Returns a string representation of the plugin metadata.
        /// </summary>
        public override string ToString()
        {
            return $"{Name} v{Version} by {Author} - {Description}";
        }
    }
}
