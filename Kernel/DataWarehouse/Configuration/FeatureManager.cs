using DataWarehouse.Fabric;
using System.Collections.Concurrent; // For DurableState

namespace DataWarehouse.Configuration
{
    /// <summary>
    /// Feature manager to turn features on and off
    /// </summary>
    public class FeatureManager(string metadataPath)
    {
        // Layer 1: Persistent User Overrides (Disk)
        private readonly DurableState<bool> _settings = new(metadataPath, "feature_flags");

        // Layer 2: Ephemeral/Smart Auto-Settings (RAM only)
        private readonly ConcurrentDictionary<string, bool> _ephemeral = new();

        private const bool DefaultState = true;


        /// <summary>
        /// Check if plugin is enabled
        /// </summary>
        /// <param name="pluginId"></param>
        /// <returns></returns>
        public bool IsEnabled(string pluginId)
        {
            // 1. User Explicit Setting (Highest Priority)
            if (_settings.TryGet(pluginId, out var userSetting)) return userSetting;

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
            return _settings.TryGet(pluginId, out _);
        }

        /// <summary>
        /// Set feature on/off foa a plugin
        /// </summary>
        /// <param name="pluginId"></param>
        /// <param name="isEnabled"></param>
        /// <param name="ephemeral"></param>
        public void SetFeatureState(string pluginId, bool isEnabled, bool ephemeral = false)
        {
            if (ephemeral)
            {
                // Set only in RAM
                _ephemeral[pluginId] = isEnabled;
            }
            else
            {
                // User is making a manual decision. Persist it.
                _settings.Set(pluginId, isEnabled);

                // Clear ephemeral so persistent takes precedence immediately
                _ephemeral.TryRemove(pluginId, out _);
            }
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