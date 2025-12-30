using DataWarehouse.Fabric; // For DurableState

namespace DataWarehouse.Configuration
{
    /// <summary>
    /// Feature manager to turn features on and off
    /// </summary>
    public class FeatureManager(string metadataPath)
    {
        // Map: PluginID -> IsEnabled (bool)
        private readonly DurableState<bool> _settings = new DurableState<bool>(metadataPath, "feature_flags");

        // Defines default behavior for new plugins (Safety first: Default to ON or OFF?)
        private const bool DefaultState = true;

        /// <summary>
        /// Check if plugin is enabled
        /// </summary>
        /// <param name="pluginId"></param>
        /// <returns></returns>
        public bool IsEnabled(string pluginId)
        {
            if (_settings.TryGet(pluginId, out var isEnabled))
            {
                return isEnabled;
            }
            return DefaultState;
        }

        /// <summary>
        /// Set feature on/off foa a plugin
        /// </summary>
        /// <param name="pluginId"></param>
        /// <param name="isEnabled"></param>
        public void SetFeatureState(string pluginId, bool isEnabled)
        {
            _settings.Set(pluginId, isEnabled);
        }

        /// <summary>
        /// Get all features for all installed plugins
        /// </summary>
        /// <param name="installedPluginIds"></param>
        /// <returns></returns>
        public Dictionary<string, bool> GetAllStates(IEnumerable<string> installedPluginIds)
        {
            var result = new Dictionary<string, bool>();
            foreach (var id in installedPluginIds)
            {
                result[id] = IsEnabled(id);
            }
            return result;
        }
    }
}