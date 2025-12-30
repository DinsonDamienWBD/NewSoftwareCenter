using Core.AI;
using Core.Infrastructure;
using DataWarehouse.Configuration;
using DataWarehouse.Contracts;
using DataWarehouse.Diagnostics;
using DataWarehouse.Fabric;
using DataWarehouse.IO;
using DataWarehouse.Primitives; // For Manifest only
using DataWarehouse.Realtime;
using DataWarehouse.Security;
using Microsoft.Extensions.Configuration; // For IConfiguration
using Microsoft.Extensions.Logging;
using System.IO.Compression;
using System.Text;

namespace DataWarehouse.Engine
{
    /// <summary>
    /// The main entry point for DW
    /// </summary>
    public class CosmicWarehouse : IDataWarehouse, ISemanticMemory, IAgentStorageTools
    {
        internal readonly PluginRegistry _registry;
        internal readonly FeatureManager _features;

        private readonly PipelineOptimizer _pipelineOptimizer;
        private readonly RuntimeOptimizer _runtimeOptimizer;
        private readonly IKeyStore _keyStore;
        private readonly ILogger _logger;
        private readonly IMetadataIndex _index;

        // Optional Plugins
        private readonly FederationManager? _federation;
        private readonly UnifiedStoragePool? _pool;
        private readonly DeduplicationTable? _dedupe;
        private readonly WormGovernor _worm;

        private readonly InMemoryRealTimeProvider _bus;
        private readonly FlightRecorder _recorder;
        private readonly IMetricsProvider _metrics;
        private readonly string _rootPath;

        // Cache
        private readonly System.Collections.Concurrent.ConcurrentDictionary<string, byte[]> _keyCache = new();

        // --- CONSTRUCTOR FIX: Added ILoggerFactory ---

        /// <summary>
        /// Constructor
        /// </summary>
        /// <param name="registry"></param>
        /// <param name="features"></param>
        /// <param name="logger"></param>
        /// <param name="loggerFactory"></param>
        /// <param name="keyStore"></param>
        /// <param name="index"></param>
        /// <param name="metrics"></param>
        /// <param name="runtimeOptimizer"></param>
        /// <param name="pipelineOptimizer"></param>
        /// <param name="rootPath"></param>
        public CosmicWarehouse(
            PluginRegistry registry,
            FeatureManager features,
            ILogger<CosmicWarehouse> logger,
            ILoggerFactory loggerFactory, // <--- NEW ARGUMENT (10th)
            IKeyStore keyStore,
            IMetadataIndex index,
            IMetricsProvider metrics,
            RuntimeOptimizer runtimeOptimizer,
            PipelineOptimizer pipelineOptimizer,
            string rootPath
            )
        {
            _rootPath = rootPath;
            _registry = registry;
            _features = features;
            _logger = logger;
            _keyStore = keyStore;
            _index = index;
            _metrics = metrics;
            _runtimeOptimizer = runtimeOptimizer;
            _pipelineOptimizer = pipelineOptimizer;

            var metadataPath = Path.Combine(rootPath, "Metadata");
            var dataPath = Path.Combine(rootPath, "Data");
            Directory.CreateDirectory(metadataPath);
            Directory.CreateDirectory(dataPath);

            _recorder = new FlightRecorder(dataPath);
            _worm = new WormGovernor(metadataPath);
            _bus = new InMemoryRealTimeProvider();

            _pool = registry.GetPlugin<UnifiedStoragePool>();
            _federation = registry.GetPlugin<FederationManager>();

            // FIX: Pass loggerFactory to FederationManager fallback
            _federation ??= new FederationManager(metadataPath, loggerFactory);
            _pool ??= new UnifiedStoragePool(logger);

            if (_runtimeOptimizer.ShouldEnableDeduplication())
            {
                _dedupe = new DeduplicationTable(metadataPath);
            }
        }

        /// <summary>
        /// Safley dispose
        /// </summary>
        public void Dispose()
        {
            _worm.Dispose();
            _dedupe?.Dispose();
            _recorder.Dispose();
        }

        // --- IDataWarehouse Lifecycle Stubs ---

        /// <summary>
        /// Configure DW
        /// </summary>
        /// <param name="config"></param>
        public void Configure(IConfiguration config) { }

        /// <summary>
        /// Mount DW
        /// </summary>
        /// <returns></returns>
        public Task MountAsync() => Task.CompletedTask;

        /// <summary>
        /// Dismount DW
        /// </summary>
        /// <returns></returns>
        public Task DismountAsync() { Dispose(); return Task.CompletedTask; }

        // --- Explicit Interface Implementation for Core Type Mapping ---

