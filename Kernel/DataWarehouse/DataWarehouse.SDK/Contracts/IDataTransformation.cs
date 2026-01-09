namespace DataWarehouse.SDK.Contracts
{
    /// <summary>
    /// The Universal Contract.
    /// Any plugin that mutates data (Crypto, Zip, AI, Watermark) implements this.
    /// </summary>
    [Obsolete("Use PipelinePluginBase abstract class instead. Interfaces force code duplication. " +
              "PipelinePluginBase provides complete implementation - plugins only override ApplyTransformAsync() and ReverseTransformAsync(). " +
              "See RULES.md Section 6 for CategoryBase pattern.", error: false)]
    public interface IDataTransformation : IPlugin
    {
        /// <summary>
        /// Self-Identified Category.
        /// Examples: "Encryption", "Compression", "Sanitization", "Watermarking"
        /// </summary>
        string Category { get; }

        /// <summary>
        /// A generic score (1-100) for sorting.
        /// e.g. Compression: 10 = Fast, 90 = Small.
        /// e.g. Crypto: 10 = Fast, 90 = Quantum.
        /// </summary>
        int QualityLevel { get; }

        /// <summary>
        /// Outgoing: Called when Storing data (User -> Disk).
        /// Example: Encrypt, Compress.
        /// </summary>
        Stream OnWrite(Stream input, IKernelContext context, Dictionary<string, object> args);

        /// <summary>
        /// Incoming: Called when Retrieving data (Disk -> User).
        /// Example: Decrypt, Decompress.
        /// </summary>
        Stream OnRead(Stream stored, IKernelContext context, Dictionary<string, object> args);
    }
}