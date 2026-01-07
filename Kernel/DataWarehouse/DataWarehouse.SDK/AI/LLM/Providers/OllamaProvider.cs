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
    /// Production-ready Ollama LLM provider.
    /// Supports local model execution via Ollama runtime.
    ///
    /// Features:
    /// - Full Ollama API integration
    /// - Function/tool calling support (for compatible models)
    /// - Local model execution (zero cost, complete privacy)
    /// - Multi-turn conversations
    /// - Streaming support (optional)
    /// - Retry logic for transient failures
    /// - Support for all Ollama-compatible models
    ///
    /// Configuration:
    /// - Base URL via constructor or environment variable OLLAMA_HOST (default: http://localhost:11434)
    ///
    /// Models supported (examples):
    /// - llama3:8b (Meta's Llama 3 8B)
    /// - llama3:70b (Meta's Llama 3 70B)
    /// - mistral:7b (Mistral 7B)
    /// - codellama:13b (Code Llama 13B)
    /// - mixtral:8x7b (Mixtral 8x7B)
    /// - phi3:mini (Microsoft Phi-3 Mini)
    /// - gemma:7b (Google Gemma 7B)
    /// - qwen2:7b (Alibaba Qwen2 7B)
    /// - And many more...
    ///
    /// Cost: Zero (local execution)
    /// </summary>
    public class OllamaProvider : ILLMProvider
    {
        private readonly HttpClient _httpClient;
        private readonly string _baseUrl;

        public string ProviderName => "Ollama";
        public string DefaultModel { get; }
        public bool SupportsToolCalling => true;

        /// <summary>
        /// Constructs Ollama provider.
        /// </summary>
        /// <param name="baseUrl">Ollama server URL (or null to use OLLAMA_HOST env var, default: http://localhost:11434).</param>
        /// <param name="defaultModel">Default model to use (default: llama3:8b).</param>
        public OllamaProvider(string? baseUrl = null, string defaultModel = "llama3:8b")
        {
            _baseUrl = baseUrl ?? Environment.GetEnvironmentVariable("OLLAMA_HOST") ?? "http://localhost:11434";
            DefaultModel = defaultModel;

            // Ensure base URL doesn't have trailing slash
            _baseUrl = _baseUrl.TrimEnd('/');

            _httpClient = new HttpClient
            {
                BaseAddress = new Uri(_baseUrl),
                Timeout = TimeSpan.FromMinutes(10) // Local models can be slow on first run
            };
        }

        public async Task<LLMResponse> CompleteAsync(string prompt, string? model = null, int? maxTokens = null)
        {
            var messages = new List<LLMMessage> { LLMMessage.User(prompt) };
            return await ChatAsync(messages, model, maxTokens);
        }

        public async Task<LLMResponse> ChatAsync(List<LLMMessage> messages, string? model = null, int? maxTokens = null)
        {
            return await ChatWithToolsAsync(messages, new List<LLMTool>(), model, maxTokens);
        }

        public async Task<LLMResponse> ChatWithToolsAsync(
            List<LLMMessage> messages,
            List<LLMTool> tools,
            string? model = null,
            int? maxTokens = null)
        {
            model ??= DefaultModel;

            var requestBody = new Dictionary<string, object>
            {
                ["model"] = model,
                ["messages"] = messages.Select(m => new Dictionary<string, object>
                {
                    ["role"] = m.Role,
                    ["content"] = m.Content ?? string.Empty
                }).ToList(),
                ["stream"] = false
            };

            // Add options for token limit
            if (maxTokens.HasValue)
            {
                requestBody["options"] = new Dictionary<string, object>
                {
                    ["num_predict"] = maxTokens.Value
                };
            }

            // Add tools if provided (for compatible models like llama3, mistral, etc.)
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
                    response = await _httpClient.PostAsync("/api/chat", content);

                    if (response.IsSuccessStatusCode)
                    {
                        break;
                    }

                    // Handle server errors (model might be loading)
                    if ((int)response.StatusCode >= 500)
                    {
                        await Task.Delay(TimeSpan.FromSeconds(Math.Pow(2, attempt)));
                        continue;
                    }

                    // Handle other errors
                    var errorContent = await response.Content.ReadAsStringAsync();
                    throw new HttpRequestException($"Ollama API error: {response.StatusCode} - {errorContent}");
                }
                catch (HttpRequestException ex)
                {
                    lastException = ex;
                    if (attempt < 2) // Not last attempt
                    {
                        await Task.Delay(TimeSpan.FromSeconds(Math.Pow(2, attempt)));
                    }
                }
                catch (TaskCanceledException ex) // Timeout
                {
                    lastException = ex;
                    if (attempt < 2)
                    {
                        // Increase timeout for next attempt
                        _httpClient.Timeout = TimeSpan.FromMinutes(15);
                        await Task.Delay(TimeSpan.FromSeconds(2));
                    }
                }
            }

            if (response == null || !response.IsSuccessStatusCode)
            {
                throw new Exception($"Ollama API request failed after 3 attempts. Is Ollama running? (ollama serve)", lastException);
            }

            var responseBody = await response.Content.ReadAsStringAsync();
            var responseJson = JsonSerializer.Deserialize<JsonElement>(responseBody);

            // Parse response
            var message = responseJson.GetProperty("message");
            var responseContent = message.GetProperty("content").GetString() ?? string.Empty;

            var toolCalls = new List<LLMToolCall>();
            if (message.TryGetProperty("tool_calls", out var toolCallsProp) && toolCallsProp.ValueKind == JsonValueKind.Array)
            {
                foreach (var toolCall in toolCallsProp.EnumerateArray())
                {
                    var function = toolCall.GetProperty("function");
                    var argumentsJson = function.GetProperty("arguments").GetString() ?? "{}";
                    var arguments = JsonSerializer.Deserialize<Dictionary<string, object>>(argumentsJson) ?? new();

                    toolCalls.Add(new LLMToolCall
                    {
                        Id = Guid.NewGuid().ToString(),
                        ToolName = function.GetProperty("name").GetString() ?? "",
                        Arguments = arguments
                    });
                }
            }

            // Get token counts
            var inputTokens = 0;
            var outputTokens = 0;

            if (responseJson.TryGetProperty("prompt_eval_count", out var promptCount))
            {
                inputTokens = promptCount.GetInt32();
            }
            if (responseJson.TryGetProperty("eval_count", out var evalCount))
            {
                outputTokens = evalCount.GetInt32();
            }

            // Ollama doesn't return finish reason in same format, infer from done_reason
            var finishReason = "completed";
            if (responseJson.TryGetProperty("done_reason", out var doneReasonProp))
            {
                var doneReason = doneReasonProp.GetString() ?? "stop";
                finishReason = doneReason switch
                {
                    "stop" => "completed",
                    "length" => "length",
                    "load" => "error",
                    _ => doneReason
                };
            }

            return new LLMResponse
            {
                Content = responseContent,
                ToolCalls = toolCalls,
                Model = model,
                InputTokens = inputTokens,
                OutputTokens = outputTokens,
                CostUsd = 0.0m, // Local execution is free
                FinishReason = finishReason
            };
        }

        public decimal EstimateCost(List<LLMMessage> messages, string? model = null, int estimatedOutputTokens = 500)
        {
            // Local execution is always free
            return 0.0m;
        }

        public int CountTokens(string text, string? model = null)
        {
            // Rough approximation: 1 token ≈ 4 characters
            // Different models have different tokenizers, but this is a reasonable estimate
            return text.Length / 4;
        }

        public async Task<bool> IsHealthyAsync()
        {
            try
            {
                // Check if Ollama server is running by listing models
                var response = await _httpClient.GetAsync("/api/tags");
                return response.IsSuccessStatusCode;
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// Lists all available models on the Ollama server.
        /// </summary>
        /// <returns>List of model names.</returns>
        public async Task<List<string>> ListModelsAsync()
        {
            try
            {
                var response = await _httpClient.GetAsync("/api/tags");
                if (!response.IsSuccessStatusCode)
                {
                    return new List<string>();
                }

                var responseBody = await response.Content.ReadAsStringAsync();
                var responseJson = JsonSerializer.Deserialize<JsonElement>(responseBody);

                var models = new List<string>();
                if (responseJson.TryGetProperty("models", out var modelsArray))
                {
                    foreach (var model in modelsArray.EnumerateArray())
                    {
                        if (model.TryGetProperty("name", out var nameProp))
                        {
                            models.Add(nameProp.GetString() ?? "");
                        }
                    }
                }

                return models;
            }
            catch
            {
                return new List<string>();
            }
        }

        /// <summary>
        /// Pulls (downloads) a model from Ollama library.
        /// </summary>
        /// <param name="modelName">Model name to pull (e.g., "llama3:8b").</param>
        /// <returns>True if successful, false otherwise.</returns>
        public async Task<bool> PullModelAsync(string modelName)
        {
            try
            {
                var requestBody = new Dictionary<string, object>
                {
                    ["name"] = modelName,
                    ["stream"] = false
                };

                var jsonBody = JsonSerializer.Serialize(requestBody);
                var content = new StringContent(jsonBody, Encoding.UTF8, "application/json");

                var response = await _httpClient.PostAsync("/api/pull", content);
                return response.IsSuccessStatusCode;
            }
            catch
            {
                return false;
            }
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
