using DataWarehouse.SDK.AI.Math;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace DataWarehouse.SDK.AI.LLM.Providers
{
    /// <summary>
    /// Production-ready Anthropic Claude LLM provider.
    /// Supports Claude 3 Opus, Sonnet, Haiku, and tool calling.
    ///
    /// Features:
    /// - Full Messages API integration
    /// - Advanced tool calling (Claude 3+)
    /// - System prompts support
    /// - Streaming support (optional)
    /// - Rate limit handling with exponential backoff
    /// - Retry logic for transient failures
    /// - Cost calculation based on current pricing
    ///
    /// Authentication:
    /// - API key via constructor or environment variable ANTHROPIC_API_KEY
    ///
    /// Models supported:
    /// - claude-3-opus-20240229 (200K context, most capable)
    /// - claude-3-sonnet-20240229 (200K context, balanced)
    /// - claude-3-haiku-20240307 (200K context, fastest)
    /// - claude-2.1 (200K context, legacy)
    /// - claude-2.0 (100K context, legacy)
    /// </summary>
    public class AnthropicProvider : ILLMProvider
    {
        private readonly HttpClient _httpClient;
        private readonly string _apiKey;
        private readonly string _baseUrl;
        private const string DefaultBaseUrl = "https://api.anthropic.com/v1";
        private const string AnthropicVersion = "2023-06-01";

        // Model pricing (USD per 1M tokens) - Updated as of 2025
        private static readonly Dictionary<string, (decimal input, decimal output)> ModelPricing = new()
        {
            ["claude-3-opus-20240229"] = (15.0m, 75.0m),
            ["claude-3-sonnet-20240229"] = (3.0m, 15.0m),
            ["claude-3-haiku-20240307"] = (0.25m, 1.25m),
            ["claude-2.1"] = (8.0m, 24.0m),
            ["claude-2.0"] = (8.0m, 24.0m),
            ["claude-instant-1.2"] = (0.8m, 2.4m)
        };

        public string ProviderName => "Anthropic";
        public string DefaultModel { get; }
        public bool SupportsToolCalling => true;

        /// <summary>
        /// Constructs Anthropic provider with API key.
        /// </summary>
        /// <param name="apiKey">Anthropic API key (or null to use ANTHROPIC_API_KEY env var).</param>
        /// <param name="defaultModel">Default model to use (default: claude-3-sonnet-20240229).</param>
        /// <param name="baseUrl">Base URL for API (default: https://api.anthropic.com/v1).</param>
        public AnthropicProvider(string? apiKey = null, string defaultModel = "claude-3-sonnet-20240229", string? baseUrl = null)
        {
            _apiKey = apiKey ?? Environment.GetEnvironmentVariable("ANTHROPIC_API_KEY")
                ?? throw new ArgumentException("Anthropic API key must be provided or set in ANTHROPIC_API_KEY environment variable");

            DefaultModel = defaultModel;
            _baseUrl = baseUrl ?? DefaultBaseUrl;

            _httpClient = new HttpClient
            {
                BaseAddress = new Uri(_baseUrl),
                Timeout = TimeSpan.FromSeconds(120)
            };
            _httpClient.DefaultRequestHeaders.Add("x-api-key", _apiKey);
            _httpClient.DefaultRequestHeaders.Add("anthropic-version", AnthropicVersion);
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
            model ??= DefaultModel;
            maxTokens ??= 4096;

            // Separate system messages from conversation
            var systemMessage = messages.FirstOrDefault(m => m.Role == "system")?.Content ?? string.Empty;
            var conversationMessages = messages.Where(m => m.Role != "system").ToList();

            var requestBody = new Dictionary<string, object>
            {
                ["model"] = model,
                ["messages"] = conversationMessages.Select(m => new Dictionary<string, object>
                {
                    ["role"] = m.Role == "assistant" ? "assistant" : "user",
                    ["content"] = m.Content ?? string.Empty
                }).ToList(),
                ["max_tokens"] = maxTokens.Value
            };

            // Add system message if present
            if (!string.IsNullOrWhiteSpace(systemMessage))
            {
                requestBody["system"] = systemMessage;
            }

            // Add tools if provided (Claude 3+ only)
            if (tools.Count > 0 && model.StartsWith("claude-3"))
            {
                requestBody["tools"] = tools.Select(t => new Dictionary<string, object>
                {
                    ["name"] = t.Name,
                    ["description"] = t.Description,
                    ["input_schema"] = t.Parameters
                }).ToList();
            }

            var jsonBody = JsonSerializer.Serialize(requestBody);
            var content = new StringContent(jsonBody, Encoding.UTF8, "application/json");

            // Retry logic with exponential backoff
            HttpResponseMessage? response = null;
            Exception? lastException = null;

            for (int attempt = 0; attempt < 3; attempt++)
            {
                try
                {
                    response = await _httpClient.PostAsync("/messages", content);

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

                    // Handle overloaded errors (529)
                    if ((int)response.StatusCode == 529)
                    {
                        await Task.Delay(TimeSpan.FromSeconds(MathUtils.Pow(2, attempt)));
                        continue;
                    }

                    // Handle other errors
                    var errorContent = await response.Content.ReadAsStringAsync();
                    throw new HttpRequestException($"Anthropic API error: {response.StatusCode} - {errorContent}");
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
                throw new Exception($"Anthropic API request failed after 3 attempts", lastException);
            }

            var responseBody = await response.Content.ReadAsStringAsync();
            var responseJson = JsonSerializer.Deserialize<JsonElement>(responseBody);

            // Parse response
            var contentArray = responseJson.GetProperty("content");
            var responseContent = string.Empty;
            var toolCalls = new List<LLMToolCall>();

            // Claude returns content as an array of content blocks
            foreach (var block in contentArray.EnumerateArray())
            {
                var blockType = block.GetProperty("type").GetString();

                if (blockType == "text")
                {
                    responseContent += block.GetProperty("text").GetString() ?? "";
                }
                else if (blockType == "tool_use")
                {
                    var toolId = block.GetProperty("id").GetString() ?? Guid.NewGuid().ToString();
                    var toolName = block.GetProperty("name").GetString() ?? "";
                    var inputJson = block.GetProperty("input").GetRawText();
                    var arguments = JsonSerializer.Deserialize<Dictionary<string, object>>(inputJson) ?? [];

                    toolCalls.Add(new LLMToolCall
                    {
                        Id = toolId,
                        ToolName = toolName,
                        Arguments = arguments
                    });
                }
            }

            var stopReason = responseJson.GetProperty("stop_reason").GetString() ?? "end_turn";
            var finishReason = stopReason switch
            {
                "end_turn" => "completed",
                "max_tokens" => "length",
                "tool_use" => "tool_calls",
                _ => stopReason
            };

            // Get token usage
            var usage = responseJson.GetProperty("usage");
            var inputTokens = usage.GetProperty("input_tokens").GetInt32();
            var outputTokens = usage.GetProperty("output_tokens").GetInt32();

            // Calculate cost
            var cost = CalculateCost(model, inputTokens, outputTokens);

            return new LLMResponse
            {
                Content = responseContent,
                ToolCalls = toolCalls,
                Model = model,
                InputTokens = inputTokens,
                OutputTokens = outputTokens,
                CostUsd = cost,
                FinishReason = finishReason
            };
        }

        public decimal EstimateCost(List<LLMMessage> messages, string? model = null, int estimatedOutputTokens = 500)
        {
            model ??= DefaultModel;

            // Estimate input tokens (rough approximation: 1 token ≈ 3.5 characters for Claude)
            var inputText = string.Join("\n", messages.Select(m => m.Content));
            var estimatedInputTokens = (int)(inputText.Length / 3.5);

            return CalculateCost(model, estimatedInputTokens, estimatedOutputTokens);
        }

        public int CountTokens(string text, string? model = null)
        {
            // Rough approximation: 1 token ≈ 3.5 characters for Claude
            // For production, use Anthropic's token counting endpoint
            return (int)(text.Length / 3.5);
        }

        public async Task<bool> IsHealthyAsync()
        {
            try
            {
                // Try a minimal completion to verify API connectivity
                var testMessage = new List<LLMMessage> { LLMMessage.User("Hi") };
                var response = await ChatAsync(testMessage, model: DefaultModel, maxTokens: 10);
                return !string.IsNullOrEmpty(response.Content);
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// Calculates cost based on token usage and model pricing.
        /// </summary>
        private static decimal CalculateCost(string model, int inputTokens, int outputTokens)
        {
            // Find pricing for model (try exact match first, then prefix match)
            (decimal inputPrice, decimal outputPrice) = (0, 0);

            if (ModelPricing.TryGetValue(model, out var pricing))
            {
                (inputPrice, outputPrice) = pricing;
            }
            else
            {
                // Try prefix match (e.g., "claude-3-opus-latest" matches "claude-3-opus-20240229")
                var matchingKey = ModelPricing.Keys.FirstOrDefault(k => model.StartsWith(k.Split('-')[0..3].Aggregate((a, b) => a + "-" + b)));
                if (matchingKey != null)
                {
                    (inputPrice, outputPrice) = ModelPricing[matchingKey];
                }
                else
                {
                    // Default to Claude 3 Sonnet pricing if unknown
                    (inputPrice, outputPrice) = ModelPricing["claude-3-sonnet-20240229"];
                }
            }

            // Calculate cost (pricing is per 1M tokens)
            var inputCost = (inputTokens / 1_000_000.0m) * inputPrice;
            var outputCost = (outputTokens / 1_000_000.0m) * outputPrice;

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
