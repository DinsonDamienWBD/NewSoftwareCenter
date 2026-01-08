using DataWarehouse.SDK.AI.Math;
using System.Text;
using System.Text.Json;

namespace DataWarehouse.SDK.AI.LLM.Providers
{
    /// <summary>
    /// Production-ready Azure OpenAI LLM provider.
    /// Supports GPT-4, GPT-4 Turbo, GPT-3.5 Turbo via Azure OpenAI Service.
    /// Also powers Microsoft Copilot integrations.
    ///
    /// Features:
    /// - Full Azure OpenAI REST API integration
    /// - Deployment-based model access
    /// - API key or Azure AD authentication
    /// - Tool/function calling support
    /// - Rate limit handling with exponential backoff
    /// - Retry logic for transient failures
    /// - Cost calculation based on Azure pricing
    ///
    /// Authentication:
    /// - API key via constructor or environment variable AZURE_OPENAI_API_KEY
    ///
    /// Configuration:
    /// - Endpoint: Your Azure OpenAI resource endpoint (e.g., https://your-resource.openai.azure.com)
    /// - Deployment: Your model deployment name (configured in Azure portal)
    /// - API Version: Azure OpenAI API version (default: 2024-02-15-preview)
    ///
    /// Models supported (via deployments):
    /// - GPT-4 (8K, 32K context)
    /// - GPT-4 Turbo (128K context)
    /// - GPT-3.5 Turbo (4K, 16K context)
    /// </summary>
    public class AzureOpenAIProvider : ILLMProvider
    {
        private readonly HttpClient _httpClient;
        private readonly string _apiKey;
        private readonly string _endpoint;
        private readonly string _apiVersion;

        // Azure OpenAI pricing (USD per 1K tokens) - Updated as of 2025
        // Note: Pricing varies by region and subscription
        private static readonly Dictionary<string, (decimal input, decimal output)> ModelPricing = new()
        {
            ["gpt-4"] = (0.03m, 0.06m),
            ["gpt-4-32k"] = (0.06m, 0.12m),
            ["gpt-4-turbo"] = (0.01m, 0.03m),
            ["gpt-35-turbo"] = (0.0005m, 0.0015m),
            ["gpt-35-turbo-16k"] = (0.003m, 0.004m)
        };

        public string ProviderName => "Azure OpenAI";
        public string DefaultModel { get; }
        public bool SupportsToolCalling => true;

        /// <summary>
        /// Constructs Azure OpenAI provider with credentials.
        /// </summary>
        /// <param name="endpoint">Azure OpenAI endpoint (or null to use AZURE_OPENAI_ENDPOINT env var).</param>
        /// <param name="apiKey">Azure OpenAI API key (or null to use AZURE_OPENAI_API_KEY env var).</param>
        /// <param name="defaultDeployment">Default deployment name (or null to use AZURE_OPENAI_DEPLOYMENT env var).</param>
        /// <param name="apiVersion">API version (default: 2024-02-15-preview).</param>
        public AzureOpenAIProvider(
            string? endpoint = null,
            string? apiKey = null,
            string? defaultDeployment = null,
            string apiVersion = "2024-02-15-preview")
        {
            _endpoint = endpoint ?? Environment.GetEnvironmentVariable("AZURE_OPENAI_ENDPOINT")
                ?? throw new ArgumentException("Azure OpenAI endpoint must be provided or set in AZURE_OPENAI_ENDPOINT environment variable");

            _apiKey = apiKey ?? Environment.GetEnvironmentVariable("AZURE_OPENAI_API_KEY")
                ?? throw new ArgumentException("Azure OpenAI API key must be provided or set in AZURE_OPENAI_API_KEY environment variable");

            DefaultModel = defaultDeployment ?? Environment.GetEnvironmentVariable("AZURE_OPENAI_DEPLOYMENT")
                ?? throw new ArgumentException("Azure OpenAI deployment must be provided or set in AZURE_OPENAI_DEPLOYMENT environment variable");

            _apiVersion = apiVersion;

            // Ensure endpoint doesn't have trailing slash
            _endpoint = _endpoint.TrimEnd('/');

            _httpClient = new HttpClient
            {
                Timeout = TimeSpan.FromSeconds(120)
            };
            _httpClient.DefaultRequestHeaders.Add("api-key", _apiKey);
        }

