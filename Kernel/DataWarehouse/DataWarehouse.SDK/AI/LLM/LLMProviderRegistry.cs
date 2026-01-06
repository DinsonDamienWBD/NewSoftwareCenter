using System;
using System.Collections.Generic;
using System.Linq;

namespace DataWarehouse.SDK.AI.LLM
{
    /// <summary>
    /// Registry for managing multiple LLM providers.
    /// Allows switching between OpenAI, Anthropic, local models, etc.
    ///
    /// Used by AI Runtime to:
    /// - Select appropriate LLM for task
    /// - Fall back to alternative if primary unavailable
    /// - Load balance across providers
    /// - Track usage and costs per provider
    ///
    /// Features:
    /// - Provider registration and discovery
    /// - Default provider selection
    /// - Cost tracking
    /// - Health monitoring
    /// </summary>
    public class LLMProviderRegistry
    {
        private readonly Dictionary<string, ILLMProvider> _providers = new();
        private readonly Dictionary<string, ProviderUsageStats> _usageStats = new();
        private string? _defaultProviderName;
        private readonly object _lock = new();

        /// <summary>
        /// Registers an LLM provider.
        /// </summary>
        /// <param name="provider">Provider to register.</param>
        /// <param name="setAsDefault">Whether to set as default provider.</param>
        public void RegisterProvider(ILLMProvider provider, bool setAsDefault = false)
        {
            if (provider == null)
                throw new ArgumentNullException(nameof(provider));

            lock (_lock)
            {
                _providers[provider.ProviderName] = provider;
                _usageStats[provider.ProviderName] = new ProviderUsageStats
                {
                    ProviderName = provider.ProviderName
                };

                if (setAsDefault || _defaultProviderName == null)
                {
                    _defaultProviderName = provider.ProviderName;
                }
            }
        }

        /// <summary>
        /// Gets a provider by name.
        /// </summary>
        /// <param name="providerName">Name of provider to get.</param>
        /// <returns>The provider, or null if not found.</returns>
        public ILLMProvider? GetProvider(string providerName)
        {
            if (string.IsNullOrWhiteSpace(providerName))
                return null;

            lock (_lock)
            {
                return _providers.TryGetValue(providerName, out var provider) ? provider : null;
            }
        }

        /// <summary>
        /// Gets the default provider.
        /// </summary>
        /// <returns>Default provider, or null if none registered.</returns>
        public ILLMProvider? GetDefaultProvider()
        {
            lock (_lock)
            {
                if (_defaultProviderName == null)
                    return null;

                return _providers.TryGetValue(_defaultProviderName, out var provider) ? provider : null;
            }
        }

        /// <summary>
        /// Sets the default provider.
        /// </summary>
        /// <param name="providerName">Name of provider to set as default.</param>
        public void SetDefaultProvider(string providerName)
        {
            if (string.IsNullOrWhiteSpace(providerName))
                throw new ArgumentException("Provider name cannot be empty");

            lock (_lock)
            {
                if (!_providers.ContainsKey(providerName))
                    throw new ArgumentException($"Provider '{providerName}' not registered");

                _defaultProviderName = providerName;
            }
        }

        /// <summary>
        /// Gets all registered providers.
        /// </summary>
        /// <returns>List of all providers.</returns>
        public List<ILLMProvider> GetAllProviders()
        {
            lock (_lock)
            {
                return _providers.Values.ToList();
            }
        }

        /// <summary>
        /// Gets providers that support tool calling.
        /// </summary>
        /// <returns>List of providers with tool calling support.</returns>
        public List<ILLMProvider> GetProvidersWithToolCalling()
        {
            lock (_lock)
            {
                return _providers.Values.Where(p => p.SupportsToolCalling).ToList();
            }
        }

