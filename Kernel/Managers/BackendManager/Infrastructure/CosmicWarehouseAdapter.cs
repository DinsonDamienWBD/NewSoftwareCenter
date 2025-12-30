using System.Threading.Tasks;
using System.IO;
// Alias to avoid naming conflicts
using Core;
using DW = DataWarehouse;
using Core.Infrastructure;
using Core.AI;

namespace Manager.Infrastructure
{
    /// <summary>
    /// THE BRIDGE.
    /// Makes the standalone "Cosmic DataWarehouse" look like a "Core DataWarehouse".
    /// </summary>
    public class CosmicWarehouseAdapter : IDataWarehouse, ISemanticMemory
    {
        private readonly DW.Engine.CosmicWarehouse _engine;

        public CosmicWarehouseAdapter(DW.Engine.CosmicWarehouse engine)
        {
            _engine = engine;
        }

        // 1. Mapping Storage Calls
        public Task StoreObjectAsync(string bucket, string key, Stream data, Core.Data.StorageIntent intent)
        {
            // Map Core.Intent -> DW.Intent
            var dwIntent = new DW.StorageIntent(
                (DW.SecurityLevel)intent.Security,
                (DW.CompressionLevel)intent.Compression,
                (DW.AvailabilityLevel)intent.Availability
            );

            return _engine.StoreObjectAsync(bucket, key, data, dwIntent);
        }

        public Task<Stream> RetrieveObjectAsync(string bucket, string key)
            => _engine.RetrieveObjectAsync(bucket, key);

        public void CheckHealth() => _engine.CheckHealth();

        // 2. Mapping AI Calls
        public Task<string> MemorizeAsync(string content, string[] tags, string? summary = null)
            => _engine.MemorizeAsync(content, tags, summary);

        public Task<string> RecallAsync(string memoryId)
            => _engine.RecallAsync(memoryId);

        public Task<string[]> SearchMemoriesAsync(string query, float[]? vector, int limit = 5)
            => _engine.SearchMemoriesAsync(query, vector, limit);
    }
}