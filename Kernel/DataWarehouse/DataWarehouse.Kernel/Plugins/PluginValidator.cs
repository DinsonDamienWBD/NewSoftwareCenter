using DataWarehouse.SDK.Contracts;
using System.Reflection;

namespace DataWarehouse.DataWarehouse.Kernel.Plugins
{
    /// <summary>
    /// Validates plugin assemblies and implementations before loading.
    /// Ensures plugins meet minimum requirements and won't cause runtime errors.
    /// </summary>
    public class PluginValidator
    {
        private readonly IKernelContext _context;

        public PluginValidator(IKernelContext context)
        {
            _context = context ?? throw new ArgumentNullException(nameof(context));
        }

        /// <summary>
        /// Validate a plugin assembly before loading.
        /// </summary>
        public PluginValidationResult ValidateAssembly(string assemblyPath)
        {
            var result = new PluginValidationResult { AssemblyPath = assemblyPath };

            try
            {
                // Check file exists
                if (!File.Exists(assemblyPath))
                {
                    result.Errors.Add($"Assembly file not found: {assemblyPath}");
                    return result;
                }

                // Load assembly
                Assembly assembly;
                try
                {
                    assembly = Assembly.LoadFrom(assemblyPath);
                }
                catch (Exception ex)
                {
                    result.Errors.Add($"Failed to load assembly: {ex.Message}");
                    return result;
                }

                // Find plugin types
                var pluginTypes = assembly.GetTypes()
                    .Where(t => typeof(IPlugin).IsAssignableFrom(t) && !t.IsInterface && !t.IsAbstract)
                    .ToList();

                if (pluginTypes.Count == 0)
                {
                    result.Warnings.Add("No plugin implementations found in assembly");
                    return result;
                }

                // Validate each plugin type
                foreach (var pluginType in pluginTypes)
                {
                    ValidatePluginType(pluginType, result);
                }

                result.IsValid = result.Errors.Count == 0;
                result.PluginCount = pluginTypes.Count;
            }
            catch (Exception ex)
            {
                result.Errors.Add($"Validation failed: {ex.Message}");
            }

            return result;
        }

        /// <summary>
        /// Validate a plugin implementation.
        /// </summary>
        public PluginValidationResult ValidatePlugin(IPlugin plugin, HandshakeResponse response)
        {
            var result = new PluginValidationResult
            {
                AssemblyPath = plugin.GetType().Assembly.Location,
                PluginCount = 1
            };

            try
            {
                // Validate handshake response
                ValidateHandshakeResponse(response, result);

                // Validate plugin implementation
                ValidatePluginImplementation(plugin, result);

                // Validate dependencies
                ValidateDependencies(response, result);

                // Validate capabilities
                ValidateCapabilities(response, result);

                result.IsValid = result.Errors.Count == 0;
            }
            catch (Exception ex)
            {
                result.Errors.Add($"Plugin validation failed: {ex.Message}");
            }

            return result;
        }

        private void ValidatePluginType(Type pluginType, PluginValidationResult result)
        {
            // Check for public constructor
            var constructors = pluginType.GetConstructors(BindingFlags.Public | BindingFlags.Instance);
            if (constructors.Length == 0)
            {
                result.Errors.Add($"Plugin '{pluginType.Name}' has no public constructor");
            }

            // Check for OnHandshakeAsync implementation
            var handshakeMethod = pluginType.GetMethod("OnHandshakeAsync");
            if (handshakeMethod == null)
            {
                result.Errors.Add($"Plugin '{pluginType.Name}' does not implement OnHandshakeAsync");
            }

            // Check for required attributes (optional but recommended)
            var attrs = pluginType.GetCustomAttributes(typeof(PluginInfoAttribute), false);
            if (attrs.Length == 0)
            {
                result.Warnings.Add($"Plugin '{pluginType.Name}' missing PluginInfo attribute (recommended)");
            }

            // Check namespace conventions
            if (!pluginType.Namespace?.StartsWith("DataWarehouse.") == true)
            {
                result.Warnings.Add($"Plugin '{pluginType.Name}' not in DataWarehouse namespace");
            }
        }

