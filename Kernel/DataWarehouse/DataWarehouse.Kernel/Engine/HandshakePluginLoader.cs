using DataWarehouse.SDK.Contracts;
using DataWarehouse.SDK.Services;
using System.Collections.Concurrent;
using System.Reflection;
using System.Runtime.Loader;

namespace DataWarehouse.Kernel.Engine
{
    /// <summary>
    /// Modern plugin loader using message-based handshake protocol.
    /// Supports async initialization, parallel loading, and dependency resolution.
    /// </summary>
    public class HandshakePluginLoader(PluginRegistry registry, IKernelContext context, string kernelId)
    {
        private readonly PluginRegistry _registry = registry;
        private readonly IKernelContext _context = context;
        private readonly string _kernelId = kernelId;
        private readonly ConcurrentDictionary<string, TaskCompletionSource<HandshakeResponse>> _pendingHandshakes = new();

        /// <summary>
        /// Load all plugins from a directory in parallel.
        /// </summary>
        /// <param name="directory">Directory containing plugin DLLs.</param>
        /// <param name="cancellationToken">Cancellation token for the operation.</param>
        /// <returns>List of load results for all plugins.</returns>
        public async Task<List<LoadPluginResult>> LoadPluginsFromAsync(
            string directory,
            CancellationToken cancellationToken = default)
        {
            if (!Directory.Exists(directory))
            {
                _context.LogWarning("Plugin directory not found: {directory}", directory);
                return [];
            }

            var dlls = Directory.GetFiles(directory, "*.dll", SearchOption.AllDirectories)
                .Where(dll => !dll.Contains("DataWarehouse.Kernel") && !dll.Contains("DataWarehouse.SDK"))
                .ToList();

            _context.LogInfo($"Discovered {dlls.Count} plugin DLLs");

            // Load all plugins in parallel
            var loadTasks = dlls.Select(dll => LoadPluginAsync(
                dll,
                timeout: TimeSpan.FromSeconds(30),
                cancellationToken));

            var results = await Task.WhenAll(loadTasks);

            // Summary
            var succeeded = results.Count(r => r.IsSuccess);
            var failed = results.Length - succeeded;

            if (succeeded > 0)
                _context.LogInfo($"✓ Loaded {succeeded}/{results.Length} plugins successfully");
            if (failed > 0)
                _context.LogWarning($"✗ Failed to load {failed} plugins");

            return [.. results];
        }

        /// <summary>
        /// Load a single plugin from a DLL file.
        /// </summary>
        /// <param name="assemblyPath">Path to the plugin DLL.</param>
        /// <param name="timeout">Maximum time to wait for handshake.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>Result of the plugin load operation.</returns>
        public async Task<LoadPluginResult> LoadPluginAsync(
            string assemblyPath,
            TimeSpan timeout,
            CancellationToken cancellationToken = default)
        {
            var startTime = DateTime.UtcNow;
            var fileName = Path.GetFileName(assemblyPath);

            try
            {
                // 1. Load assembly and discover plugin types
                var loadContext = new PluginLoadContext(assemblyPath);
                var assembly = loadContext.LoadFromAssemblyPath(assemblyPath);

                var pluginTypes = assembly.GetTypes()
                    .Where(t => typeof(IPlugin).IsAssignableFrom(t) && !t.IsInterface && !t.IsAbstract)
                    .ToList();

                if (pluginTypes.Count == 0)
                {
                    return LoadPluginResult.Failed(fileName, "No IPlugin implementations found");
                }

                if (pluginTypes.Count > 1)
                {
                    _context.LogWarning($"{fileName} contains {pluginTypes.Count} plugins. Loading first one only.");
                }

                var pluginType = pluginTypes[0];

                // 2. Create plugin instance
                var plugin = (IPlugin)Activator.CreateInstance(pluginType)!;
                var tempId = Guid.NewGuid().ToString();

                // 3. Setup response handler
                var tcs = new TaskCompletionSource<HandshakeResponse>();
                _pendingHandshakes[tempId] = tcs;

                // 4. Build handshake request
                var request = new HandshakeRequest
                {
                    KernelId = _kernelId,
                    ProtocolVersion = "1.0",
                    Timestamp = DateTime.UtcNow,
                    Mode = _context.Mode,
                    RootPath = _context.RootPath,
                    AlreadyLoadedPlugins = _registry.GetAllDescriptors()
                };

                // 5. Send handshake (fire and forget, plugin responds asynchronously)
                _ = Task.Run(async () =>
                {
                    try
                    {
                        var response = await plugin.OnHandshakeAsync(request);
                        tcs.TrySetResult(response);
                    }
                    catch (Exception ex)
                    {
                        tcs.TrySetException(ex);
                    }
                }, cancellationToken);

                // 6. Wait for response with timeout
                using var timeoutCts = new CancellationTokenSource(timeout);
                using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, timeoutCts.Token);

                HandshakeResponse response;
                try
                {
                    response = await tcs.Task.WaitAsync(linkedCts.Token);
                }
                catch (OperationCanceledException)
                {
                    if (timeoutCts.IsCancellationRequested)
                    {
                        return LoadPluginResult.Failed(fileName, $"Handshake timed out after {timeout.TotalSeconds}s");
                    }
                    throw;
                }

                // 7. Validate response
                if (!response.IsSuccess)
                {
                    return LoadPluginResult.Failed(
                        fileName,
                        $"Plugin '{response.Name}' initialization failed: {response.ErrorMessage}");
                }

                if (response.ReadyState == PluginReadyState.NotReady)
                {
                    return LoadPluginResult.Failed(fileName, $"Plugin '{response.Name}' is not ready");
                }

                // 8. Check dependencies
                var missingDeps = CheckDependencies(response.Dependencies);
                if (missingDeps.Count != 0)
                {
                    return LoadPluginResult.Failed(
                        fileName,
                        $"Plugin '{response.Name}' missing required dependencies: {string.Join(", ", missingDeps)}");
                }

                // 9. Register plugin and capabilities
                _registry.Register(plugin, response);

                // TODO: Register capabilities when capability system is implemented
                // foreach (var capability in response.Capabilities)
                // {
                //     _registry.RegisterCapability(capability, plugin);
                // }

                var duration = DateTime.UtcNow - startTime;

                // 10. Log success
                var stateEmoji = response.ReadyState switch
                {
                    PluginReadyState.Ready => "✓",
                    PluginReadyState.Initializing => "⏳",
                    PluginReadyState.PartiallyReady => "⚠",
                    PluginReadyState.Degraded => "⚠",
                    _ => "?"
                };

                _context.LogInfo(
                    $"{stateEmoji} {response.Name} v{response.Version} " +
                    $"({response.Category}, {response.Capabilities.Count} capabilities, " +
                    $"{duration.TotalMilliseconds:F0}ms)");

                // 11. Handle deferred initialization
                if (response.ReadyState == PluginReadyState.Initializing)
                {
                    _context.LogDebug($"Plugin '{response.Name}' is still initializing in background");
                    // TODO: Listen for PluginStateChangedEvent
                }

                // 12. Start health monitoring if requested
                if (response.HealthCheckInterval.HasValue)
                {
                    StartHealthMonitoring(plugin, response);
                }

                return LoadPluginResult.Success(fileName, response, duration);
            }
            catch (Exception ex)
            {
                _context.LogError($"Failed to load {fileName}", ex);
                return LoadPluginResult.Failed(fileName, $"Exception: {ex.Message}");
            }
            finally
            {
                _pendingHandshakes.TryRemove(tempId, out _);
            }
        }

