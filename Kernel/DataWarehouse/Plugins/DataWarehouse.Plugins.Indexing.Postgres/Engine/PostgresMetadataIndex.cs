using DataWarehouse.SDK.Contracts;
using DataWarehouse.SDK.Primitives;
using Npgsql;
using System.Data;
using System.Runtime.CompilerServices;
using System.Text.Json;

namespace DataWarehouse.Plugins.Indexing.Postgres.Engine
{
    /// <summary>
    /// A high-performance, concurrent Metadata Index backed by PostgreSQL.
    /// Supports JSONB querying and Vector Similarity Search (via pgvector).
    /// </summary>
    public class PostgresMetadataIndex : IAsyncDisposable
    {
        private readonly NpgsqlDataSource _dataSource;
        private readonly IKernelContext _context;

        /// <summary>
        /// Initializes the Postgres Engine with a connection string.
        /// </summary>
        public PostgresMetadataIndex(string connectionString, IKernelContext context)
        {
            _context = context;
            var builder = new NpgsqlDataSourceBuilder(connectionString);
            _dataSource = builder.Build();
        }

        /// <summary>
        /// Idempotent Schema Migration.
        /// Creates tables and indexes if they do not exist.
        /// </summary>
        public void InitializeSchema()
        {
            using var conn = _dataSource.OpenConnection();
            using var cmd = conn.CreateCommand();

            cmd.CommandText = @"
                -- 1. Main Table
                CREATE TABLE IF NOT EXISTS Manifests (
                    Id TEXT PRIMARY KEY,
                    ContainerId TEXT NOT NULL,
                    BlobUri TEXT NOT NULL,
                    OwnerId TEXT,
                    SizeBytes BIGINT,
                    JsonData JSONB NOT NULL,
                    LastAccessed BIGINT,
                    VectorData VECTOR(384) -- Attempt to use pgvector
                );

                -- 2. Indexes for Performance
                CREATE INDEX IF NOT EXISTS idx_manifests_container ON Manifests(ContainerId);
                CREATE INDEX IF NOT EXISTS idx_manifests_json ON Manifests USING GIN (JsonData);

                -- 3. Try create Vector Index (Graceful failure if extension missing)
                DO $$
                BEGIN
                    IF EXISTS (SELECT 1 FROM pg_extension WHERE extname = 'vector') THEN
                        CREATE INDEX IF NOT EXISTS idx_manifests_vector ON Manifests USING ivfflat (VectorData vector_cosine_ops) WITH (lists = 100);
                    END IF;
                EXCEPTION WHEN OTHERS THEN
                    RAISE NOTICE 'pgvector extension not available. Vector search will be slow/disabled.';
                END
                $$;
            ";

            try
            {
                cmd.ExecuteNonQuery();
            }
            catch (PostgresException ex)
            {
                // Fallback: If vector type fails, creates table without vector column
                if (ex.SqlState == "42704") // Undefined object (VECTOR type missing)
                {
                    _context.LogWarning("[Postgres] pgvector not installed. Falling back to standard schema.");
                    cmd.CommandText = @"
                        CREATE TABLE IF NOT EXISTS Manifests (
                            Id TEXT PRIMARY KEY,
                            ContainerId TEXT NOT NULL,
                            BlobUri TEXT NOT NULL,
                            OwnerId TEXT,
                            SizeBytes BIGINT,
                            JsonData JSONB NOT NULL,
                            LastAccessed BIGINT
                        );
                        CREATE INDEX IF NOT EXISTS idx_manifests_container ON Manifests(ContainerId);
                        CREATE INDEX IF NOT EXISTS idx_manifests_json ON Manifests USING GIN (JsonData);
                    ";
                    cmd.ExecuteNonQuery();
                }
                else
                {
                    throw;
                }
            }
        }

