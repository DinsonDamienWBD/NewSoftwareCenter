using DataWarehouse.Kernel.Diagnostics;
using DataWarehouse.Kernel.Indexing;
using DataWarehouse.Kernel.IO;
using DataWarehouse.SDK.Contracts;
using DataWarehouse.SDK.Governance; // [NEW] Governance Contracts
using DataWarehouse.SDK.Primitives;
using DataWarehouse.SDK.Security;
using DataWarehouse.SDK.Services;
using Microsoft.Extensions.Logging;

namespace DataWarehouse.Kernel.Engine
{
    /// <summary>
    /// The God Tier Microkernel.
    /// Acts as the Orchestrator for Storage, Security, and Active Governance.
    /// Strictly enforces Neural Sentinel judgments for Self-Healing and Auto-Compliance.
    /// </summary>
    public class DataWarehouse : IDataWarehouse, IKernelContext
    {
        private readonly string _rootPath;
        private readonly PluginRegistry _registry;
        private readonly HotPluginLoader _loader;
        private readonly HandshakePluginLoader _handshakeLoader;
        private readonly IKeyStore _keyStore;
        private readonly PolicyEnforcer _policy;
        private readonly RuntimeOptimizer _optimizer;
        private readonly PipelineOptimizer _pipelineoptimizer;
        private readonly ILogger<DataWarehouse> _logger;
        private readonly string _kernelId;

        // --- Core Plugin Interfaces ---
        private IAccessControl _acl = new UnsafeAclFallback();
        private IStorageProvider _storage;
        private IMetadataIndex _index = new InMemoryMetadataIndex();
        private INeuralSentinel _sentinel = new PassiveSentinelFallback();

        /// <summary>
        /// The root directory of the Data Warehouse instance.
        /// </summary>
        public string RootPath => _rootPath;

        /// <summary>
        /// The detected Operating Mode (Laptop, Server, etc.)
        /// </summary>
        public OperatingMode Mode => _optimizer.CurrentMode;

        /// <summary>
        /// Initializes the Data Warehouse Kernel.
        /// </summary>
        public DataWarehouse(
            string rootPath,
            PluginRegistry registry,
            RuntimeOptimizer optimizer,
            PipelineOptimizer pipelineOptimizer,
            IKeyStore keyStore,
            ILogger<DataWarehouse> logger)
        {
            _rootPath = rootPath;
            _registry = registry;
            _optimizer = optimizer;
            _pipelineoptimizer = pipelineOptimizer;
            _logger = logger;
            _keyStore = keyStore;
            _kernelId = Guid.NewGuid().ToString();

            _registry.SetOperatingMode(_optimizer.CurrentMode);

            // Initialize both loaders (old and new)
            _loader = new HotPluginLoader(_registry, this);
            _handshakeLoader = new HandshakePluginLoader(_registry, this, _kernelId);

            var globalConfig = new GlobalPolicyConfig
            {
                DefaultEnableCompression = true,
                DefaultEnableEncryption = true
            };
            _policy = new PolicyEnforcer(globalConfig);

            _storage = new LocalDiskProvider(_rootPath);
        }

        /// <summary>
        /// Configure
        /// </summary>
        /// <param name="config"></param>
        public static void Configure(Microsoft.Extensions.Configuration.IConfiguration config) { }