        /// <summary>
        /// Store object
        /// </summary>
        /// <param name="bucket"></param>
        /// <param name="key"></param>
        /// <param name="data"></param>
        /// <param name="coreIntent"></param>
        /// <returns></returns>
        async Task IDataWarehouse.StoreObjectAsync(string bucket, string key, Stream data, Core.Data.StorageIntent coreIntent)
        {
            // Map Core.Data.StorageIntent -> DataWarehouse.StorageIntent
            var intent = new StorageIntent(
                (SecurityLevel)(int)coreIntent.Security,
                (CompressionLevel)(int)coreIntent.Compression,
                (AvailabilityLevel)(int)coreIntent.Availability
            );

            await StoreObjectAsync(bucket, key, data, intent);
        }

        /// <summary>
        /// Health check
        /// </summary>
        /// <exception cref="InvalidOperationException"></exception>
        public void CheckHealth()
        {
            if (!_registry.CryptoAlgos.Any()) throw new InvalidOperationException("No Crypto Plugins");
        }

        // --- PUBLIC API ---

        /// <summary>
        /// Attach DW
        /// </summary>
        /// <param name="provider"></param>
        /// <param name="tier"></param>
        public void AttachStorage(IStorageProvider provider, StorageTier tier)
            => _pool?.AttachNode(provider, tier);

        /// <summary>
        /// Link resource
        /// </summary>
        /// <param name="alias"></param>
        /// <param name="connectionString"></param>
        public void LinkResource(string alias, string connectionString)
            => _federation?.MountRemote(alias, connectionString);

        /// <summary>
        /// Unlink resource
        /// </summary>
        /// <param name="alias"></param>
        public void UnlinkResource(string alias)
            => _federation?.Unmount(alias);
        
        /// <summary>
        /// Get list of all linked resources
        /// </summary>
        /// <returns></returns>
        public IEnumerable<KeyValuePair<string, string>> GetLinkedResources()
            => _federation?.GetLinks() ?? [];

        /// <summary>
        /// Store object
        /// Uses DataWarehouse.StorageIntent (Native)
        /// </summary>
        /// <param name="bucket"></param>
        /// <param name="key"></param>
        /// <param name="data"></param>
        /// <param name="intent"></param>
        /// <param name="meta"></param>
        /// <param name="expectedETag"></param>
        /// <returns></returns>
        /// <exception cref="InvalidOperationException"></exception>
        public async Task StoreObjectAsync(string bucket, string key, Stream data, StorageIntent intent, Manifest? meta = null, string? expectedETag = null)
        {
            using var timer = _metrics.TrackDuration("dw_write_latency");
            _recorder.Record("WRITE_START", $"{bucket}/{key} ({data.Length} bytes)");

            try
            {
                // 1. FEDERATION
                if (_federation != null && _features.IsEnabled("Federation") && _federation.IsRemote(bucket))
                {
                    await _federation.GetProvider(bucket).SaveAsync(new Uri($"net://{bucket}/{key}"), data);
                    return;
                }

                // 2. CONCURRENCY
                var manifestUri = new Uri($"file:///{bucket}/{key}.manifest");
                if (expectedETag != null)
                {
                    var existing = await LoadManifestInternalAsync(manifestUri);
                    if (existing != null && existing.ETag != expectedETag)
                        throw new InvalidOperationException($"Concurrency Conflict");
                }

                // 3. DEDUPLICATION
                string contentHash = CalculateHash(data);
                bool skipDedupe = !_runtimeOptimizer.ShouldEnableDeduplication() || intent.Compression == CompressionLevel.Fast;

                if (!skipDedupe && _dedupe != null && _features.IsEnabled("Dedupe"))
                {
                    if (_dedupe.TryGetExisting(contentHash, out var existingBlobUri))
                    {
                        var dedupeManifest = CreateManifest(bucket, key, existingBlobUri, contentHash, data.Length, intent);
                        if (meta != null) { dedupeManifest.ContentSummary = meta.ContentSummary; dedupeManifest.Tags = meta.Tags; }
                        await SaveManifestAsync(dedupeManifest);
                        _metrics.IncrementCounter("dw_dedupe_hit");
                        return;
                    }
                }

                // 4. PIPELINE
                var pipelineConfig = _pipelineOptimizer.Resolve(intent);
                pipelineConfig.KeyId = await _keyStore.GetCurrentKeyIdAsync();

                // 5. WRITE
                string blobUri;
                if (data.CanSeek) data.Position = 0;

                using (var pipelineStream = BuildWritePipeline(data, pipelineConfig))
                {
                    if (_pool != null && _features.IsEnabled("Fabric"))
                    {
                        blobUri = (await _pool.WriteBlobAsync(pipelineStream, intent)).ToString();
                    }
                    else
                    {
                        var provider = _registry.GetStorage("file");
                        blobUri = $"file:///{bucket}/{key}.blob";
                        await provider.SaveAsync(new Uri(blobUri), pipelineStream);
                    }
                }

                // 6. FINALIZE
                if (!skipDedupe && _dedupe != null) _dedupe.Register(contentHash, blobUri);

                if (intent.Security == SecurityLevel.High)
                    _worm.LockBlob(blobUri, TimeSpan.FromDays(365 * 7));

                var newManifest = CreateManifest(bucket, key, blobUri, contentHash, data.Length, intent, pipelineConfig);
                if (meta != null) { newManifest.ContentSummary = meta.ContentSummary; newManifest.Tags = meta.Tags; }

                await SaveManifestAsync(newManifest);
                await _index.IndexManifestAsync(newManifest);

                _metrics.IncrementCounter("dw_writes_success");
                _recorder.Record("WRITE_SUCCESS", $"{bucket}/{key}");
                await _bus.PublishAsync(new StorageEvent(blobUri, newManifest.ETag, "UPDATE", DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()));
            }
            catch (Exception ex)
            {
                _metrics.IncrementCounter("dw_writes_failed");
                _recorder.Record("WRITE_FAIL", $"{ex.Message}");
                throw;
            }
        }

