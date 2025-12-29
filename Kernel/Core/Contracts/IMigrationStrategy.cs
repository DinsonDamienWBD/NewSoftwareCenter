namespace Core.Contracts
{
    /// <summary>
    /// Handles upgrading old data schemas to the current version.
    /// </summary>
    /// <typeparam name="TData">The target data type.</typeparam>
    public interface IMigrationStrategy<TData>
    {
        /// <summary>
        /// Converts raw data (usually JObject or Dictionary) from an old version to the current TData.
        /// </summary>
        /// <param name="rawData">The raw deserialized object.</param>
        /// <param name="schemaVersion">The version found in the source.</param>
        TData Upgrade(object rawData, int schemaVersion);
    }
}