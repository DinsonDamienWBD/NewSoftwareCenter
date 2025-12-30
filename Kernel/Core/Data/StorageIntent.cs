namespace Core.Data
{
    /// <summary>
    /// Level of security to be used
    /// </summary>
    public enum SecurityLevel
    {
        /// <summary>
        /// No security
        /// </summary>
        None,

        /// <summary>
        /// Standard security level
        /// </summary>
        Standard,

        /// <summary>
        /// High level of security
        /// </summary>
        High,

        /// <summary>
        /// Top level security
        /// </summary>
        Quantum
    }

    /// <summary>
    /// Level of compression to b∈ used
    /// </summary>
    public enum CompressionLevel
    {
        /// <summary>
        /// No compression
        /// </summary>
        None,

        /// <summary>
        /// Fast but less compression
        /// </summary>
        Fast,

        /// <summary>
        /// Optimal balance between compression and performance
        /// </summary>
        Optimal
    }

    /// <summary>
    /// Level of availability
    /// </summary>
    public enum AvailabilityLevel
    {
        /// <summary>
        /// Single copy, no redundancy
        /// </summary>
        Single,

        /// <summary>
        /// Redundancy applied
        /// </summary>
        Redundant,

        /// <summary>
        /// Geo level redundancy applied
        /// </summary>
        GeoRedundant
    }

    /// <summary>
    /// A record defining a storage intent
    /// Expresses the "Why" and "How" of storage.
    /// Used by Modules to request specific storage behaviors from the Kernel.
    /// </summary>
    /// <param name="Security"></param>
    /// <param name="Compression"></param>
    /// <param name="Availability"></param>
    public record StorageIntent(
        SecurityLevel Security = SecurityLevel.Standard,
        CompressionLevel Compression = CompressionLevel.Optimal,
        AvailabilityLevel Availability = AvailabilityLevel.Single
    );
}