        public async Task<LLMResponse> CompleteAsync(string prompt, string? model = null, int? maxTokens = null)
        {
            var messages = new List<LLMMessage> { LLMMessage.User(prompt) };
            return await ChatAsync(messages, model, maxTokens);
        }

        public async Task<LLMResponse> ChatAsync(List<LLMMessage> messages, string? model = null, int? maxTokens = null)
        {
            return await ChatWithToolsAsync(messages, [], model, maxTokens);
        }

        public async Task<LLMResponse> ChatWithToolsAsync(
            List<LLMMessage> messages,
            List<LLMTool> tools,
            string? model = null,
            int? maxTokens = null)
        {
            var deployment = model ?? DefaultModel;
            maxTokens ??= 2048;

            var requestBody = new Dictionary<string, object>
            {
                ["messages"] = messages.Select(m => new Dictionary<string, object>
                {
                    ["role"] = m.Role,
                    ["content"] = m.Content ?? string.Empty
                }).ToList(),
                ["max_tokens"] = maxTokens.Value,
                ["temperature"] = 0.7
            };

            // Add tools if provided
            if (tools.Count > 0)
            {
                requestBody["tools"] = tools.Select(t => new Dictionary<string, object>
                {
                    ["type"] = "function",
                    ["function"] = new Dictionary<string, object>
                    {
                        ["name"] = t.Name,
                        ["description"] = t.Description,
                        ["parameters"] = t.Parameters
                    }
                }).ToList();
                requestBody["tool_choice"] = "auto";
            }

            var jsonBody = JsonSerializer.Serialize(requestBody);
            var content = new StringContent(jsonBody, Encoding.UTF8, "application/json");

            // Build endpoint URL
            var url = $"{_endpoint}/openai/deployments/{deployment}/chat/completions?api-version={_apiVersion}";

            // Retry logic with exponential backoff
            HttpResponseMessage? response = null;
            Exception? lastException = null;

            for (int attempt = 0; attempt < 3; attempt++)
            {
                try
                {
                    response = await _httpClient.PostAsync(url, content);

                    if (response.IsSuccessStatusCode)
                    {
                        break;
                    }

                    // Handle rate limiting
                    if (response.StatusCode == System.Net.HttpStatusCode.TooManyRequests)
                    {
                        var retryAfter = response.Headers.RetryAfter?.Delta ?? TimeSpan.FromSeconds(MathUtils.Pow(2, attempt));
                        await Task.Delay(retryAfter);
                        continue;
                    }

                    // Handle throttling (429)
                    if ((int)response.StatusCode == 429)
                    {
                        await Task.Delay(TimeSpan.FromSeconds(MathUtils.Pow(2, attempt + 1)));
                        continue;
                    }

                    // Handle other errors
                    var errorContent = await response.Content.ReadAsStringAsync();
                    throw new HttpRequestException($"Azure OpenAI API error: {response.StatusCode} - {errorContent}");
                }
                catch (Exception ex)
                {
                    lastException = ex;
                    if (attempt < 2) // Not last attempt
                    {
                        await Task.Delay(TimeSpan.FromSeconds(MathUtils.Pow(2, attempt)));
                    }
                }
            }

            if (response == null || !response.IsSuccessStatusCode)
            {
                throw new Exception($"Azure OpenAI API request failed after 3 attempts", lastException);
            }

            var responseBody = await response.Content.ReadAsStringAsync();
            var responseJson = JsonSerializer.Deserialize<JsonElement>(responseBody);

            // Parse response (same format as OpenAI)
            var choice = responseJson.GetProperty("choices")[0];
            var message = choice.GetProperty("message");
            var finishReason = choice.GetProperty("finish_reason").GetString() ?? "completed";

            var responseContent = message.TryGetProperty("content", out var contentProp) && contentProp.ValueKind != JsonValueKind.Null
                ? contentProp.GetString() ?? string.Empty
                : string.Empty;

            var toolCalls = new List<LLMToolCall>();
            if (message.TryGetProperty("tool_calls", out var toolCallsProp) && toolCallsProp.ValueKind == JsonValueKind.Array)
            {
                foreach (var toolCall in toolCallsProp.EnumerateArray())
                {
                    var function = toolCall.GetProperty("function");
                    var argumentsJson = function.GetProperty("arguments").GetString() ?? "{}";
                    var arguments = JsonSerializer.Deserialize<Dictionary<string, object>>(argumentsJson) ?? [];

                    toolCalls.Add(new LLMToolCall
                    {
                        Id = toolCall.GetProperty("id").GetString() ?? Guid.NewGuid().ToString(),
                        ToolName = function.GetProperty("name").GetString() ?? "",
                        Arguments = arguments
                    });
                }
            }

            // Get token usage
            var usage = responseJson.GetProperty("usage");
            var inputTokens = usage.GetProperty("prompt_tokens").GetInt32();
            var outputTokens = usage.GetProperty("completion_tokens").GetInt32();

            // Calculate cost (use deployment name to infer model type)
            var cost = CalculateCost(deployment, inputTokens, outputTokens);

            return new LLMResponse
            {
                Content = responseContent,
                ToolCalls = toolCalls,
                Model = deployment,
                InputTokens = inputTokens,
                OutputTokens = outputTokens,
                CostUsd = cost,
                FinishReason = finishReason
            };
        }

