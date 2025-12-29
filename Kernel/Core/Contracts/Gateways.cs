using Core.Pipeline;
using System.Threading.Tasks;

namespace Core.Contracts
{
    /// <summary>
    /// Resilience Fallback
    /// </summary>
    /// <typeparam name="TResponse"></typeparam>
    public interface IFallbackPolicy<TResponse>
    {
        /// <summary>
        /// Provides a fallback value when the primary operation fails.
        /// </summary>
        Task<TResponse> GetFallbackAsync(Exception failure);
    }

    /// <summary>
    /// Gateway for checking Feature Flags / Toggles.
    /// </summary>
    public interface IFeatureFlagProvider
    {
        /// <summary>
        /// Indicates whether the specified feature is enabled.
        /// </summary>
        /// <param name="featureId"></param>
        /// <returns></returns>
        Task<bool> IsEnabledAsync(string featureId);

        /// <summary>
        /// Checks a flag against the full request context (User, Tenant, IP, etc).
        /// </summary>
        Task<bool> IsEnabledAsync(string featureId, PipelineContext context);

        /// <summary>
        /// Indicates whether the specified feature is enabled for the specified user.
        /// </summary>
        /// <param name="featureId"></param>
        /// <param name="userId"></param>
        /// <returns></returns>
        Task<bool> IsEnabledForUserAsync(string featureId, string userId);
    }

    /// <summary>
    /// Gateway for localization strings.
    /// </summary>
    public interface ILocalizer
    {
        /// <summary>
        /// Gets the localized string for the specified key.
        /// </summary>
        /// <param name="key"></param>
        /// <param name="args"></param>
        /// <returns></returns>
        string GetString(string key, params object[] args);

        /// <summary>
        /// Gets the localized string for the specified culture code and key.
        /// </summary>
        /// <param name="cultureCode"></param>
        /// <param name="key"></param>
        /// <param name="args"></param>
        /// <returns></returns>
        string GetStringForCulture(string cultureCode, string key, params object[] args);
    }

    /// <summary>
    /// Lightweight Identity wrapper to avoid ClaimsPrincipal parsing in modules.
    /// </summary>
    public interface IUserContext
    {
        /// <summary>
        /// Indicates the User's unique identifier.
        /// </summary>
        string UserId { get; }

        /// <summary>
        /// Indicates the Tenant's unique identifier.
        /// </summary>
        string TenantId { get; }

        /// <summary>
        /// Nice display name for the user.
        /// </summary>
        string DisplayName { get; }

        /// <summary>
        /// Role check for the user.
        /// </summary>
        /// <param name="role"></param>
        /// <returns></returns>
        bool IsInRole(string role);
    }

    /// <summary>
    /// Environment information about the host application.
    /// </summary>
    public interface IHostEnvironment
    {
        /// <summary>
        /// Name of the current environment (e.g., Development, Staging, Production).
        /// </summary>
        string EnvironmentName { get; }

        /// <summary>
        /// Checks if the current environment is Development.
        /// </summary>
        bool IsDevelopment { get; }

        /// <summary>
        /// Machine hostname
        /// </summary>
        string MachineName { get; }

        /// <summary>
        /// Application name
        /// </summary>
        string ApplicationName { get; }

        /// <summary>
        /// Process ID
        /// </summary>
        int ProcessId { get; }

        /// <summary>
        /// OS
        /// </summary>
        string OSDescription { get; }
    }

    /// <summary>
    /// Service to manage application lifetime events.
    /// Shutdown Synchronization
    /// </summary>
    public interface IApplicationLifetime
    {
        /// <summary>
        /// Signals a request to stop the application.
        /// </summary>
        void RequestStop();
    }

    /// <summary>
    /// Cache Provider for caching data.
    /// </summary>
    public interface ICacheProvider
    {
        /// <summary>
        /// Gets a cached item by key.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="key"></param>
        /// <returns></returns>
        Task<T?> GetAsync<T>(string key);

        /// <summary>
        /// Sets a cached item by key with an optional expiration.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="key"></param>
        /// <param name="value"></param>
        /// <param name="expiration"></param>
        /// <returns></returns>
        Task SetAsync<T>(string key, T value, TimeSpan? expiration = null);

        /// <summary>
        /// Deletes a cached item by key.
        /// </summary>
        /// <param name="key"></param>
        /// <returns></returns>
        Task RemoveAsync(string key);
    }

    /// <summary>
    /// Secret Provider for retrieving sensitive configuration values.
    /// </summary>
    public interface ISecretProvider
    {
        /// <summary>
        /// Gets a secret value by key.
        /// </summary>
        /// <param name="key"></param>
        /// <returns></returns>
        Task<string> GetSecretAsync(string key);
    }

    /// <summary>
    /// Distributed Lock interface.
    /// </summary>
    public interface IDistributedLock : IAsyncDisposable
    {
        /// <summary>
        /// Confirms whether the lock was successfully acquired.
        /// </summary>
        bool IsAcquired { get; }
    }

    /// <summary>
    /// Distributed Lock Provider for acquiring distributed locks.
    /// </summary>
    public interface IDistributedLockProvider
    {
        /// <summary>
        /// Gets a distributed lock for the specified resource key with a timeout.
        /// </summary>
        /// <param name="resourceKey"></param>
        /// <param name="timeout"></param>
        /// <returns></returns>
        Task<IDistributedLock> AcquireAsync(string resourceKey, TimeSpan timeout);
    }

    /// <summary>
    /// Tenant provider
    /// </summary>
    public interface ITenantResolver
    {
        /// <summary>
        /// Resolve tenant
        /// </summary>
        /// <returns></returns>
        Task<string?> ResolveAsync();
    }

    /// <summary>
    /// Manage quotas
    /// </summary>
    public interface IQuotaManager
    {
        /// <summary>
        /// Checks if the usage limit for a key has been exceeded.
        /// </summary>
        Task<bool> CheckQuotaAsync(string resourceKey, int increment = 1);

        /// <summary>
        /// Gets remaining usage.
        /// </summary>
        Task<long> GetRemainingAsync(string resourceKey);
    }
}