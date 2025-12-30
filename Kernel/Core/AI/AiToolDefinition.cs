using System;
using System.Text.Json;
using System.Threading.Tasks;

namespace Core.AI
{
    /// <summary>
    /// Describes a capability that the AI Agent can invoke.
    /// </summary>
    public class AiToolDefinition
    {
        /// <summary>
        /// Name of the tool
        /// </summary>
        public string Name { get; set; } = string.Empty;

        /// <summary>
        /// Description of the tool
        /// Crucial for LLM to understand WHEN to use it
        /// </summary>
        public string Description { get; set; } = string.Empty;

        /// <summary>
        /// JSON Schema describing the arguments
        /// </summary>
        public string ParametersSchemaJson { get; set; } = "{}";

        /// <summary>
        /// The actual function to execute
        /// </summary>
        public Func<string, Task<string>> ExecuteAsync { get; set; } = _ => Task.FromResult("");
    }
}