        /// <summary>
        /// Retrieve object from storage
        /// </summary>
        /// <param name="bucket"></param>
        /// <param name="key"></param>
        /// <returns></returns>
        /// <exception cref="RemoteResourceUnavailableException"></exception>
        /// <exception cref="FileNotFoundException"></exception>
        public async Task<Stream> RetrieveObjectAsync(string bucket, string key)
        {
            var uri = new Uri($"file:///{bucket}/{key}");

            if (_federation != null && _federation.Resolve(uri) is IStorageProvider remote)
            {
                try { return await remote.LoadAsync(uri); }
                catch (IOException ex) when (ex.Message.Contains("OFFLINE"))
                {
                    throw new RemoteResourceUnavailableException($"Node offline: {bucket}", ex);
                }
            }

            var manifestUri = new Uri($"file:///{bucket}/{key}.manifest");
            var manifest = await LoadManifestInternalAsync(manifestUri) ?? throw new FileNotFoundException($"Object {bucket}/{key} not found");
            IStorageProvider provider = _registry.GetStorage("file");
            var stream = await provider.LoadAsync(new Uri(manifest.BlobUri));

            return BuildReadPipeline(stream, manifest.Pipeline);
        }

        // --- HELPERS ---

        private static string CalculateHash(Stream data)
        {
            if (!data.CanSeek) return "unavailable";
            using var sha = System.Security.Cryptography.SHA256.Create();
            var hashBytes = sha.ComputeHash(data);
            data.Position = 0;
            return Convert.ToHexString(hashBytes);
        }

        private async Task SaveManifestAsync(Manifest m)
        {
            var provider = _registry.GetStorage("file");
            var json = System.Text.Json.JsonSerializer.Serialize(m);
            using var ms = new MemoryStream(Encoding.UTF8.GetBytes(json));
            var manifestUri = m.BlobUri.Replace(".blob", ".manifest");
            await provider.SaveAsync(new Uri(manifestUri), ms);
        }

        private async Task<Manifest?> LoadManifestInternalAsync(Uri uri)
        {
            try
            {
                var provider = _registry.GetStorage("file");
                if (!await provider.ExistsAsync(uri)) return null;
                using var ms = await provider.LoadAsync(uri);
                return await System.Text.Json.JsonSerializer.DeserializeAsync<Manifest>(ms);
            }
            catch { return null; }
        }

        private static Manifest CreateManifest(string bucket, string key, string blobUri, string hash, long size, StorageIntent intent, PipelineConfig? pipe = null)
        {
            var m = new Manifest
            {
                BlobUri = blobUri,
                Checksum = hash,
                SizeBytes = size,
                Pipeline = pipe ?? new PipelineConfig(),
                ETag = Guid.NewGuid().ToString("N")
            };

            // Usage of parameters
            m.Tags["Bucket"] = bucket;
            m.Tags["Key"] = key;
            m.Tags["Security"] = intent.Security.ToString();

            return m;
        }

        // --- AI & QUERY ---

        /// <summary>
        /// Store memory
        /// </summary>
        /// <param name="c"></param>
        /// <param name="t"></param>
        /// <param name="s"></param>
        /// <returns></returns>
        public Task<string> StoreMemoryAsync(string c, string[] t, string s) => MemorizeAsync(c, t, s);

