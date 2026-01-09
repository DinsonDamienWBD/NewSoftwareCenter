using DataWarehouse.SDK.Contracts;
using DataWarehouse.Plugins.Feature.Tiering.Engine;

namespace DataWarehouse.Plugins.Feature.Tiering.Bootstrapper
{
    [PluginInfo(
        name: "Storage Tiering Feature",
        description: "Automatic storage tiering (hot/warm/cold) for cost optimization",
        author: "DataWarehouse Team",
        version: "1.0.0",
        category: PluginCategory.Feature
    )]
    public class TieringFeaturePlugin
    {
        public static TieringFeatureEngine CreateInstance() => new();
    }
}
