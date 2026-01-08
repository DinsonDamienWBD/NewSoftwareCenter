namespace DataWarehouse.SDK.Primitives
{
    /// <summary>
    /// Pipeline configuration
    /// </summary>
    public class PipelineConfig
    {
        /// <summary>
        /// Enable encryption
        /// </summary>
        public bool EnableEncryption { get; set; }

        /// <summary>
        /// Crypto provider ID
        /// </summary>
        public string CryptoProviderId { get; set; } = string.Empty;

        /// <summary>
        /// Key ID
        /// </summary>
        public string KeyId { get; set; } = string.Empty;

        /// <summary>
        /// Enable compression
        /// </summary>
        public bool EnableCompression { get; set; }

        /// <summary>
        /// Compression provider ID
        /// </summary>
        public string CompressionProviderId { get; set; } = string.Empty;

        /// <summary>
        /// Order of operations (e.g. Compress -> Encrypt)
        /// </summary>
        public List<string> TransformationOrder { get; set; } = [];
    }
}
