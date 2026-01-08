namespace DataWarehouse.SDK.AI.LLM
{
    /// <summary>
    /// Interface for Large Language Model providers.
    /// Enables model-agnostic AI integration supporting OpenAI, Anthropic, local models, etc.
    ///
    /// Used by AI Runtime to:
    /// - Generate natural language responses
    /// - Understand user intent
    /// - Plan capability execution
    /// - Generate code/queries
    /// - Tool calling (invoke capabilities)
    ///
    /// Supported providers:
    /// - OpenAI (GPT-4, GPT-3.5, etc.)
    /// - Anthropic (Claude 3.5 Sonnet, Opus, etc.)
    /// - Azure OpenAI
    /// - Local models (Ollama, llama.cpp, etc.)
    /// </summary>
    public interface ILLMProvider
    {
        /// <summary>
        /// Name of the LLM provider.
        /// Examples: "OpenAI", "Anthropic", "Ollama"
        /// </summary>
        string ProviderName { get; }

        /// <summary>
        /// Default model identifier.
        /// Examples: "gpt-4", "claude-3-5-sonnet-20241022", "llama3:8b"
        /// </summary>
        string DefaultModel { get; }

        /// <summary>
        /// Whether this provider supports tool calling (function calling).
        /// Tool calling enables LLM to invoke DataWarehouse capabilities.
        /// </summary>
        bool SupportsToolCalling { get; }

        /// <summary>
        /// Generates a completion from a prompt.
        /// Basic text generation without conversation context.
        ///
        /// Use cases:
        /// - Generate capability description
        /// - Summarize execution results
        /// - Extract structured data from text
        /// </summary>
        /// <param name="prompt">Text prompt for the LLM.</param>
        /// <param name="model">Model to use (optional, uses DefaultModel if null).</param>
        /// <param name="maxTokens">Maximum tokens to generate (optional).</param>
        /// <returns>LLM completion response.</returns>
        Task<LLMResponse> CompleteAsync(
            string prompt,
            string? model = null,
            int? maxTokens = null);

        /// <summary>
        /// Generates a response in a conversation context.
        /// Maintains conversation history for multi-turn interactions.
        ///
        /// Use cases:
        /// - Chatbot interactions
        /// - Multi-step planning
        /// - Clarification questions
        /// - Interactive debugging
        /// </summary>
        /// <param name="messages">Conversation history (user/assistant messages).</param>
        /// <param name="model">Model to use (optional, uses DefaultModel if null).</param>
        /// <param name="maxTokens">Maximum tokens to generate (optional).</param>
        /// <returns>LLM response with assistant message.</returns>
        Task<LLMResponse> ChatAsync(
            List<LLMMessage> messages,
            string? model = null,
            int? maxTokens = null);

        /// <summary>
        /// Generates a response with tool calling enabled.
        /// LLM can invoke DataWarehouse capabilities to accomplish tasks.
        ///
        /// Use cases:
        /// - User: "Compress and encrypt my file"
        /// - LLM calls: transform.gzip.apply, then transform.aes.apply
        ///
        /// Flow:
        /// 1. Send messages + available tools
        /// 2. LLM responds with tool calls OR text
        /// 3. If tool calls, execute them
        /// 4. Send results back to LLM
        /// 5. LLM generates final response
        /// </summary>
        /// <param name="messages">Conversation history.</param>
        /// <param name="tools">Available tools (capabilities) LLM can invoke.</param>
        /// <param name="model">Model to use (optional, uses DefaultModel if null).</param>
        /// <param name="maxTokens">Maximum tokens to generate (optional).</param>
        /// <returns>LLM response (may contain tool calls).</returns>
        Task<LLMResponse> ChatWithToolsAsync(
            List<LLMMessage> messages,
            List<LLMTool> tools,
            string? model = null,
            int? maxTokens = null);

        /// <summary>
        /// Estimates the cost of a request before sending it.
        /// Helps avoid unexpected bills and enforce budgets.
        ///
        /// Cost factors:
        /// - Model pricing (input tokens, output tokens)
        /// - Context length
        /// - Tool calling overhead
        /// </summary>
        /// <param name="messages">Messages to send.</param>
        /// <param name="model">Model to use.</param>
        /// <param name="estimatedOutputTokens">Estimated output tokens (default 500).</param>
        /// <returns>Estimated cost in USD.</returns>
        decimal EstimateCost(
            List<LLMMessage> messages,
            string? model = null,
            int estimatedOutputTokens = 500);

        /// <summary>
        /// Counts tokens in text for the specified model.
        /// Different models have different tokenization.
        ///
        /// Use cases:
        /// - Check if prompt fits context window
        /// - Estimate costs
        /// - Truncate text to fit limits
        /// </summary>
        /// <param name="text">Text to count tokens for.</param>
        /// <param name="model">Model to use for tokenization.</param>
        /// <returns>Token count.</returns>
        int CountTokens(string text, string? model = null);

        /// <summary>
        /// Checks if the provider is healthy and available.
        /// Verifies API keys, network connectivity, rate limits.
        /// </summary>
        /// <returns>True if healthy, false otherwise.</returns>
        Task<bool> IsHealthyAsync();
    }

    /// <summary>
    /// Represents a message in a conversation.
    /// </summary>
    public class LLMMessage
    {
        /// <summary>
        /// Role of the message sender.
        /// "system": System instructions
        /// "user": User message
        /// "assistant": AI assistant response
        /// "tool": Tool execution result
        /// </summary>
        public string Role { get; init; } = string.Empty;

        /// <summary>Text content of the message.</summary>
        public string Content { get; init; } = string.Empty;

        /// <summary>
        /// Tool calls requested by assistant (only for assistant messages).
        /// </summary>
        public List<LLMToolCall>? ToolCalls { get; init; }

        /// <summary>
        /// Tool call ID (only for tool result messages).
        /// </summary>
        public string? ToolCallId { get; init; }

        /// <summary>Constructs an empty message.</summary>
        public LLMMessage() { }

        /// <summary>Constructs a message with role and content.</summary>
        public LLMMessage(string role, string content)
        {
            Role = role;
            Content = content;
        }

        /// <summary>Creates a system message.</summary>
        public static LLMMessage System(string content) => new("system", content);

        /// <summary>Creates a user message.</summary>
        public static LLMMessage User(string content) => new("user", content);

        /// <summary>Creates an assistant message.</summary>
        public static LLMMessage Assistant(string content) => new("assistant", content);
    }

    /// <summary>
    /// Response from an LLM.
    /// </summary>
    public class LLMResponse
    {
        /// <summary>Generated text content.</summary>
        public string Content { get; init; } = string.Empty;

        /// <summary>Tool calls requested by the LLM (if any).</summary>
        public List<LLMToolCall> ToolCalls { get; init; } = [];

        /// <summary>Model used for generation.</summary>
        public string Model { get; init; } = string.Empty;

        /// <summary>Input tokens consumed.</summary>
        public int InputTokens { get; init; }

        /// <summary>Output tokens generated.</summary>
        public int OutputTokens { get; init; }

        /// <summary>Actual cost in USD.</summary>
        public decimal CostUsd { get; init; }

        /// <summary>Finish reason (completed, length, tool_calls, etc.).</summary>
        public string FinishReason { get; init; } = string.Empty;

        /// <summary>Whether the response contains tool calls.</summary>
        public bool HasToolCalls => ToolCalls.Count > 0;
    }

    /// <summary>
    /// Represents a tool (capability) the LLM can invoke.
    /// </summary>
    public class LLMTool
    {
        /// <summary>Unique tool identifier (usually capability ID).</summary>
        public string Name { get; init; } = string.Empty;

        /// <summary>Human-readable description of what the tool does.</summary>
        public string Description { get; init; } = string.Empty;

        /// <summary>
        /// JSON Schema describing tool parameters.
        /// Example: { "type": "object", "properties": { "data": { "type": "string" } } }
        /// </summary>
        public Dictionary<string, object> Parameters { get; init; } = [];
    }

    /// <summary>
    /// Represents a tool call requested by the LLM.
    /// </summary>
    public class LLMToolCall
    {
        /// <summary>Unique identifier for this tool call.</summary>
        public string Id { get; init; } = string.Empty;

        /// <summary>Name of the tool to invoke.</summary>
        public string ToolName { get; init; } = string.Empty;

        /// <summary>Arguments to pass to the tool (JSON object).</summary>
        public Dictionary<string, object> Arguments { get; init; } = [];
    }
}