        private void ValidateHandshakeResponse(HandshakeResponse response, PluginValidationResult result)
        {
            // Validate plugin ID
            if (string.IsNullOrWhiteSpace(response.PluginId))
            {
                result.Errors.Add("Plugin ID cannot be empty");
            }
            else if (response.PluginId.Contains(' '))
            {
                result.Errors.Add("Plugin ID cannot contain spaces");
            }
            else if (!response.PluginId.StartsWith("DataWarehouse."))
            {
                result.Warnings.Add("Plugin ID should start with 'DataWarehouse.' prefix");
            }

            // Validate name
            if (string.IsNullOrWhiteSpace(response.Name))
            {
                result.Errors.Add("Plugin name cannot be empty");
            }

            // Validate version
            if (response.Version == null || response.Version.Major < 0)
            {
                result.Errors.Add("Invalid plugin version");
            }

            // Validate category
            if (!Enum.IsDefined(typeof(PluginCategory), response.Category))
            {
                result.Errors.Add($"Invalid plugin category: {response.Category}");
            }

            // Check for success
            if (!response.IsSuccess && string.IsNullOrEmpty(response.ErrorMessage))
            {
                result.Errors.Add("Failed plugin response missing error message");
            }
        }

        private void ValidatePluginImplementation(IPlugin plugin, PluginValidationResult result)
        {
            var pluginType = plugin.GetType();

            // Check for IDisposable if plugin allocates resources
            var hasDisposePattern = typeof(IDisposable).IsAssignableFrom(pluginType);
            if (!hasDisposePattern)
            {
                result.Warnings.Add($"Plugin '{pluginType.Name}' does not implement IDisposable (consider if cleanup needed)");
            }

            // Check for async methods
            var methods = pluginType.GetMethods(BindingFlags.Public | BindingFlags.Instance);
            var asyncMethods = methods.Where(m => m.ReturnType == typeof(Task) || m.ReturnType.IsGenericType && m.ReturnType.GetGenericTypeDefinition() == typeof(Task<>));

            if (asyncMethods.Any())
            {
                // Good - using async
            }
            else
            {
                result.Warnings.Add($"Plugin '{pluginType.Name}' has no async methods (may block)");
            }
        }

        private void ValidateDependencies(HandshakeResponse response, PluginValidationResult result)
        {
            foreach (var dependency in response.Dependencies)
            {
                // Validate dependency structure
                if (string.IsNullOrWhiteSpace(dependency.RequiredInterface))
                {
                    result.Errors.Add("Dependency missing required interface name");
                    continue;
                }

                // Check if interface name looks valid
                if (!dependency.RequiredInterface.StartsWith("I"))
                {
                    result.Warnings.Add($"Dependency interface '{dependency.RequiredInterface}' doesn't follow naming convention (should start with 'I')");
                }

                // Validate optional dependencies have reason
                if (dependency.IsOptional && string.IsNullOrWhiteSpace(dependency.Reason))
                {
                    result.Warnings.Add($"Optional dependency '{dependency.RequiredInterface}' should include reason");
                }

                // Check version constraints
                if (dependency.MinimumVersion != null && dependency.MinimumVersion.Major < 0)
                {
                    result.Errors.Add($"Invalid minimum version for dependency '{dependency.RequiredInterface}'");
                }
            }

            // Check for circular dependencies (basic check)
            var interfaceNames = response.Dependencies.Select(d => d.RequiredInterface).ToHashSet();
            if (interfaceNames.Contains("IPlugin"))
            {
                result.Errors.Add("Plugin cannot depend on IPlugin itself");
            }
        }

        private void ValidateCapabilities(HandshakeResponse response, PluginValidationResult result)
        {
            foreach (var capability in response.Capabilities)
            {
                // Validate capability ID
                if (string.IsNullOrWhiteSpace(capability.CapabilityId))
                {
                    result.Errors.Add("Capability missing ID");
                    continue;
                }

                // Check ID format (category.plugin.action)
                var parts = capability.CapabilityId.Split('.');
                if (parts.Length < 3)
                {
                    result.Warnings.Add($"Capability ID '{capability.CapabilityId}' should follow format 'category.plugin.action'");
                }

                // Validate display name
                if (string.IsNullOrWhiteSpace(capability.DisplayName))
                {
                    result.Warnings.Add($"Capability '{capability.CapabilityId}' missing display name");
                }

                // Validate description
                if (string.IsNullOrWhiteSpace(capability.Description))
                {
                    result.Warnings.Add($"Capability '{capability.CapabilityId}' missing description (required for AI)");
                }

                // Validate category
                if (!Enum.IsDefined(typeof(CapabilityCategory), capability.Category))
                {
                    result.Errors.Add($"Capability '{capability.CapabilityId}' has invalid category");
                }

                // Validate permission
                if (!Enum.IsDefined(typeof(SDK.Security.Permission), capability.RequiredPermission))
                {
                    result.Errors.Add($"Capability '{capability.CapabilityId}' has invalid required permission");
                }

                // Validate parameter schema (should be valid JSON)
                if (!string.IsNullOrEmpty(capability.ParameterSchemaJson))
                {
                    try
                    {
                        System.Text.Json.JsonDocument.Parse(capability.ParameterSchemaJson);
                    }
                    catch
                    {
                        result.Errors.Add($"Capability '{capability.CapabilityId}' has invalid parameter schema JSON");
                    }
                }

                // Check tags
                if (capability.Tags == null || capability.Tags.Count == 0)
                {
                    result.Warnings.Add($"Capability '{capability.CapabilityId}' has no tags (helpful for AI discovery)");
                }
            }
        }

