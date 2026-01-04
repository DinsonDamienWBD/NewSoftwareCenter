using DataWarehouse.SDK.Contracts;

namespace DataWarehouse.SDK.Primitives
{
    /// <summary>
    /// Describes the Service Level Agreement (SLA) for a specific blob.
    /// This tells the Storage Pool how to treat the data (Encryption, Speed, Safety).
    /// </summary>
    public class StorageIntent
    {
        /// <summary>
        /// What level of compression is required?
        /// </summary>
        public CompressionLevel Compression { get; set; } = CompressionLevel.None;

        /// <summary>
        /// What level of data safety/redundancy is required?
        /// </summary>
        public AvailabilityLevel Availability { get; set; } = AvailabilityLevel.Single;

        /// <summary>
        /// What level of security (Encryption) is required?
        /// </summary>
        public SecurityLevel Security { get; set; } = SecurityLevel.Standard;

        // --- God Tier Presets ---

        /// <summary>
        /// Low latency, no overhead. (e.g., Temp files, RAM cache)
        /// </summary>
        public static StorageIntent Hot => new()
        {
            Compression = CompressionLevel.None,
            Availability = AvailabilityLevel.Single,
            Security = SecurityLevel.None
        };

        /// <summary>
        /// Balanced performance and safety. (e.g., User documents)
        /// </summary>
        public static StorageIntent Standard => new()
        {
            Compression = CompressionLevel.Fast,
            Availability = AvailabilityLevel.Redundant,
            Security = SecurityLevel.Standard
        };

        /// <summary>
        /// Maximum efficiency and safety, regardless of speed. (e.g., Legal backups)
        /// </summary>
        public static StorageIntent Archive => new()
        {
            Compression = CompressionLevel.Optimal,
            Availability = AvailabilityLevel.GeoRedundant,
            Security = SecurityLevel.High
        };
    }
}