        /// <summary>
        /// Mount
        /// </summary>
        /// <returns></returns>
        public async Task MountAsync()
        {
            using var span = KernelTelemetry.StartActivity("Kernel.Mount"); // [NEW]

            LogInfo("--- MOUNTING DATA WAREHOUSE (SILVER TIER V5.1) ---");
            LogInfo($"Mode: {Mode}");
            LogInfo($"Root: {_rootPath}");

            Directory.CreateDirectory(_rootPath);

            // 1. Load Plugins
            string pluginsDir = Path.Combine(_rootPath, "Plugins");
            Directory.CreateDirectory(pluginsDir);

            // Feature flag: Use new handshake protocol (set to true to enable)
            bool useHandshakeProtocol = true;

            if (useHandshakeProtocol)
            {
                LogInfo("Using handshake-based plugin loader (message protocol)");
                var results = await _handshakeLoader.LoadPluginsFromAsync(pluginsDir);

                // Log summary
                var failed = results.Where(r => !r.IsSuccess).ToList();
                if (failed.Count != 0)
                {
                    LogWarning("Failed plugins:");
                    foreach (var failure in failed)
                    {
                        LogWarning($"  - {failure.FileName}: {failure.ErrorMessage}");
                    }
                }
            }
            else
            {
                LogInfo("Using legacy plugin loader (direct calls)");
                _loader.LoadPluginsFrom(pluginsDir);
            }

            // 2. Resolve Dependencies
            _acl = _registry.GetPlugin<IAccessControl>() ?? new UnsafeAclFallback();
            _storage = _registry.GetPlugin<IStorageProvider>() ?? new Kernel.IO.LocalDiskProvider(_rootPath);
            _index = _registry.GetPlugin<IMetadataIndex>() ?? new Kernel.Indexing.InMemoryMetadataIndex();
            _sentinel = _registry.GetPlugin<INeuralSentinel>() ?? new PassiveSentinelFallback();

            LogInfo($"ACL:      {_acl.Name}");
            LogInfo($"Storage:  {_storage.Name}");
            LogInfo($"Index:    {_index.Name}");
            LogInfo($"Sentinel: {_sentinel.Name}");

            // 3. Start Background Features
            foreach (var feature in _registry.GetPlugins<IFeaturePlugin>())
            {
                try
                {
                    LogInfo($"Starting Feature: {feature.Name}...");
                    using var fSpan = KernelTelemetry.StartActivity($"StartPlugin.{feature.Name}"); // [NEW]
                    await feature.StartAsync(System.Threading.CancellationToken.None);
                }
                catch (Exception ex)
                {
                    LogError($"Failed to start {feature.Name}", ex);
                    span?.SetStatus(System.Diagnostics.ActivityStatusCode.Error, ex.Message);
                }
            }
            LogInfo("--- SYSTEM ONLINE ---");
        }

        /// <summary>
        /// Dismount
        /// </summary>
        /// <returns></returns>
        public async Task DismountAsync()
        {
            LogInfo("--- DISMOUNTING ---");
            using var span = KernelTelemetry.StartActivity("Kernel.Dismount");
            foreach (var feature in _registry.GetPlugins<IFeaturePlugin>())
            {
                await feature.StopAsync();
            }
        }

        /// <summary>
        /// Health check
        /// </summary>
        public void CheckHealth() => LogInfo("Health Check: OK");

        // --- Core Operations (Store/Retrieve) ---

        /// <summary>
        /// Store
        /// </summary>
        /// <param name="bucket"></param>
        /// <param name="key"></param>
        /// <param name="data"></param>
        /// <param name="intent"></param>
        /// <returns></returns>
        public async Task StoreObjectAsync(string bucket, string key, Stream data, StorageIntent intent)
        {
            var context = new SimpleSecurityContext("System", true);
            await StoreBlobAsync(context, bucket, key, data);
        }

        /// <summary>
        /// Retrieve
        /// </summary>
        /// <param name="bucket"></param>
        /// <param name="key"></param>
        /// <returns></returns>
        public async Task<Stream> RetrieveObjectAsync(string bucket, string key)
        {
            var context = new SimpleSecurityContext("System", true);
            return await GetBlobAsync(context, bucket, key);
        }

