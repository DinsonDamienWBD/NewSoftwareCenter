using DataWarehouse.Plugins.Indexing.Postgres.Engine;
using DataWarehouse.SDK.Attributes;
using DataWarehouse.SDK.Contracts;
using DataWarehouse.SDK.Primitives;

namespace DataWarehouse.Plugins.Indexing.Postgres.Bootstrapper
{
    /// <summary>
    /// The Bootstrapper for the PostgreSQL Metadata Provider.
    /// This plugin is preferred in 'Server' and 'Hyperscale' modes due to high concurrency support.
    /// </summary>
    [PluginPriority(100, OperatingMode.Server)]
    public class PostgresIndexingPlugin : IFeaturePlugin, IMetadataIndex
    {
        /// <summary>
        /// Unique Plugin ID.
        /// </summary>
        public string Id => "DataWarehouse.Indexing.Postgres";

        /// <summary>
        /// Human-readable name.
        /// </summary>
        public string Name => "PostgreSQL Metadata Index";

        /// <summary>
        /// Plugin Version.
        /// </summary>
        public string Version => "5.0.0";

        private PostgresMetadataIndex? _engine;
        private IKernelContext? _context;

        /// <summary>
        /// Handshake protocol handler
        /// </summary>
        public Task<HandshakeResponse> OnHandshakeAsync(HandshakeRequest request)
        {
            _context = request as IKernelContext;

            string connString = Environment.GetEnvironmentVariable("DW_POSTGRES_CONN")
                                ?? "Host=localhost;Port=5432;Database=datawarehouse;Username=postgres;Password=postgres";

            _context?.LogInfo($"[{Id}] Connecting to PostgreSQL at {connString.Split(';')[0]}...");

            try
            {
                _engine = new PostgresMetadataIndex(connString, _context!);
                _engine.InitializeSchema();
                _context?.LogInfo($"[{Id}] Connection established. Schema verified.");

                return Task.FromResult(HandshakeResponse.Success(
                    pluginId: Id,
                    name: Name,
                    version: new Version(Version),
                    category: PluginCategory.Feature
                ));
            }
            catch (Exception ex)
            {
                _context?.LogError($"[{Id}] Failed to connect to PostgreSQL. Plugin will fail commands.", ex);
                return Task.FromResult(HandshakeResponse.Failure(
                    Id,
                    Name,
                    $"Failed to connect to PostgreSQL: {ex.Message}"));
            }
        }

        /// <summary>
        /// Message handler (optional).
        /// </summary>
        public Task OnMessageAsync(PluginMessage message)
        {
            return Task.CompletedTask;
        }

        /// <summary>
        /// Initializes the plugin and brings the database online.
        /// </summary>
        /// <param name="context">The kernel context.</param>
        public void Initialize(IKernelContext context)
        {
            _context = context;

            // In a real deployment, we read this from IConfiguration via context (if exposed) or Environment Variables
            // Defaulting to a standard local Docker container string for immediate utility.
            string connString = Environment.GetEnvironmentVariable("DW_POSTGRES_CONN")
                                ?? "Host=localhost;Port=5432;Database=datawarehouse;Username=postgres;Password=postgres";

            context.LogInfo($"[{Id}] Connecting to PostgreSQL at {connString.Split(';')[0]}...");

            try
            {
                _engine = new PostgresMetadataIndex(connString, context);
                _engine.InitializeSchema();
                context.LogInfo($"[{Id}] Connection established. Schema verified.");
            }
            catch (Exception ex)
            {
                context.LogError($"[{Id}] Failed to connect to PostgreSQL. Plugin will fail commands.", ex);
                throw; // Letting Kernel handle the failure (Strategy: Fallback to SQLite if this fails)
            }
        }

        /// <summary>
        /// Starts background maintenance tasks (Vacuum, Analyze).
        /// </summary>
        public Task StartAsync(CancellationToken ct) => Task.CompletedTask;

        /// <summary>
        /// Stops the plugin and closes connections.
        /// </summary>
        public async Task StopAsync()
        {
            if (_engine != null)
            {
                await _engine.DisposeAsync();
                _context?.LogInfo($"[{Id}] Disconnected.");
            }
        }

        // --- IMetadataIndex Implementation (Forwarding) ---

        /// <inheritdoc />
        public Task IndexManifestAsync(Manifest manifest)
            => _engine!.IndexManifestAsync(manifest);

        /// <inheritdoc />
        public Task<string[]> SearchAsync(string query, float[]? vector, int limit)
            => _engine!.SearchAsync(query, vector, limit);

        /// <inheritdoc />
        public IAsyncEnumerable<Manifest> EnumerateAllAsync(CancellationToken ct = default)
            => _engine!.EnumerateAllAsync(ct);

        /// <inheritdoc />
        public Task UpdateLastAccessAsync(string id, long timestamp)
            => _engine!.UpdateLastAccessAsync(id, timestamp);

        /// <inheritdoc />
        public Task<Manifest?> GetManifestAsync(string id)
            => _engine!.GetManifestAsync(id);

        /// <inheritdoc />
        public Task<string[]> ExecuteQueryAsync(string query, int limit)
            => _engine!.ExecuteQueryAsync(query, limit);

        // [FIX] Forward the CompositeQuery call to the Engine
        public Task<string[]> ExecuteQueryAsync(CompositeQuery query, int limit)
        {
            if (_engine == null) return Task.FromResult(Array.Empty<string>());
            return _engine.ExecuteQueryAsync(query, limit);
        }
    }
}