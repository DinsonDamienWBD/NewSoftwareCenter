using DataWarehouse.SDK.Primitives;

namespace DataWarehouse.SDK.Contracts
{
    /// <summary>
    /// Exposes tiering capabilities to other plugins (like Governance).
    /// </summary>
    public interface ITieredStorage : IPlugin
    {
        /// <summary>
        /// Moves a blob to the specified tier.
        /// </summary>
        Task<string> MoveToTierAsync(Manifest manifest, StorageTier targetTier);
    }
}