        /// <summary>
        /// Stores a blob with Active Governance Enforcement.
        /// </summary>
        public async Task<string> StoreBlobAsync(ISecurityContext context, string containerId, string blobName, Stream data)
        {
            using var span = KernelTelemetry.StartActivity("Kernel.StoreBlob"); // [NEW] Start Trace
            span?.SetTag("container", containerId);
            span?.SetTag("blob", blobName);
            span?.SetTag("size", data.Length);
            span?.SetTag("user", context.UserId);

            try
            {
                // 1. Sentinel Scan (Pre-Write)
                using var scanSpan = KernelTelemetry.StartActivity("Sentinel.Evaluate");
                string fullPath = $"{containerId}/{blobName}";

                // 1. ACL Check
                if (!_acl.HasAccess(fullPath, context.UserId, Permission.Write))
                    throw new UnauthorizedAccessException($"Access Denied: Write permission required for {fullPath}");

                // 2. Resolve Pipeline & Save
                var pipelineConfig = _policy.ResolvePipeline(containerId, blobName);
                var uri = new Uri($"{_storage.Scheme}://{containerId}/{blobName}");
                var pipeline = _pipelineoptimizer.Resolve(new StorageIntent { Security = SecurityLevel.High, Compression = CompressionLevel.High });
                Stream processedStream = data;

                using (var ioSpan = KernelTelemetry.StartActivity("Storage.Write"))
                {
                    // Wrap: Compression
                    if (pipeline.EnableCompression)
                    {
                        var compressor = _registry.GetPlugin<IDataTransformation>(pipeline.CompressionProviderId);
                        if (compressor != null)
                        {
                            // Note: We use the 'OnWrite' method to wrap the stream
                            processedStream = compressor.OnWrite(processedStream, this, []);
                        }
                    }

                    // Wrap: Encryption
                    if (pipeline.EnableEncryption)
                    {
                        var encryptor = _registry.GetPlugin<IDataTransformation>(pipeline.CryptoProviderId);
                        if (encryptor != null)
                        {
                            var key = await _keyStore.GetKeyAsync("MASTER-01", context);
                            processedStream = encryptor.OnWrite(processedStream, this, new Dictionary<string, object> { { "Key", key } });
                        }
                    }
                    await _storage.SaveAsync(uri, data);
                }

                // 3. Indexing
                using var idxSpan = KernelTelemetry.StartActivity("Index.Update");
                var manifest = new Manifest
                {
                    Id = Guid.NewGuid().ToString("N"),
                    ContainerId = containerId,
                    OwnerId = context.UserId,
                    BlobUri = $"{_storage.Scheme}://{containerId}/{blobName}",
                    CreatedAt = DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
                    LastAccessedAt = DateTime.UtcNow.Ticks,
                    Pipeline = pipelineConfig
                };
                await _index.IndexManifestAsync(manifest);

                // 4. [NEW] NEURAL SENTINEL INTERVENTION (OnWrite)
                // The Sentinel inspects the raw data and the proposed pipeline BEFORE we write.
                var sentinelContext = new SentinelContext
                {
                    Trigger = TriggerType.OnWrite,
                    Metadata = manifest,
                    DataStream = data,
                    UserContext = context
                };

                // Sentinel analyzes stream (and must reset it)
                var judgment = await _sentinel.EvaluateAsync(sentinelContext);
                if (data.CanSeek) data.Position = 0; // Ensure stream is ready for reading

                // 5. ENFORCE JUDGMENT
                if (judgment.InterventionRequired)
                {
                    // A. Blocking
                    if (judgment.BlockOperation)
                    {
                        LogWarning($"[Governance] Blocked Write on {fullPath}: {judgment.Alert?.Message}");
                        throw new System.Security.SecurityException($"Governance Block: {judgment.Alert?.Message ?? "Risk Detected"}");
                    }

                    // B. Pipeline Override (Auto-Encryption)
                    if (judgment.EnforcePipeline != null)
                    {
                        LogInfo($"[Governance] Overriding Pipeline for {fullPath} based on Sentinel Judgment.");
                        manifest.Pipeline = judgment.EnforcePipeline;
                        // Ensure we have a valid Key ID if encryption was forced
                        if (string.IsNullOrEmpty(manifest.Pipeline.KeyId))
                        {
                            manifest.Pipeline.KeyId = await _keyStore.GetCurrentKeyIdAsync();
                        }
                    }

                    // C. Metadata Enrichment (Auto-Tagging)
                    foreach (var tag in judgment.AddTags)
                    {
                        _ = manifest.GovernanceTags.TryAdd(tag, "True");
                    }
                    foreach (var prop in judgment.UpdateProperties)
                    {
                        // Logic to reflect properties onto Manifest if applicable
                        if (prop.Key == "ContentType") manifest.Tags["ContentType"] = prop.Value;
                    }

                    // D. Alerting
                    if (judgment.Alert != null)
                    {
                        LogWarning($"[Sentinel Alert] {judgment.Alert.Code}: {judgment.Alert.Message}");
                    }
                }

                // 6. Execute Pipeline (Using the potentially modified Manifest.Pipeline)
                var runtimeArgs = await PrepareRuntimeArgs(manifest.Pipeline, context);
                processedStream = data;
                var disposables = new List<IDisposable>();

                try
                {
                    foreach (var stepName in manifest.Pipeline.TransformationOrder)
                    {
                        var middleware = ResolveTransformation(stepName);
                        if (middleware != null)
                        {
                            var nextStream = middleware.OnWrite(processedStream, this, runtimeArgs);
                            processedStream = nextStream;
                            disposables.Add(nextStream);
                        }
                    }

                    await _storage.SaveAsync(new Uri(manifest.BlobUri), processedStream);
                    manifest.SizeBytes = data.CanSeek ? data.Length : 0;
                }
                finally
                {
                    disposables.Reverse();
                    foreach (var d in disposables) d.Dispose();
                }

                // 7. Index
                await _index.IndexManifestAsync(manifest);
                return manifest.Id;
            }
            catch (Exception ex)
            {
                span?.SetStatus(System.Diagnostics.ActivityStatusCode.Error, ex.Message);
                throw;
            }
        }

