using DataWarehouse.SDK.Utilities;
using System.Collections.Concurrent;

namespace DataWarehouse.Kernel.Configuration
{
    /// <summary>
    /// Feature manager to turn features on and off.
    /// Uses DurableStateV2 for storage-agnostic persistence with write-ahead logging.
    /// </summary>
    public class FeatureManager
    {
        // Layer 1: Persistent User Overrides (Storage-backed with DurableStateV2)
        private readonly DurableStateV2<bool> _features;

        // Layer 2: Ephemeral/Smart Auto-Settings (RAM only)
        private readonly ConcurrentDictionary<string, bool> _ephemeral = new();

        private const bool DefaultState = true;

        /// <summary>
        /// Initializes the feature manager with storage-backed persistence.
        /// </summary>
        /// <param name="rootPath">Root path for DataWarehouse metadata storage</param>
        public FeatureManager(string rootPath)
        {
            // Create simple local storage provider for internal use
            var storageProvider = new SimpleLocalStorageProvider(rootPath);

            // Initialize DurableStateV2 with features journal
            _features = new DurableStateV2<bool>(storageProvider, "features.journal");
        }

        /// <summary>
        /// Check if plugin is enabled
        /// </summary>
        /// <param name="pluginId"></param>
        /// <returns></returns>
        public bool IsEnabled(string pluginId)
        {
            // 1. User Explicit Setting (Highest Priority)
            if (_features.TryGet(pluginId, out var userSetting)) return userSetting;

            // 2. Smart/Ephemeral Setting
            if (_ephemeral.TryGetValue(pluginId, out var autoSetting)) return autoSetting;

            // 3. Global Default
            return DefaultState;
        }

        /// <summary>
        /// Checks if explicit settings exist
        /// </summary>
        /// <param name="pluginId"></param>
        /// <returns></returns>
        public bool HasExplicitSetting(string pluginId)
        {
            return _features.TryGet(pluginId, out _);
        }

        /// <summary>
        /// Set feature on/off for a plugin
        /// </summary>
        /// <param name="pluginId"></param>
        /// <param name="isEnabled"></param>
        /// <param name="ephemeral"></param>
        public async Task SetFeatureStateAsync(string pluginId, bool isEnabled, bool ephemeral = false)
        {
            if (ephemeral)
            {
                // Set only in RAM
                _ephemeral[pluginId] = isEnabled;
            }
            else
            {
                // User is making a manual decision. Persist it.
                await _features.SetAsync(pluginId, isEnabled);

                // Clear ephemeral so persistent takes precedence immediately
                _ephemeral.TryRemove(pluginId, out _);
            }
        }

        /// <summary>
        /// Set feature on/off for a plugin (synchronous wrapper).
        /// </summary>
        public void SetFeatureState(string pluginId, bool isEnabled, bool ephemeral = false)
        {
            SetFeatureStateAsync(pluginId, isEnabled, ephemeral).GetAwaiter().GetResult();
        }

        /// <summary>
        /// Get all features for all installed plugins
        /// </summary>
        /// <param name="installedPluginIds"></param>
        /// <returns></returns>
        public Dictionary<string, bool> GetAllStates(IEnumerable<string> installedPluginIds)
        {
            var result = new Dictionary<string, bool>();
            foreach (var id in installedPluginIds) result[id] = IsEnabled(id);
            return result;
        }
    }
}