namespace Core.Messages
{
    // -------------------------------------------------------------------------
    // RUNTIME DOCUMENTATION API CONTRACTS
    // -------------------------------------------------------------------------

    /// <summary>Query to retrieve the full system capability manifest.</summary>
    public class GetSystemManifestQuery : Query<SystemManifest> { }

    /// <summary>
    /// System capability manifest.
    /// </summary>
    public class SystemManifest
    {
        /// <summary>
        /// Version of the kernel.
        /// </summary>
        public string? KernelVersion { get; set; }

        /// <summary>
        /// List of loaded modules and their commands.
        /// </summary>
        public List<ModuleDefinition> Modules { get; set; } = [];
    }

    /// <summary>
    /// Definition of a loaded module and its commands.
    /// </summary>
    public class ModuleDefinition
    {
        /// <summary>
        /// Identifier of the module.
        /// </summary>
        public string? Id { get; set; }

        /// <summary>
        /// Version of the module.
        /// </summary>
        public string? Version { get; set; }

        /// <summary>
        /// Description of the module.
        /// </summary>
        public string? Description { get; set; }

        /// <summary>
        /// List of commands provided by the module.
        /// </summary>
        public List<CommandDefinition> Commands { get; set; } = [];
    }

    /// <summary>
    /// Command definition within a module.
    /// </summary>
    public class CommandDefinition
    {
        /// <summary>
        /// Name of the command.
        /// </summary>
        public string? Name { get; set; }

        /// <summary>
        /// Description of the command.
        /// </summary>
        public string? Description { get; set; }

        /// <summary>
        /// Example of how to use the command in JSON format.
        /// </summary>
        public string? UsageExampleJson { get; set; }

        /// <summary>
        /// Type of the command (e.g., "action", "query").
        /// </summary>
        public string? Type { get; set; }
    }
}