        /// <summary>
        /// Retrieves a blob with Active Governance Checks.
        /// </summary>
        public async Task<Stream> GetBlobAsync(ISecurityContext context, string containerId, string blobName)
        {
            using var span = KernelTelemetry.StartActivity("Kernel.GetBlob"); // [NEW]
            span?.SetTag("blob", $"{containerId}/{blobName}");
            string fullPath = $"{containerId}/{blobName}";

            // 1. Check ACL
            if (!_acl.HasAccess($"{containerId}/{blobName}", context.UserId, Permission.Read))
            {
                span?.SetStatus(System.Diagnostics.ActivityStatusCode.Error, "Access Denied");
                throw new UnauthorizedAccessException($"Access Denied: Read permission required for {fullPath}");
            }

            // 2. Load
            using var ioSpan = KernelTelemetry.StartActivity("Storage.Read");
            // 2. Load Manifest
            var manifest = await _index.GetManifestAsync(blobName);
            // Fallback for unindexed files
            var pipelineConfig = manifest?.Pipeline ?? _policy.ResolvePipeline(containerId, blobName);

            // 3. [NEW] NEURAL SENTINEL INTERVENTION (OnRead)
            // Check if we should block access (e.g., Quarantine)
            if (_sentinel != null)
            {
                var sentinelContext = new SentinelContext
                {
                    Trigger = TriggerType.OnRead,
                    Metadata = manifest ?? new Manifest { ContainerId = containerId, BlobUri = fullPath },
                    DataStream = null, // Meta-check only for speed, unless DeepScan enabled
                    UserContext = context
                };

                var judgment = await _sentinel.EvaluateAsync(sentinelContext);

                if (judgment.InterventionRequired && judgment.BlockOperation)
                {
                    LogWarning($"[Governance] Blocked Read on {fullPath}: {judgment.Alert?.Message}");
                    throw new System.Security.SecurityException($"Access Denied by Sentinel: {judgment.Alert?.Message}");
                }
            }

            // 4. Load & Reverse Pipeline
            var runtimeArgs = await PrepareRuntimeArgs(pipelineConfig, context);
            var uri = new Uri($"{_storage.Scheme}://{containerId}/{blobName}");
            Stream currentStream = await _storage.LoadAsync(uri);

            var readOrder = new List<string>(pipelineConfig.TransformationOrder);
            readOrder.Reverse();

            foreach (var stepName in readOrder)
            {
                var middleware = ResolveTransformation(stepName);
                if (middleware != null)
                {
                    currentStream = middleware.OnRead(currentStream, this, runtimeArgs);
                }
            }

            return currentStream;
        }

        // --- Standard Implementations (CreateContainer, GrantAccess, etc.) ---

        private record SimpleSecurityContext(string UserId, bool IsSystemAdmin) : ISecurityContext
        {
            public string? TenantId => "Default";
            public IEnumerable<string> Roles => [];
        }

        /// <inheritdoc/>
        public async Task CreateContainerAsync(ISecurityContext context, string containerId, bool encrypt, bool compress)
        {
            _acl.CreateScope(containerId, context.UserId);
            await Task.CompletedTask;
        }

        /// <inheritdoc/>
        public async Task GrantAccessAsync(ISecurityContext owner, string containerId, string targetUser, AccessLevel level)
        {
            if (!_acl.HasAccess(containerId, owner.UserId, Permission.FullControl))
                throw new UnauthorizedAccessException("Not Authorized");

            Permission p = level switch
            {
                AccessLevel.Read => Permission.Read,
                AccessLevel.Write => Permission.Read | Permission.Write,
                AccessLevel.FullControl => Permission.FullControl,
                _ => Permission.None
            };
            _acl.SetPermissions(containerId, targetUser, p, Permission.None);
            await Task.CompletedTask;
        }