        public decimal EstimateCost(List<LLMMessage> messages, string? model = null, int estimatedOutputTokens = 500)
        {
            var deployment = model ?? DefaultModel;

            // Estimate input tokens (rough approximation: 1 token ≈ 4 characters)
            var inputText = string.Join("\n", messages.Select(m => m.Content));
            var estimatedInputTokens = inputText.Length / 4;

            return CalculateCost(deployment, estimatedInputTokens, estimatedOutputTokens);
        }

        public int CountTokens(string text, string? model = null)
        {
            // Rough approximation: 1 token ≈ 4 characters
            // For production, use tiktoken library for accurate token counting
            return text.Length / 4;
        }

        public async Task<bool> IsHealthyAsync()
        {
            try
            {
                // Try listing deployments to verify API connectivity
                var url = $"{_endpoint}/openai/deployments?api-version={_apiVersion}";
                var response = await _httpClient.GetAsync(url);
                return response.IsSuccessStatusCode;
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// Calculates cost based on token usage and model pricing.
        /// Infers model type from deployment name.
        /// </summary>
        private static decimal CalculateCost(string deployment, int inputTokens, int outputTokens)
        {
            // Try to infer model from deployment name
            var modelKey = "gpt-35-turbo"; // Default

            if (deployment.Contains("gpt-4-32k", StringComparison.OrdinalIgnoreCase))
                modelKey = "gpt-4-32k";
            else if (deployment.Contains("gpt-4-turbo", StringComparison.OrdinalIgnoreCase) ||
                     deployment.Contains("gpt-4-1106", StringComparison.OrdinalIgnoreCase))
                modelKey = "gpt-4-turbo";
            else if (deployment.Contains("gpt-4", StringComparison.OrdinalIgnoreCase))
                modelKey = "gpt-4";
            else if (deployment.Contains("gpt-35-turbo-16k", StringComparison.OrdinalIgnoreCase))
                modelKey = "gpt-35-turbo-16k";

            var (inputPrice, outputPrice) = ModelPricing.TryGetValue(modelKey, out var pricing)
                ? pricing
                : ModelPricing["gpt-35-turbo"]; // Default pricing

            // Calculate cost (pricing is per 1K tokens)
            var inputCost = (inputTokens / 1000.0m) * inputPrice;
            var outputCost = (outputTokens / 1000.0m) * outputPrice;

            return inputCost + outputCost;
        }

        /// <summary>
        /// Disposes resources.
        /// </summary>
        public void Dispose()
        {
            _httpClient?.Dispose();
        }
    }
}