        /// <summary>
        /// Check if all required dependencies are available.
        /// </summary>
        private List<string> CheckDependencies(List<PluginDependency> dependencies)
        {
            var missing = new List<string>();

            foreach (var dep in dependencies.Where(d => !d.IsOptional))
            {
                if (!_registry.HasInterface(dep.RequiredInterface))
                {
                    missing.Add($"{dep.RequiredInterface} (v{dep.MinimumVersion})");
                }
            }

            return missing;
        }

        /// <summary>
        /// Start periodic health monitoring for a plugin.
        /// </summary>
        private void StartHealthMonitoring(IPlugin plugin, HandshakeResponse response)
        {
            if (!response.HealthCheckInterval.HasValue) return;

            _ = Task.Run(async () =>
            {
                var interval = response.HealthCheckInterval.Value;
                _context.LogDebug($"Starting health monitoring for '{response.Name}' every {interval.TotalMinutes:F1}m");

                while (true)
                {
                    await Task.Delay(interval);

                    try
                    {
                        var healthCheck = new HealthCheckRequest
                        {
                            KernelId = _kernelId,
                            Timestamp = DateTime.UtcNow
                        };

                        // TODO: Implement health check message handling
                        // var healthResponse = await plugin.OnMessageAsync(healthCheck);
                        // if (!healthResponse.IsHealthy)
                        // {
                        //     _context.LogWarning($"Plugin '{response.Name}' reported unhealthy: {healthResponse.StatusMessage}");
                        // }
                    }
                    catch (Exception ex)
                    {
                        _context.LogError($"Health check failed for '{response.Name}'", ex);
                    }
                }
            });
        }

        /// <summary>
        /// Nested class for plugin assembly loading with isolation.
        /// </summary>
        private class PluginLoadContext(string pluginPath) : AssemblyLoadContext(pluginPath, isCollectible: true)
        {
            private readonly string _pluginPath = pluginPath;

            protected override Assembly? Load(AssemblyName assemblyName)
            {
                // Defer to default context for SDK/Shared types (avoid version conflicts)
                if (assemblyName.Name != null && assemblyName.Name.Contains("DataWarehouse.SDK"))
                {
                    return null;
                }

                // Try to load dependency from plugin's directory
                var folder = Path.GetDirectoryName(_pluginPath);
                if (folder != null)
                {
                    var localPath = Path.Combine(folder, assemblyName.Name + ".dll");
                    if (File.Exists(localPath))
                    {
                        return LoadFromAssemblyPath(localPath);
                    }
                }

                return null;
            }
        }
    }

    /// <summary>
    /// Result of a plugin load operation.
    /// </summary>
    public class LoadPluginResult
    {
        public bool IsSuccess { get; init; }
        public string FileName { get; init; } = string.Empty;
        public string? ErrorMessage { get; init; }
        public HandshakeResponse? Response { get; init; }
        public TimeSpan LoadDuration { get; init; }

        public static LoadPluginResult Success(string fileName, HandshakeResponse response, TimeSpan duration) =>
            new()
            {
                IsSuccess = true,
                FileName = fileName,
                Response = response,
                LoadDuration = duration
            };

        public static LoadPluginResult Failed(string fileName, string error) =>
            new()
            {
                IsSuccess = false,
                FileName = fileName,
                ErrorMessage = error
            };
    }
}