        /// <inheritdoc/>
        public Task<string[]> SearchAsync(ISecurityContext context, string query, float[]? vector, int limit)
            => _index.SearchAsync(query, vector, limit);

        // --- Support Methods ---

        /// <inheritdoc/>
        public T? GetPlugin<T>() where T : class, IPlugin
        {
            return _registry.GetPlugin<T>();
        }

        /// <summary>
        /// Get plugin
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="id"></param>
        /// <returns></returns>
        public T? GetPlugin<T>(string? id = null) where T : class, IPlugin
        {
            // If id is null, Registry.GetPlugin<T>() (no args) is called
            // If id is set, Registry.GetPlugin<T>(id) is called
            return string.IsNullOrEmpty(id)
                ? _registry.GetPlugin<T>()
                : _registry.GetPlugin<T>(id);
        }

        /// <inheritdoc/>
        public IEnumerable<T> GetPlugins<T>() where T : class, IPlugin => _registry.GetPlugins<T>();

        /// <summary>
        /// Log info
        /// </summary>
        /// <param name="m"></param>
        public void LogInfo(string m) => _logger.LogInformation("[INFO] {m}", m);

        /// <summary>
        /// Log warning
        /// </summary>
        /// <param name="m"></param>
        public void LogWarning(string m) => _logger.LogWarning("[WARN] {m}", m);

        /// <summary>
        /// Log error
        /// </summary>
        /// <param name="m"></param>
        /// <param name="e"></param>
        public void LogError(string m, Exception? e) => _logger.LogError("[ERR] {m} {e}", m, e);

        /// <summary>
        /// Log debug
        /// </summary>
        /// <param name="m"></param>
        public void LogDebug(string m) => _logger.LogDebug("[DBG] {m}", m);

        private async Task<Dictionary<string, object>> PrepareRuntimeArgs(PipelineConfig config, ISecurityContext context)
        {
            var args = new Dictionary<string, object>
            {
                { "Owner", context.UserId },
                { "Tenant", context.TenantId ?? "Default" }
            };

            bool needsKey = config.TransformationOrder.Any(s =>
                s.Contains("Encryption", StringComparison.OrdinalIgnoreCase) ||
                s.Contains("Aes", StringComparison.OrdinalIgnoreCase));

            if (needsKey && !string.IsNullOrEmpty(config.KeyId))
            {
                byte[] key = await _keyStore.GetKeyAsync(config.KeyId, context);
                args["Key"] = key;
            }
            return args;
        }

        private IDataTransformation? ResolveTransformation(string stepName)
        {
            var plugin = _registry.GetPlugin<IDataTransformation>(stepName);
            if (plugin != null) return plugin;

            return _registry.GetPlugins<IDataTransformation>()
                .Where(p => p.Category.Equals(stepName, StringComparison.OrdinalIgnoreCase))
                .OrderByDescending(p => p.QualityLevel)
                .FirstOrDefault();
        }

        // --- Fallbacks ---

        private class UnsafeAclFallback : IAccessControl
        {
            public static string Id => "unsafe-acl";
            public static string Name => "Unsafe ACL";
            public static string Version => "0.0";
            public static void Initialize(IKernelContext c) { }
            public static Task StartAsync(CancellationToken c) => Task.CompletedTask;
            public static Task StopAsync() => Task.CompletedTask;
            public void CreateScope(string r, string o) { }
            public void SetPermissions(string r, string s, Permission a, Permission d) { }
            public bool HasAccess(string r, string s, Permission p) => true;
        }

        private class PassiveSentinelFallback : INeuralSentinel
        {
            public static string Id => "passive-sentinel";
            public static string Name => "Passive Sentinel";
            public static string Version => "0.0";
            public static void Initialize(IKernelContext c) { }
            public static Task StartAsync(CancellationToken c) => Task.CompletedTask;
            public static Task StopAsync() => Task.CompletedTask;
            public Task<GovernanceJudgment> EvaluateAsync(SentinelContext c)
                => Task.FromResult(new GovernanceJudgment { InterventionRequired = false });
        }
    }
}