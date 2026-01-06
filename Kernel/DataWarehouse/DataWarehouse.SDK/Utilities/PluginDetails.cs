using DataWarehouse.SDK.Primitives;

namespace DataWarehouse.SDK.Utilities
{
    public class PluginDependency
    {
        public string RequiredInterface { get; init; } = string.Empty;  // "IMetadataIndex"
        public bool IsOptional { get; init; }
        public string Reason { get; init; } = string.Empty;  // "Needed for manifest lookups"
    }

    public class PluginCapabilityDescriptor
    {
        public string CapabilityId { get; init; } = string.Empty;
        public string DisplayName { get; init; } = string.Empty;
        public string Description { get; init; } = string.Empty;
        public CapabilityCategory Category { get; init; }
        public bool RequiresApproval { get; init; }
        public Permission RequiredPermission { get; init; }
        // JSON schema as string (or JObject)
        public string ParameterSchemaJson { get; init; } = "{}";
    }
}
