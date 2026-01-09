using DataWarehouse.Plugins.Indexing.Sqlite.Engine;
using DataWarehouse.SDK.Contracts;
using DataWarehouse.SDK.Primitives;

namespace DataWarehouse.Plugins.Indexing.Sqlite.Bootstrapper
{
    /// <summary>
    /// The Plugin Entry Point.
    /// Acts as a FACADE, exposing the internal SQLite Engine as the system's IMetadataIndex.
    /// </summary>
    public class SqliteIndexingPlugin : IFeaturePlugin, IMetadataIndex
    {
        public string Id => "DataWarehouse.Indexing.Sqlite";
        public string Version => "5.0.0";
        public string Name => "SQLite Metadata Index";

        private SqliteMetadataIndex? _engine;
        private IKernelContext? _context;

        /// <summary>
        /// Handshake protocol handler
        /// </summary>
        public Task<HandshakeResponse> OnHandshakeAsync(HandshakeRequest request)
        {
            _context = request as IKernelContext;
            string dbPath = Path.Combine(_context?.RootPath ?? "", "Index", "metadata.db");
            Directory.CreateDirectory(Path.GetDirectoryName(dbPath)!);

            _engine = new SqliteMetadataIndex(dbPath);
            _context?.LogInfo($"[{Id}] SQLite Index initialized at {dbPath}");

            return Task.FromResult(HandshakeResponse.Success(
                pluginId: Id,
                name: Name,
                version: new Version(Version),
                category: PluginCategory.Feature
            ));
        }

        /// <summary>
        /// Message handler (optional).
        /// </summary>
        public Task OnMessageAsync(PluginMessage message)
        {
            return Task.CompletedTask;
        }

        public void Initialize(IKernelContext context)
        {
            _context = context;
            string dbPath = Path.Combine(context.RootPath, "Index", "metadata.db");
            Directory.CreateDirectory(Path.GetDirectoryName(dbPath)!);

            _engine = new SqliteMetadataIndex(dbPath);
            context.LogInfo($"[{Id}] SQLite Index initialized at {dbPath}");
        }

        public Task StartAsync(CancellationToken ct) => Task.CompletedTask;

        public Task StopAsync()
        {
            _engine?.Dispose();
            return Task.CompletedTask;
        }

        // --- IMetadataIndex Implementation (Forwarding) ---

        /// <summary>
        /// Index manifest
        /// </summary>
        /// <param name="manifest"></param>
        /// <returns></returns>
        public Task IndexManifestAsync(Manifest manifest)
        {
            EnsureEngine();
            return _engine!.IndexManifestAsync(manifest);
        }

        /// <summary>
        /// Get manifest
        /// </summary>
        /// <param name="id"></param>
        /// <returns></returns>
        public Task<Manifest?> GetManifestAsync(string id)
        {
            EnsureEngine();
            return _engine!.GetManifestAsync(id);
        }

        /// <summary>
        /// Enumerate all
        /// </summary>
        /// <param name="ct"></param>
        /// <returns></returns>
        public IAsyncEnumerable<Manifest> EnumerateAllAsync(CancellationToken ct = default)
        {
            EnsureEngine();
            return _engine!.EnumerateAllAsync(ct);
        }

        /// <summary>
        /// Execute query
        /// </summary>
        /// <param name="query"></param>
        /// <param name="limit"></param>
        /// <returns></returns>
        public Task<string[]> ExecuteQueryAsync(string query, int limit)
        {
            EnsureEngine();
            // Forward to the String-based overload in the Engine
            return _engine!.ExecuteQueryAsync(query, limit);
        }

        /// <summary>
        /// Execute query
        /// </summary>
        /// <param name="query"></param>
        /// <param name="limit"></param>
        /// <returns></returns>
        public Task<string[]> ExecuteQueryAsync(CompositeQuery query, int limit = 50)
        {
            return _engine!.ExecuteQueryAsync(query, limit);
        }

        /// <summary>
        /// Search
        /// </summary>
        /// <param name="query"></param>
        /// <param name="vector"></param>
        /// <param name="limit"></param>
        /// <returns></returns>
        public Task<string[]> SearchAsync(string query, float[]? vector, int limit)
        {
            EnsureEngine();
            return _engine!.SearchAsync(query, vector, limit);
        }

        /// <summary>
        /// Update access
        /// </summary>
        /// <param name="id"></param>
        /// <param name="timestamp"></param>
        /// <returns></returns>
        public Task UpdateLastAccessAsync(string id, long timestamp)
        {
            EnsureEngine();
            return _engine!.UpdateLastAccessAsync(id, timestamp);
        }

        // --- Helpers ---
        private void EnsureEngine()
        {
            if (_engine == null) throw new InvalidOperationException("SQLite Plugin not initialized.");
        }
    }
}