        /// <summary>
        /// Run comprehensive plugin compatibility check.
        /// </summary>
        public PluginCompatibilityResult CheckCompatibility(
            IPlugin plugin,
            HandshakeResponse response,
            List<PluginDescriptor> loadedPlugins,
            string protocolVersion)
        {
            var compatResult = new PluginCompatibilityResult
            {
                PluginId = response.PluginId,
                IsCompatible = true
            };

            // Check protocol version
            if (response.Metadata.TryGetValue("ProtocolVersion", out var pluginProtocol))
            {
                if (pluginProtocol.ToString() != protocolVersion)
                {
                    compatResult.Warnings.Add($"Protocol version mismatch: Plugin uses {pluginProtocol}, Kernel uses {protocolVersion}");
                }
            }

            // Check dependencies are satisfied
            foreach (var dependency in response.Dependencies)
            {
                var satisfied = loadedPlugins.Any(p => p.Interfaces.Contains(dependency.RequiredInterface));

                if (!satisfied && !dependency.IsOptional)
                {
                    compatResult.Issues.Add($"Required dependency not met: {dependency.RequiredInterface}");
                    compatResult.IsCompatible = false;
                }
                else if (!satisfied && dependency.IsOptional)
                {
                    compatResult.Warnings.Add($"Optional dependency not available: {dependency.RequiredInterface}");
                }
            }

            // Check for conflicting plugins (same capability IDs)
            var capabilityIds = response.Capabilities.Select(c => c.CapabilityId).ToHashSet();
            foreach (var loadedPlugin in loadedPlugins)
            {
                // Would need to check loaded plugin capabilities here
                // For now, just check plugin ID conflicts
                if (loadedPlugin.PluginId == response.PluginId)
                {
                    compatResult.Issues.Add($"Plugin with ID '{response.PluginId}' already loaded");
                    compatResult.IsCompatible = false;
                }
            }

            return compatResult;
        }
    }

    public class PluginValidationResult
    {
        public string? AssemblyPath { get; set; }
        public int PluginCount { get; set; }
        public bool IsValid { get; set; }
        public List<string> Errors { get; } = new();
        public List<string> Warnings { get; } = new();

        public override string ToString()
        {
            var status = IsValid ? "VALID" : "INVALID";
            return $"[{status}] {AssemblyPath}: {PluginCount} plugins, {Errors.Count} errors, {Warnings.Count} warnings";
        }
    }

    public class PluginCompatibilityResult
    {
        public required string PluginId { get; init; }
        public bool IsCompatible { get; set; }
        public List<string> Issues { get; } = new();
        public List<string> Warnings { get; } = new();

        public override string ToString()
        {
            var status = IsCompatible ? "COMPATIBLE" : "INCOMPATIBLE";
            return $"[{status}] {PluginId}: {Issues.Count} issues, {Warnings.Count} warnings";
        }
    }

    /// <summary>
    /// Attribute for plugin metadata (optional but recommended).
    /// </summary>
    [AttributeUsage(AttributeTargets.Class, AllowMultiple = false)]
    public class PluginInfoAttribute : Attribute
    {
        public string Name { get; init; }
        public string Description { get; init; }
        public string Author { get; init; }

        public PluginInfoAttribute(string name, string description, string author = "DataWarehouse")
        {
            Name = name;
            Description = description;
            Author = author;
        }
    }
}
