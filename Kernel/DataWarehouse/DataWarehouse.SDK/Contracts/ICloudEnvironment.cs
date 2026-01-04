namespace DataWarehouse.SDK.Contracts
{
    /// <summary>
    /// Cloud environment interface
    /// </summary>
    public interface ICloudEnvironment : IPlugin
    {
        /// <summary>
        /// "AWS", "Azure", "Google", "Local"
        /// </summary>
        string EnvironmentName { get; }

        /// <summary>
        /// Logic to auto-detect if we are running in this environment.
        /// </summary>
        bool IsCurrentEnvironment();

        /// <summary>
        /// Get the Storage Provider optimized for this cloud (e.g., S3, Colossus).
        /// </summary>
        IStorageProvider CreateStorageProvider();
    }
}