        /// <summary>
        /// Upserts a manifest into the database.
        /// </summary>
        public async Task IndexManifestAsync(Manifest manifest)
        {
            string json = JsonSerializer.Serialize(manifest);

            using var cmd = _dataSource.CreateCommand(@"
                INSERT INTO Manifests (Id, ContainerId, BlobUri, OwnerId, SizeBytes, JsonData, LastAccessed)
                VALUES (@id, @cont, @uri, @owner, @size, @json::jsonb, @access)
                ON CONFLICT (Id) DO UPDATE SET
                    ContainerId = EXCLUDED.ContainerId,
                    JsonData = EXCLUDED.JsonData,
                    LastAccessed = EXCLUDED.LastAccessed;
            ");

            cmd.Parameters.AddWithValue("id", manifest.Id);
            cmd.Parameters.AddWithValue("cont", manifest.ContainerId);
            cmd.Parameters.AddWithValue("uri", manifest.BlobUri);
            cmd.Parameters.AddWithValue("owner", manifest.OwnerId ?? (object)DBNull.Value);
            cmd.Parameters.AddWithValue("size", manifest.SizeBytes);
            cmd.Parameters.AddWithValue("json", json);
            cmd.Parameters.AddWithValue("access", manifest.LastAccessedAt);

            await cmd.ExecuteNonQueryAsync();
        }

        /// <summary>
        /// Retrieves a single manifest by ID.
        /// </summary>
        public async Task<Manifest?> GetManifestAsync(string id)
        {
            using var cmd = _dataSource.CreateCommand("SELECT JsonData FROM Manifests WHERE Id = @id");
            cmd.Parameters.AddWithValue("id", id);

            var json = (string?)await cmd.ExecuteScalarAsync();
            return json != null ? JsonSerializer.Deserialize<Manifest>(json) : null;
        }

        /// <summary>
        /// Executes a keyword or vector search.
        /// </summary>
        public async Task<string[]> SearchAsync(string query, float[]? vector, int limit)
        {
            using var cmd = _dataSource.CreateCommand();

            if (vector != null && vector.Length > 0)
            {
                // Hybrid Search: JSON contains query AND Vector Similarity
                // Note: Requires pgvector. Assuming existence or we catch exception.
                cmd.CommandText = @"
                    SELECT Id FROM Manifests 
                    WHERE JsonData::text ILIKE @q 
                    ORDER BY VectorData <=> @vec 
                    LIMIT @limit";

                cmd.Parameters.AddWithValue("q", $"%{query}%");
                cmd.Parameters.AddWithValue("vec", vector); // Npgsql supports float[] mapping to vector
            }
            else
            {
                // Standard Text Search
                cmd.CommandText = @"
                    SELECT Id FROM Manifests 
                    WHERE JsonData::text ILIKE @q 
                    LIMIT @limit";
                cmd.Parameters.AddWithValue("q", $"%{query}%");
            }

            cmd.Parameters.AddWithValue("limit", limit);

            var results = new List<string>();
            try
            {
                using var reader = await cmd.ExecuteReaderAsync();
                while (await reader.ReadAsync())
                {
                    results.Add(reader.GetString(0));
                }
            }
            catch (Exception ex)
            {
                _context.LogError("[Postgres] Search failed (Possible missing pgvector?)", ex);
                return [];
            }

            return [.. results];
        }

        /// <summary>
        /// Enumerates all records for maintenance.
        /// </summary>
        public async IAsyncEnumerable<Manifest> EnumerateAllAsync([EnumeratorCancellation] CancellationToken ct = default)
        {
            using var conn = await _dataSource.OpenConnectionAsync(ct);
            using var cmd = conn.CreateCommand();
            cmd.CommandText = "SELECT JsonData FROM Manifests";

            // Using unbuffered reader for memory efficiency on huge datasets
            using var reader = await cmd.ExecuteReaderAsync(CommandBehavior.SequentialAccess, ct);
            while (await reader.ReadAsync(ct))
            {
                var json = reader.GetString(0);
                var manifest = JsonSerializer.Deserialize<Manifest>(json);
                if (manifest != null) yield return manifest;
            }
        }

        /// <summary>
        /// Updates the LastAccessed timestamp (Fast Path).
        /// </summary>
        public async Task UpdateLastAccessAsync(string id, long timestamp)
        {
            using var cmd = _dataSource.CreateCommand("UPDATE Manifests SET LastAccessed = @ts WHERE Id = @id");
            cmd.Parameters.AddWithValue("ts", timestamp);
            cmd.Parameters.AddWithValue("id", id);
            await cmd.ExecuteNonQueryAsync();
        }

        /// <summary>
        /// Executes a raw SQL query (for internal tools).
        /// </summary>
        public async Task<string[]> ExecuteQueryAsync(string query, int limit)
        {
            // WARNING: This assumes the query is pre-sanitized or generated by the internal Parser.
            // Direct SQL injection risk if exposed to raw user input without the Parser wrapper.
            using var cmd = _dataSource.CreateCommand();
            cmd.CommandText = query; // We assume the Parser constructs valid SQL

            // Safety Limit
            if (!query.Contains("LIMIT", StringComparison.OrdinalIgnoreCase))
            {
                cmd.CommandText += $" LIMIT {limit}";
            }

            var results = new List<string>();
            using var reader = await cmd.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                // We return the first column (usually ID or JSON)
                results.Add(reader.GetString(0));
            }
            return [.. results];
        }

        public Task<string[]> ExecuteQueryAsync(CompositeQuery query, int limit)
        {
            // 1. Convert CompositeQuery to SQL WHERE clause
            var sb = new System.Text.StringBuilder();
            sb.Append("SELECT JsonData FROM Manifests");

            if (query.Filters.Count > 0)
            {
                sb.Append(" WHERE ");
                for (int i = 0; i < query.Filters.Count; i++)
                {
                    var f = query.Filters[i];
                    if (i > 0) sb.Append(" AND ");

                    // Simple SQL Injection protection: Parameterize in production!
                    // Here we map fields to JSONB paths
                    string col = MapField(f.Field);
                    string op = MapOperator(f.Operator);

                    // Basic numeric vs string handling
                    if (decimal.TryParse(f.Value?.ToString(), out _))
                        sb.Append($"({col})::numeric {op} {f.Value}");
                    else
                        sb.Append($"{col} {op} '{f.Value}'");
                }
            }

            sb.Append($" LIMIT {limit}");

            // 2. Execute raw string
            return ExecuteQueryAsync(sb.ToString(), limit);
        }

        private static string MapField(string field)
        {
            return field switch
            {
                "Id" => "Id",
                "ContainerId" => "ContainerId",
                "SizeBytes" => "SizeBytes",
                _ => $"JsonData->>'{field}'"
            };
        }

        private static string MapOperator(string op)
        {
            return op switch { "==" => "=", "!=" => "<>", _ => op };
        }

        public async ValueTask DisposeAsync()
        {
            await _dataSource.DisposeAsync();
            GC.SuppressFinalize(this);
        }
    }
}