        /// <summary>
        /// Records usage for cost tracking.
        /// Called after each LLM request.
        /// </summary>
        /// <param name="providerName">Provider used.</param>
        /// <param name="inputTokens">Input tokens consumed.</param>
        /// <param name="outputTokens">Output tokens generated.</param>
        /// <param name="costUsd">Cost in USD.</param>
        public void RecordUsage(string providerName, int inputTokens, int outputTokens, decimal costUsd)
        {
            lock (_lock)
            {
                if (_usageStats.TryGetValue(providerName, out var stats))
                {
                    stats.TotalRequests++;
                    stats.TotalInputTokens += inputTokens;
                    stats.TotalOutputTokens += outputTokens;
                    stats.TotalCostUsd += costUsd;
                    stats.LastUsed = DateTime.UtcNow;
                }
            }
        }

        /// <summary>
        /// Gets usage statistics for a provider.
        /// </summary>
        /// <param name="providerName">Provider to get stats for.</param>
        /// <returns>Usage statistics, or null if provider not found.</returns>
        public ProviderUsageStats? GetUsageStats(string providerName)
        {
            lock (_lock)
            {
                return _usageStats.TryGetValue(providerName, out var stats) ? stats : null;
            }
        }

        /// <summary>
        /// Gets usage statistics for all providers.
        /// </summary>
        /// <returns>List of usage statistics.</returns>
        public List<ProviderUsageStats> GetAllUsageStats()
        {
            lock (_lock)
            {
                return _usageStats.Values.ToList();
            }
        }

        /// <summary>
        /// Selects the cheapest provider for a given task.
        /// Estimates cost for each provider and chooses minimum.
        /// </summary>
        /// <param name="messages">Messages to send.</param>
        /// <param name="estimatedOutputTokens">Estimated output tokens.</param>
        /// <returns>Cheapest provider, or default if no providers available.</returns>
        public ILLMProvider? SelectCheapestProvider(List<LLMMessage> messages, int estimatedOutputTokens = 500)
        {
            lock (_lock)
            {
                if (_providers.Count == 0)
                    return null;

                var providerCosts = _providers.Values
                    .Select(p => new
                    {
                        Provider = p,
                        Cost = p.EstimateCost(messages, null, estimatedOutputTokens)
                    })
                    .OrderBy(x => x.Cost)
                    .ToList();

                return providerCosts.FirstOrDefault()?.Provider;
            }
        }

        /// <summary>
        /// Checks health of all providers.
        /// Returns list of healthy providers.
        /// </summary>
        /// <returns>List of provider names that are healthy.</returns>
        public async System.Threading.Tasks.Task<List<string>> CheckHealthAsync()
        {
            var healthyProviders = new List<string>();

            foreach (var provider in _providers.Values)
            {
                try
                {
                    if (await provider.IsHealthyAsync())
                    {
                        healthyProviders.Add(provider.ProviderName);
                    }
                }
                catch
                {
                    // Provider unhealthy
                }
            }

            return healthyProviders;
        }

        /// <summary>
        /// Resets usage statistics for all providers.
        /// </summary>
        public void ResetUsageStats()
        {
            lock (_lock)
            {
                foreach (var stats in _usageStats.Values)
                {
                    stats.TotalRequests = 0;
                    stats.TotalInputTokens = 0;
                    stats.TotalOutputTokens = 0;
                    stats.TotalCostUsd = 0;
                }
            }
        }
    }

    /// <summary>
    /// Usage statistics for an LLM provider.
    /// </summary>
    public class ProviderUsageStats
    {
        /// <summary>Provider name.</summary>
        public string ProviderName { get; set; } = string.Empty;

        /// <summary>Total requests made to this provider.</summary>
        public long TotalRequests { get; set; }

        /// <summary>Total input tokens consumed.</summary>
        public long TotalInputTokens { get; set; }

        /// <summary>Total output tokens generated.</summary>
        public long TotalOutputTokens { get; set; }

        /// <summary>Total cost in USD.</summary>
        public decimal TotalCostUsd { get; set; }

        /// <summary>Last time this provider was used.</summary>
        public DateTime? LastUsed { get; set; }

        /// <summary>Average cost per request.</summary>
        public decimal AverageCostPerRequest =>
            TotalRequests > 0 ? TotalCostUsd / TotalRequests : 0;
    }
}
