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
        public static PluginInfo PluginInfo => new()
        {
            Id = "feature.tiering",
            Name = "Storage Tiering Feature",
            Version = new Version(1, 0, 0),
            Author = "DataWarehouse SDK",
            Description = "Automatic storage tiering (hot/warm/cold) for cost optimization",
            Category = PluginCategory.Feature,
            Tags = new[] { "feature", "tiering", "storage", "optimization", "lifecycle" }
        };

        public static TieringFeatureEngine CreateInstance() => new();
    }
}
