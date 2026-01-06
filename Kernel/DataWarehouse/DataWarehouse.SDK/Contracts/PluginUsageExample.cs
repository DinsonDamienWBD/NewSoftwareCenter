namespace DataWarehouse.SDK.Contracts
{
    /// <summary>
    /// Represents a usage example for a plugin capability.
    /// Used by AI to learn typical usage patterns and generate correct invocations.
    ///
    /// Examples help AI understand:
    /// - Common use cases for capabilities
    /// - Typical parameter values
    /// - Expected input/output formats
    /// - Real-world scenarios
    /// </summary>
    public class PluginUsageExample
    {
        /// <summary>
        /// Human-readable title for this example.
        ///
        /// Examples:
        /// - "Compress large log file"
        /// - "Encrypt sensitive user data"
        /// - "Store backup to S3"
        /// </summary>
        public string Title { get; init; } = string.Empty;

        /// <summary>
        /// Natural language description of what this example does.
        /// Used by AI to understand the scenario.
        ///
        /// Examples:
        /// - "Compress a 100MB log file to reduce storage costs"
        /// - "Encrypt customer PII data before storing in database"
        /// - "Upload encrypted backup to AWS S3 with lifecycle policy"
        /// </summary>
        public string Description { get; init; } = string.Empty;

        /// <summary>
        /// The capability ID this example demonstrates.
        ///
        /// Examples:
        /// - "transform.gzip.apply"
        /// - "transform.aes.apply"
        /// - "storage.s3.write"
        /// </summary>
        public string CapabilityId { get; init; } = string.Empty;

        /// <summary>
        /// Example input data description.
        /// Describes what kind of data this example expects.
        ///
        /// Examples:
        /// - "100MB uncompressed log file (text/plain)"
        /// - "JSON document containing user credentials"
        /// - "Binary video file (1GB MP4)"
        /// </summary>
        public string InputDescription { get; init; } = string.Empty;

        /// <summary>
        /// Example parameters for this capability invocation.
        /// Dictionary of parameter name → example value.
        ///
        /// Examples:
        /// - { "level": 6, "buffer_size": 8192 }
        /// - { "key": "base64_encoded_key", "mode": "CBC" }
        /// - { "bucket": "backups", "region": "us-west-2" }
        /// </summary>
        public Dictionary<string, object> ExampleParameters { get; init; } = new();

        /// <summary>
        /// Expected output description.
        /// Describes what the capability will produce.
        ///
        /// Examples:
        /// - "Compressed data (~30MB GZIP format)"
        /// - "AES-256 encrypted bytes with prepended IV"
        /// - "S3 object URL: s3://backups/file.enc"
        /// </summary>
        public string ExpectedOutput { get; init; } = string.Empty;

        /// <summary>
        /// Tags for categorizing examples.
        /// Used by AI for filtering and searching examples.
        ///
        /// Examples:
        /// - ["performance", "large-files", "logs"]
        /// - ["security", "encryption", "pii"]
        /// - ["backup", "cloud", "lifecycle"]
        /// </summary>
        public string[] Tags { get; init; } = Array.Empty<string>();

        /// <summary>
        /// Constructs an empty usage example.
        /// </summary>
        public PluginUsageExample()
        {
        }

        /// <summary>
        /// Constructs a usage example with basic information.
        /// </summary>
        /// <param name="title">Example title.</param>
        /// <param name="description">Example description.</param>
        /// <param name="capabilityId">Capability this example demonstrates.</param>
        public PluginUsageExample(string title, string description, string capabilityId)
        {
            Title = title;
            Description = description;
            CapabilityId = capabilityId;
        }

        /// <summary>
        /// Constructs a fully specified usage example.
        /// </summary>
        /// <param name="title">Example title.</param>
        /// <param name="description">Example description.</param>
        /// <param name="capabilityId">Capability this example demonstrates.</param>
        /// <param name="inputDescription">Input data description.</param>
        /// <param name="exampleParameters">Example parameters.</param>
        /// <param name="expectedOutput">Expected output description.</param>
        /// <param name="tags">Categorization tags.</param>
        public PluginUsageExample(
            string title,
            string description,
            string capabilityId,
            string inputDescription,
            Dictionary<string, object> exampleParameters,
            string expectedOutput,
            string[] tags)
        {
            Title = title;
            Description = description;
            CapabilityId = capabilityId;
            InputDescription = inputDescription;
            ExampleParameters = exampleParameters;
            ExpectedOutput = expectedOutput;
            Tags = tags;
        }
    }
}
