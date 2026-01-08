namespace DataWarehouse.SDK.Primitives
{
    /// <summary>
    /// Configuration object for initializing the DataWarehouse Kernel.
    /// Defines global defaults and security overrides.
    /// </summary>
    public class GlobalPolicyConfig
    {
        /// <summary>
        /// The default order of middleware transformations.
        /// Default: ["Compression", "Encryption"]
        /// </summary>
        public List<string>? DefaultPipelineOrder { get; set; }

        /// <summary>
        /// Should encryption be enabled by default for new containers?
        /// </summary>
        public bool DefaultEnableEncryption { get; set; } = true;

        /// <summary>
        /// Should compression be enabled by default for new containers?
        /// </summary>
        public bool DefaultEnableCompression { get; set; } = true;
    }
}