using System.Collections.Generic;
using System.Linq;
using DataWarehouse.SDK.Contracts;

namespace DataWarehouse.SDK.AI.LLM
{
    /// <summary>
    /// Generates LLM tool definitions from DataWarehouse capabilities.
    /// Converts plugin capabilities into JSON Schema format for tool calling.
    ///
    /// Used by AI Runtime to:
    /// - Expose capabilities to LLM
    /// - Enable LLM to invoke capabilities
    /// - Provide parameter schemas
    /// - Generate tool descriptions
    ///
    /// Process:
    /// 1. Read capability descriptors from plugins
    /// 2. Convert to LLMTool format
    /// 3. Generate JSON Schema for parameters
    /// 4. Add semantic descriptions for LLM understanding
    /// </summary>
    public class ToolDefinitionGenerator
    {
        /// <summary>
        /// Generates LLM tool definitions from capability descriptors.
        /// </summary>
        /// <param name="capabilities">List of plugin capabilities.</param>
        /// <returns>List of LLM tool definitions.</returns>
        public List<LLMTool> GenerateToolDefinitions(List<PluginCapabilityDescriptor> capabilities)
        {
            var tools = new List<LLMTool>();

            foreach (var capability in capabilities)
            {
                var tool = new LLMTool
                {
                    Name = capability.Id,
                    Description = capability.Description,
                    Parameters = GenerateParameterSchema(capability)
                };

                tools.Add(tool);
            }

            return tools;
        }

        /// <summary>
        /// Generates JSON Schema for capability parameters.
        /// LLM uses this to understand what arguments to provide.
        ///
        /// Example output:
        /// {
        ///   "type": "object",
        ///   "properties": {
        ///     "data": { "type": "string", "description": "Data to compress" },
        ///     "level": { "type": "string", "enum": ["fastest", "optimal"], "description": "Compression level" }
        ///   },
        ///   "required": ["data"]
        /// }
        /// </summary>
        private Dictionary<string, object> GenerateParameterSchema(PluginCapabilityDescriptor capability)
        {
            var schema = new Dictionary<string, object>
            {
                ["type"] = "object",
                ["properties"] = new Dictionary<string, object>(),
                ["required"] = new List<string>()
            };

            var properties = (Dictionary<string, object>)schema["properties"];
            var required = (List<string>)schema["required"];

            // Add common parameters based on capability type
            if (capability.Id.Contains("transform"))
            {
                // Transformation capabilities
                properties["data"] = new Dictionary<string, object>
                {
                    ["type"] = "string",
                    ["description"] = "Data to transform (base64 encoded)"
                };
                required.Add("data");

                // Optional parameters
                if (capability.Id.Contains("gzip"))
                {
                    properties["level"] = new Dictionary<string, object>
                    {
                        ["type"] = "string",
                        ["enum"] = new List<string> { "fastest", "optimal", "nocompression" },
                        ["description"] = "Compression level (default: fastest)"
                    };
                }
                else if (capability.Id.Contains("aes"))
                {
                    properties["key"] = new Dictionary<string, object>
                    {
                        ["type"] = "string",
                        ["description"] = "Encryption key (base64 encoded 256-bit key)"
                    };
                    required.Add("key");
                }
            }
            else if (capability.Id.Contains("storage"))
            {
                // Storage capabilities
                if (capability.Id.Contains("write") || capability.Id.Contains("save"))
                {
                    properties["key"] = new Dictionary<string, object>
                    {
                        ["type"] = "string",
                        ["description"] = "Storage key/path"
                    };
                    properties["data"] = new Dictionary<string, object>
                    {
                        ["type"] = "string",
                        ["description"] = "Data to store (base64 encoded)"
                    };
                    required.Add("key");
                    required.Add("data");
                }
                else if (capability.Id.Contains("read") || capability.Id.Contains("load"))
                {
                    properties["key"] = new Dictionary<string, object>
                    {
                        ["type"] = "string",
                        ["description"] = "Storage key/path to read"
                    };
                    required.Add("key");
                }
            }
            else if (capability.Id.Contains("metadata"))
            {
                // Metadata/search capabilities
                properties["query"] = new Dictionary<string, object>
                {
                    ["type"] = "string",
                    ["description"] = "Search query"
                };
                properties["limit"] = new Dictionary<string, object>
                {
                    ["type"] = "integer",
                    ["description"] = "Maximum results to return (default: 10)"
                };
                required.Add("query");
            }

            return schema;
        }

        /// <summary>
        /// Filters tools based on user context and permissions.
        /// Only exposes capabilities the user is allowed to use.
        ///
        /// Use cases:
        /// - Hide admin capabilities from regular users
        /// - Respect ACL permissions
        /// - Filter by capability category
        /// </summary>
        /// <param name="tools">All available tools.</param>
        /// <param name="allowedCategories">Allowed capability categories (null = all).</param>
        /// <param name="excludePatterns">Patterns to exclude (e.g., "security.*").</param>
        /// <returns>Filtered list of tools.</returns>
        public List<LLMTool> FilterTools(
            List<LLMTool> tools,
            List<string>? allowedCategories = null,
            List<string>? excludePatterns = null)
        {
            var filtered = tools.AsEnumerable();

            // Filter by category
            if (allowedCategories != null && allowedCategories.Count > 0)
            {
                filtered = filtered.Where(t =>
                    allowedCategories.Any(cat => t.Name.StartsWith(cat + "."))
                );
            }

            // Exclude patterns
            if (excludePatterns != null && excludePatterns.Count > 0)
            {
                foreach (var pattern in excludePatterns)
                {
                    var prefix = pattern.TrimEnd('*', '.');
                    filtered = filtered.Where(t => !t.Name.StartsWith(prefix));
                }
            }

            return filtered.ToList();
        }

        /// <summary>
        /// Generates a system prompt for the LLM explaining available tools.
        /// Helps LLM understand how to use DataWarehouse capabilities.
        /// </summary>
        /// <param name="tools">Available tools.</param>
        /// <returns>System prompt text.</returns>
        public string GenerateSystemPrompt(List<LLMTool> tools)
        {
            var prompt = @"You are an AI assistant with access to DataWarehouse capabilities.

Available capabilities:
";

            foreach (var tool in tools)
            {
                prompt += $"- {tool.Name}: {tool.Description}\n";
            }

            prompt += @"
When the user asks you to perform data operations:
1. Use the appropriate capabilities via tool calling
2. Execute capabilities in the correct order (compress → encrypt → store)
3. Pass data between capabilities using the output from one as input to the next
4. Always explain what you're doing before invoking tools
5. Report results clearly after tool execution

For data transformations:
- Data must be base64 encoded
- Chain transformations when needed (compress then encrypt)
- Preserve data through the pipeline

For storage operations:
- Use meaningful keys/paths
- Ensure data is properly transformed before storing
- Confirm successful storage

You can invoke multiple tools in sequence to accomplish complex tasks.
";

            return prompt;
        }
    }
}
