using System;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using SoftwareCenter.Core.Diagnostics;

namespace SoftwareCenter.Module.AI.Agent.Services
{
    /// <summary>
    /// Lightweight agent that calls an OpenAI-compatible chat completions endpoint.
    /// Configuration keys expected:
    /// - OpenAI:ApiKey
    /// - OpenAI:Endpoint (optional) - defaults to https://api.openai.com/v1/chat/completions
    /// - OpenAI:Model (optional) - defaults to gpt-4o-mini
    /// </summary>
    public class SemanticKernelAgent : IDisposable
    {
        private readonly HttpClient _http;
        private readonly string _apiKey;
        private readonly string _endpoint;
        private readonly string _model;

        public SemanticKernelAgent(IConfiguration config, HttpClient http = null)
        {
            _apiKey = config["OpenAI:ApiKey"] ?? string.Empty;
            _endpoint = config["OpenAI:Endpoint"] ?? "https://api.openai.com/v1/chat/completions";
            _model = config["OpenAI:Model"] ?? "gpt-4o-mini";

            _http = http ?? new HttpClient();
            if (!string.IsNullOrEmpty(_apiKey) && !_http.DefaultRequestHeaders.Contains("Authorization"))
            {
                _http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", _apiKey);
            }
        }

        public async Task<string> ExecuteAsync(string prompt)
        {
            if (string.IsNullOrEmpty(_apiKey))
            {
                return "Error: OpenAI API key not configured.";
            }

            var payload = new
            {
                model = _model,
                messages = new[] { new { role = "user", content = prompt } },
                max_tokens = 800
            };

            var content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json");

            try
            {
                var resp = await _http.PostAsync(_endpoint, content);
                resp.EnsureSuccessStatusCode();

                var body = await resp.Content.ReadAsStringAsync();
                using var doc = JsonDocument.Parse(body);
                if (doc.RootElement.TryGetProperty("choices", out var choices) && choices.GetArrayLength() > 0)
                {
                    var first = choices[0];
                    if (first.TryGetProperty("message", out var message) && message.TryGetProperty("content", out var contentEl))
                    {
                        return contentEl.GetString() ?? string.Empty;
                    }
                    if (first.TryGetProperty("text", out var textEl))
                    {
                        return textEl.GetString() ?? string.Empty;
                    }
                }

                return body;
            }
            catch (Exception ex)
            {
                return $"Error calling model: {ex.Message}";
            }
        }

        public void Dispose()
        {
            _http?.Dispose();
        }
    }
}
