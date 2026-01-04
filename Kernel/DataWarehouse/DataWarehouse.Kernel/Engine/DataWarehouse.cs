using DataWarehouse.Kernel.Indexing;
using DataWarehouse.Kernel.IO;
using DataWarehouse.Kernel.Primitives;
using DataWarehouse.Kernel.Security;
using DataWarehouse.SDK.Contracts;
using DataWarehouse.SDK.Governance; // [NEW] Governance Contracts
using DataWarehouse.SDK.Primitives;
using DataWarehouse.SDK.Security;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace DataWarehouse.Kernel.Engine
{
    /// <summary>
    /// The God Tier Microkernel.
    /// Acts as the Orchestrator for Storage, Security, and now Active Governance.
    /// Strictly enforces Neural Sentinel judgments for Self-Healing and Auto-Compliance.
    /// </summary>
    public class DataWarehouse : IDataWarehouse, IKernelContext
    {
        private readonly string _rootPath;
        private readonly PluginRegistry _registry;
        private readonly HotPluginLoader _loader;
        private readonly KeyStoreAdapter _keyStore;
        private readonly PolicyEnforcer _policy;

        // --- Core Plugin Interfaces ---
        private IAccessControl _acl;
        private IStorageProvider _storage;
        private IMetadataIndex _index;
        private INeuralSentinel _sentinel; // [NEW] The Active Guardian

        /// <summary>
        /// The root directory of the Data Warehouse instance.
        /// </summary>
        public string RootPath => _rootPath;

        /// <summary>
        /// Initializes the Data Warehouse Kernel.
        /// </summary>
        /// <param name="rootPath">Physical path on disk.</param>
        /// <param name="globalPolicy">Optional global policy overrides.</param>
        public DataWarehouse(string rootPath, GlobalPolicyConfig? globalPolicy = null)
        {
            _rootPath = rootPath;
            Directory.CreateDirectory(_rootPath);

            // 1. Boot Core Kernel Services
            _registry = new PluginRegistry();
            _loader = new HotPluginLoader(_registry, this);
            _keyStore = new KeyStoreAdapter(rootPath);
            _policy = new PolicyEnforcer(globalPolicy ?? new GlobalPolicyConfig());

            // 2. Load External Plugins
            string pluginPath = Path.Combine(rootPath, "Plugins");
            Directory.CreateDirectory(pluginPath);
            _loader.LoadPluginsFrom(pluginPath);

            // 3. Resolve Dependencies
            ResolveCorePlugins();

            // 4. Start Background Features
            foreach (var feature in _registry.GetFeatures())
            {
                Task.Run(() => feature.StartAsync(CancellationToken.None));
            }

            LogInfo($"[Kernel] Boot Complete. Sentinel: {_sentinel.Name}");
        }

        private void ResolveCorePlugins()
        {
            // A. Index
            _index = _registry.GetPlugin<IMetadataIndex>() ?? new InMemoryMetadataIndex();
            (_index as IPlugin)?.Initialize(this);

            // B. Storage
            _storage = _registry.GetStorage("file");
            if (_storage == null)
            {
                LogWarning("[Kernel] No physical Storage Plugin found. Booting into SAFE MODE.");
                var ramStorage = new InMemoryStorageProvider();
                ramStorage.Initialize(this);
                _storage = ramStorage;
            }

            // C. ACL
            _acl = _registry.GetPlugin<IAccessControl>();
            if (_acl == null)
            {
                LogWarning("[Kernel] No ACL Plugin found. Security is OPEN.");
                var unsafeAcl = new UnsafeAclFallback();
                unsafeAcl.Initialize(this);
                _acl = unsafeAcl;
            }

            // D. Sentinel (Governance)
            // Priority: Plugin -> Passive Fallback
            _sentinel = _registry.GetPlugin<INeuralSentinel>();
            if (_sentinel == null)
            {
                LogInfo("[Kernel] No Neural Sentinel found. Governance is PASSIVE.");
                var passive = new PassiveSentinelFallback();
                passive.Initialize(this);
                _sentinel = passive;
            }
        }

        // --- IDataWarehouse Implementation ---

        /// <summary>
        /// Stores a blob with Active Governance Enforcement.
        /// </summary>
        public async Task<string> StoreBlobAsync(ISecurityContext context, string containerId, string blobName, Stream data)
        {
            string fullPath = $"{containerId}/{blobName}";

            // 1. ACL Check
            if (!_acl.HasAccess(fullPath, context.UserId, Permission.Write))
                throw new UnauthorizedAccessException($"Access Denied: Write permission required for {fullPath}");

            // 2. Policy Resolution (User Intent + Global Rules)
            var pipelineConfig = _policy.ResolvePipeline(containerId, blobName);

            // 3. Prepare Metadata
            var manifest = new Manifest
            {
                Id = Guid.NewGuid().ToString("N"),
                ContainerId = containerId,
                OwnerId = context.UserId,
                BlobUri = $"{_storage.Scheme}://{containerId}/{blobName}",
                CreatedAt = DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
                Pipeline = pipelineConfig
            };

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
            Stream processedStream = data;
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

        /// <summary>
        /// Retrieves a blob with Active Governance Checks.
        /// </summary>
        public async Task<Stream> GetBlobAsync(ISecurityContext context, string containerId, string blobName)
        {
            string fullPath = $"{containerId}/{blobName}";

            // 1. ACL Check
            if (!_acl.HasAccess(fullPath, context.UserId, Permission.Read))
                throw new UnauthorizedAccessException($"Access Denied: Read permission required for {fullPath}");

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
        public T? GetPlugin<T>(string? id = null) where T : class, IPlugin => _registry.GetPlugin<T>(id);

        /// <inheritdoc/>
        public IEnumerable<T> GetPlugins<T>() where T : class, IPlugin => _registry.GetPlugins<T>();

        /// <summary>
        /// Log info
        /// </summary>
        /// <param name="m"></param>
        public void LogInfo(string m) => Console.WriteLine($"[INFO] {m}");

        /// <summary>
        /// Log warning
        /// </summary>
        /// <param name="m"></param>
        public void LogWarning(string m) => Console.WriteLine($"[WARN] {m}");

        /// <summary>
        /// Log error
        /// </summary>
        /// <param name="m"></param>
        /// <param name="e"></param>
        public void LogError(string m, Exception? e) => Console.WriteLine($"[ERR] {m} {e}");

        /// <summary>
        /// Log debug
        /// </summary>
        /// <param name="m"></param>
        public void LogDebug(string m) { }

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
            public string Id => "unsafe-acl";
            public string Name => "Unsafe ACL";
            public string Version => "0.0";
            public void Initialize(IKernelContext c) { }
            public static Task StartAsync(CancellationToken c) => Task.CompletedTask;
            public static Task StopAsync() => Task.CompletedTask;
            public void CreateScope(string r, string o) { }
            public void SetPermissions(string r, string s, Permission a, Permission d) { }
            public bool HasAccess(string r, string s, Permission p) => true;
        }

        private class PassiveSentinelFallback : INeuralSentinel
        {
            public string Id => "passive-sentinel";
            public string Name => "Passive Sentinel";
            public string Version => "0.0";
            public void Initialize(IKernelContext c) { }
            public static Task StartAsync(CancellationToken c) => Task.CompletedTask;
            public static Task StopAsync() => Task.CompletedTask;
            public Task<GovernanceJudgment> EvaluateAsync(SentinelContext c)
                => Task.FromResult(new GovernanceJudgment { InterventionRequired = false });
        }
    }
}