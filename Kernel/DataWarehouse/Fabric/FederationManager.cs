using System.Collections.Concurrent;
using DataWarehouse.Contracts;
using DataWarehouse.Drivers;
using Microsoft.Extensions.Logging;

namespace DataWarehouse.Fabric
{
    /// <summary>
    /// This manages the links. It allows you to "Mount" other people's DWs.
    /// </summary>
    public class FederationManager : IDisposable, IPlugin
    {
        /// <summary>
        /// ID
        /// </summary>
        public string Id => "FederationManager";

        /// <summary>
        /// Version
        /// </summary>
        public string Version => "1.0";

        // Active runtime providers (RAM only)
        // e.g. "/server" -> NetworkStorageProvider("192.168.1.50")
        private readonly ConcurrentDictionary<string, NetworkStorageProvider> _activeMounts = new();
        
        // Persistent Configuration (Disk)
        // Map: Alias (e.g., "/team") -> ConnectionString (e.g., "192.168.1.50")
        private readonly DurableState<string> _configStore;

        private readonly ILoggerFactory _loggerFactory;

        /// <summary>
        /// Constructor
        /// </summary>
        /// <param name="metadataPath"></param>
        /// <param name="loggerFactory"></param>
        public FederationManager(string metadataPath, ILoggerFactory loggerFactory)
        {
            _loggerFactory = loggerFactory;
            _configStore = new DurableState<string>(metadataPath, "federation_links");
            RehydrateAsync().Wait(); 
        }

        private async Task RehydrateAsync()
        {
            // On startup, reload all saved links
            // We need to expose a way to iterate DurableState, or just load specific keys if we knew them.
            // For this implementation, let's assume DurableState exposes a GetAll() or we iterate known keys.

            var savedLinks = _configStore.GetAll(); // Added method to DurableState
            foreach (var link in savedLinks)
            {
                try
                {
                    // Re-connect silently
                    MountInternal(link.Key, link.Value);
                }
                catch
                {
                    // Log error but don't crash if server is offline
                    // In a real UI, this would show as "Red/Offline" status
                }
            }
        }

        /// <summary>
        /// Mount a remote DW
        /// </summary>
        /// <param name="alias"></param>
        /// <param name="connectionString"></param>
        public void MountRemote(string alias, string connectionString)
        {
            // 1. Validate inputs
            if (string.IsNullOrWhiteSpace(alias) || !alias.StartsWith('/'))
                throw new ArgumentException("Alias must start with /");

            // 2. Persist Intent (So it survives reboot)
            _configStore.Set(alias, connectionString);

            // 3. Activate
            MountInternal(alias, connectionString);
        }

        private void MountInternal(string alias, string connectionString)
        {
            // Create the driver (Network, gRPC, etc.)
            var logger = _loggerFactory.CreateLogger<NetworkStorageProvider>();
            var provider = new NetworkStorageProvider(connectionString, logger);
            _activeMounts[alias] = provider;
        }

        /// <summary>
        /// Unmount a remote DW
        /// </summary>
        /// <param name="alias"></param>
        public void Unmount(string alias)
        {
            // 1. Remove from RAM
            if (_activeMounts.TryRemove(alias, out var provider)) provider.Dispose();

            // 2. Remove from Disk
            _configStore.Remove(alias); // Added Remove method to DurableState
        }

        /// <summary>
        /// Routing Logic.
        /// If user asks for "net://server/docs/file.txt", this finds the right provider.
        /// </summary>
        public IStorageProvider? Resolve(Uri uri)
        {
            foreach (var mount in _activeMounts)
            {
                if (uri.ToString().Contains(mount.Key)) return mount.Value;
            }
            return null;
        }

        /// <summary>
        /// Get links
        /// </summary>
        /// <returns></returns>
        public IEnumerable<KeyValuePair<string, string>> GetLinks()
        {
            return _configStore.GetAll();
        }

        /// <summary>
        /// Check if is remote
        /// </summary>
        /// <param name="bucket"></param>
        /// <returns></returns>
        public bool IsRemote(string bucket)
        {
            // If we have a mount matching the bucket name (e.g. bucket "team" matches mount "/team")
            string alias = bucket.StartsWith('/') ? bucket : "/" + bucket; 
            return _activeMounts.ContainsKey(alias);
        }

        /// <summary>
        /// Get provider
        /// </summary>
        /// <param name="bucket"></param>
        /// <returns></returns>
        /// <exception cref="KeyNotFoundException"></exception>
        public IStorageProvider GetProvider(string bucket)
        {
            string alias = bucket.StartsWith('/') ? bucket : "/" + bucket; 
            if (_activeMounts.TryGetValue(alias, out var provider)) return provider;
            throw new KeyNotFoundException($"No remote link found for alias {alias}");
        }

        /// <summary>
        /// Safely dispose
        /// </summary>
        public void Dispose()
        {
            foreach (var p in _activeMounts.Values) p.Dispose();
            _configStore.Dispose();
            GC.SuppressFinalize(this);
        }
    }
}