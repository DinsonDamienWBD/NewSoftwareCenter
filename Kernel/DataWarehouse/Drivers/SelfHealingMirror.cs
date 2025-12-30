using DataWarehouse.Contracts;
using Microsoft.Extensions.Logging;

namespace DataWarehouse.Drivers
{
    /// <summary>
    /// Self healing feature
    /// </summary>
    public class SelfHealingMirror(IStorageProvider primary, IStorageProvider secondary, ILogger logger) : IStorageProvider
    {
        /// <summary>
        /// ID
        /// </summary>
        public string Id => "Mirror-RAID1";
        
        /// <summary>
        /// Version
        /// </summary>
        public string Version => "1.0";

        /// <summary>
        /// Scheme
        /// </summary>
        public string Scheme => "mirror";

        private readonly IStorageProvider _primary = primary;
        private readonly IStorageProvider _secondary = secondary;
        private readonly ILogger _logger = logger;

        /// <summary>
        /// Save data
        /// </summary>
        /// <param name="uri"></param>
        /// <param name="data"></param>
        /// <returns></returns>
        public async Task SaveAsync(Uri uri, Stream data)
        {
            // Robust Stream Handling: 
            // If stream is not seekable (Network), we must buffer it to Memory first
            // to allow writing to two destinations.
            Stream writeStream = data;
            if (!data.CanSeek)
            {
                var memoryBuffer = new MemoryStream();
                await data.CopyToAsync(memoryBuffer);
                memoryBuffer.Position = 0;
                writeStream = memoryBuffer;
            }

            // 1. Primary Write (Critical Path)
            try
            {
                await _primary.SaveAsync(uri, writeStream);
            }
            catch (Exception ex)
            {
                _logger.LogError($"Mirror Primary Write Failed: {ex.Message}");
                throw; // Strict Consistency: If Primary fails, operation fails.
            }

            // 2. Secondary Write (Best Effort / Background)
            try
            {
                writeStream.Position = 0;
                await _secondary.SaveAsync(uri, writeStream);
            }
            catch (Exception ex)
            {
                _logger.LogWarning($"Mirror Secondary Write Failed: {ex.Message}");
                // We do NOT throw here. We degrade to single-node availability.
            }
        }

        /// <summary>
        /// Load data
        /// </summary>
        /// <param name="uri"></param>
        /// <returns></returns>
        public async Task<Stream> LoadAsync(Uri uri)
        {
            try
            {
                return await _primary.LoadAsync(uri);
            }
            catch (Exception ex)
            {
                _logger.LogWarning($"Primary Load Failed: {ex.Message}. Failing over.");

                // Failover
                var stream = await _secondary.LoadAsync(uri);

                // COSMIC FEATURE: Self-Healing
                // Automatically repair the primary node using data from secondary
                _ = Task.Run(() => RepairPrimaryAsync(uri));

                return stream;
            }
        }

        private async Task RepairPrimaryAsync(Uri uri)
        {
            try
            {
                using var stream = await _secondary.LoadAsync(uri);
                await _primary.SaveAsync(uri, stream);
                _logger.LogInformation($"Self-Healing: Repaired {uri} on Primary.");
            }
            catch (Exception ex) 
            {
                _logger.LogError($"Heal Failed: {ex.Message}");
            }
        }

        /// <summary>
        /// Delete data
        /// </summary>
        /// <param name="uri"></param>
        /// <returns></returns>
        public async Task DeleteAsync(Uri uri)
        {
            await _primary.DeleteAsync(uri);
            await _secondary.DeleteAsync(uri);
        }

        /// <summary>
        /// Check if data exists
        /// </summary>
        /// <param name="uri"></param>
        /// <returns></returns>
        public async Task<bool> ExistsAsync(Uri uri)
            => await _primary.ExistsAsync(uri) || await _secondary.ExistsAsync(uri);
    }
}