        /// <summary>
        /// Recall memory
        /// </summary>
        /// <param name="id"></param>
        /// <returns></returns>
        public Task<string> RecallMemoryAsync(string id) => RecallAsync(id);

        /// <summary>
        /// Memorize
        /// </summary>
        /// <param name="content"></param>
        /// <param name="tags"></param>
        /// <param name="summary"></param>
        /// <returns></returns>
        public async Task<string> MemorizeAsync(string content, string[] tags, string? summary = null)
        {
            using var ms = new MemoryStream(Encoding.UTF8.GetBytes(content));
            var intent = new StorageIntent();
            var meta = new Manifest { ContentSummary = summary, Tags = tags.ToDictionary(k => k, v => "true") };
            var id = Guid.NewGuid().ToString("N");
            await StoreObjectAsync("memories", id, ms, intent, meta);
            return id;
        }

        /// <summary>
        /// Recall async
        /// </summary>
        /// <param name="id"></param>
        /// <returns></returns>
        public async Task<string> RecallAsync(string id)
        {
            using var stream = await RetrieveObjectAsync("memories", id);
            using var reader = new StreamReader(stream);
            return await reader.ReadToEndAsync();
        }

        /// <summary>
        /// Search memory
        /// </summary>
        /// <param name="q"></param>
        /// <param name="v"></param>
        /// <param name="l"></param>
        /// <returns></returns>
        public Task<string[]> SearchMemoriesAsync(string q, float[]? v, int l) => _index.SearchAsync(q, v, l);

        /// <summary>
        /// Search memory
        /// </summary>
        /// <param name="v"></param>
        /// <param name="l"></param>
        /// <returns></returns>
        public Task<string[]> SearchMemoriesAsync(float[] v, int l) => _index.SearchAsync("", v, l);

        /// <summary>
        /// Query file
        /// </summary>
        /// <param name="sql"></param>
        /// <returns></returns>
        /// <exception cref="NotSupportedException"></exception>
        public async Task<string[]> QueryFilesAsync(string sql)
        {
            if (_index is IQueryableIndex q) return await q.ExecuteSqlAsync(sql);
            throw new NotSupportedException("SQL not supported");
        }

        /// <summary>
        /// Query async
        /// </summary>
        /// <param name="q"></param>
        /// <returns></returns>
        public async Task<string[]> QueryAsync(CompositeQuery q)
        {
            if (_index is IQueryableIndex qi) return await qi.ExecuteQueryAsync(q);
            return [];
        }

        /// <summary>
        /// Query SQL async
        /// </summary>
        /// <param name="sql"></param>
        /// <returns></returns>
        public async Task<string[]> QuerySqlAsync(string sql) => await QueryFilesAsync(sql);

        // --- PIPELINE IMPL ---

        private Stream BuildWritePipeline(Stream input, PipelineConfig config)
        {
            Stream current = input;
            if (config.CompressionAlgo == "GZip")
            {
                var ms = new MemoryStream();
                using (var gzip = new System.IO.Compression.GZipStream(ms, System.IO.Compression.CompressionLevel.Optimal, true))
                {
                    input.CopyTo(gzip);
                }
                ms.Position = 0;
                current = ms;
            }
            if (config.CryptoAlgo != "None")
            {
                var crypto = _registry.GetCrypto(config.CryptoAlgo);
                var keyBytes = _keyCache.GetOrAdd(config.KeyId, id => _keyStore.GetKey(id));
                var ms = new MemoryStream();
                using (var enc = new DataWarehouse.IO.ChunkedEncryptionStream(ms, crypto, keyBytes))
                {
                    current.CopyToAsync(enc).Wait();
                }
                ms.Position = 0;
                current = ms;
            }
            return current;
        }

        private Stream BuildReadPipeline(Stream input, PipelineConfig config)
        {
            Stream current = input;

            // FIX: IDE0059 - Remove redundant check for "None" return
            // Flow: Storage -> Decrypt -> Decompress -> Output

            // 1. Decryption (Reverse of Encryption)
            if (config.CryptoAlgo != "None")
            {
                var crypto = _registry.GetCrypto(config.CryptoAlgo);
                var keyBytes = _keyCache.GetOrAdd(config.KeyId, id => _keyStore.GetKey(id));
                // Wraps the raw file stream to decrypt on the fly
                current = new ChunkedDecryptionStream(current, crypto, keyBytes);
            }

            // 2. Decompression (Reverse of Compression)
            if (config.CompressionAlgo == "GZip")
            {
                current = new GZipStream(current, CompressionMode.Decompress);
            }

            return current;
        }
    }
}