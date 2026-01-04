using DataWarehouse.SDK.Primitives;
using Microsoft.Data.Sqlite;
using System.Runtime.CompilerServices;
using System.Text.Json;

namespace DataWarehouse.Plugins.Indexing.Sqlite.Engine
{
    /// <summary>
    /// The Core Engine: SQLite-backed Metadata Storage.
    /// Handles JSON storage, FTS (Future), and high-performance querying.
    /// </summary>
    public class SqliteMetadataIndex : IDisposable
    {
        /// <summary>
        /// ID
        /// </summary>
        public static string Id => "SqliteIndex";

        /// <summary>
        /// Version
        /// </summary>
        public static string Version => "5.0.0";

        private readonly string _connectionString;

        /// <summary>
        /// Constructor
        /// </summary>
        /// <param name="dbPath"></param>
        public SqliteMetadataIndex(string dbPath)
        {
            var builder = new SqliteConnectionStringBuilder
            {
                DataSource = dbPath,
                Mode = SqliteOpenMode.ReadWriteCreate,
                Cache = SqliteCacheMode.Shared,
                Pooling = true // Enable pooling for performance
            };
            _connectionString = builder.ToString();
            InitializeDb();
        }

        private void InitializeDb()
        {
            using var conn = new SqliteConnection(_connectionString);
            conn.Open();
            var cmd = conn.CreateCommand();
            cmd.CommandText = @"
                -- Main Store
                CREATE TABLE IF NOT EXISTS Manifests (
                    Id TEXT PRIMARY KEY,
                    Json TEXT,
                    ContentSummary TEXT,
                    LastAccessedAt INTEGER DEFAULT 0
                );
                
                -- Optimization Indices
                CREATE INDEX IF NOT EXISTS idx_last_access ON Manifests(LastAccessedAt);
            ";
            cmd.ExecuteNonQuery();
        }

        /// <summary>
        /// Index manifest
        /// </summary>
        /// <param name="manifest"></param>
        /// <returns></returns>
        public async Task IndexManifestAsync(Manifest manifest)
        {
            using var conn = new SqliteConnection(_connectionString);
            await conn.OpenAsync();

            using var cmd = conn.CreateCommand();
            cmd.CommandText = @"
                INSERT INTO Manifests (Id, Json, ContentSummary, LastAccessedAt) 
                VALUES (@id, @json, @summary, @lastAccess)
                ON CONFLICT(Id) DO UPDATE SET 
                    Json = @json,
                    ContentSummary = @summary,
                    LastAccessedAt = @lastAccess;
            ";

            cmd.Parameters.AddWithValue("@id", manifest.Id);
            cmd.Parameters.AddWithValue("@json", JsonSerializer.Serialize(manifest));
            cmd.Parameters.AddWithValue("@summary", manifest.ContentSummary ?? "");
            cmd.Parameters.AddWithValue("@lastAccess", manifest.LastAccessedAt);

            await cmd.ExecuteNonQueryAsync();
        }

        /// <summary>
        /// Execute query
        /// </summary>
        /// <param name="sqlQuery"></param>
        /// <param name="limit"></param>
        /// <returns></returns>
        public async Task<string[]> ExecuteQueryAsync(string sqlQuery, int limit = 50)
        {
            // Simple wrapper allowing direct WHERE clauses or full SELECTs
            string sql = sqlQuery.Trim();

            // Safety: Ensure it's a SELECT or a partial WHERE
            if (!sql.StartsWith("SELECT", StringComparison.OrdinalIgnoreCase))
            {
                // Assume it's a condition like "json_extract(...) > 10"
                sql = $"SELECT Id FROM Manifests WHERE {sql} LIMIT {limit}";
            }

            return await RunSqlInternal(sql, []);
        }

        /// <summary>
        /// Execute query
        /// </summary>
        /// <param name="query"></param>
        /// <param name="limit"></param>
        /// <returns></returns>
        public async Task<string[]> ExecuteQueryAsync(CompositeQuery query, int limit = 50)
        {
            var whereClauses = new List<string>();
            var parameters = new List<SqliteParameter>();
            int pIndex = 0;

            foreach (var f in query.Filters)
            {
                string jsonPath = f.Field.StartsWith("Tags.")
                    ? $"$.Tags.\"{f.Field[5..]}\""
                    : $"$.{f.Field}";

                string paramName = $"@p{pIndex++}";
                string sqlOp = MapOperator(f.Operator);

                whereClauses.Add($"json_extract(Json, '{jsonPath}') {sqlOp} {paramName}");
                parameters.Add(new SqliteParameter(paramName, f.Value.ToString()));
            }

            string logic = (query.Logic == "OR") ? "OR" : "AND";
            string whereSql = whereClauses.Count > 0 ? $"WHERE {string.Join($" {logic} ", whereClauses)}" : "";

            string sql = $"SELECT Id FROM Manifests {whereSql} LIMIT {limit}";

            return await RunSqlInternal(sql, parameters);
        }

        /// <summary>
        /// Search
        /// </summary>
        /// <param name="query"></param>
        /// <param name="_"></param>
        /// <param name="limit"></param>
        /// <returns></returns>
        public async Task<string[]> SearchAsync(string query, float[]? _, int limit)
        {
            string sql = $"SELECT Id FROM Manifests WHERE ContentSummary LIKE @q LIMIT {limit}";
            return await RunSqlInternal(sql, [new SqliteParameter("@q", $"%{query}%")]);
        }

        /// <summary>
        /// Enumerate all
        /// </summary>
        /// <param name="ct"></param>
        /// <returns></returns>
        public async IAsyncEnumerable<Manifest> EnumerateAllAsync([EnumeratorCancellation] CancellationToken ct = default)
        {
            using var conn = new SqliteConnection(_connectionString);
            await conn.OpenAsync(ct);

            using var cmd = conn.CreateCommand();
            cmd.CommandText = "SELECT Json FROM Manifests";

            using var reader = await cmd.ExecuteReaderAsync(ct);
            while (await reader.ReadAsync(ct))
            {
                var json = reader.GetString(0);
                var manifest = JsonSerializer.Deserialize<Manifest>(json);
                if (manifest != null) yield return manifest;
            }
        }

        /// <summary>
        /// Update last access
        /// </summary>
        /// <param name="id"></param>
        /// <param name="timestamp"></param>
        /// <returns></returns>
        public async Task UpdateLastAccessAsync(string id, long timestamp)
        {
            using var conn = new SqliteConnection(_connectionString);
            await conn.OpenAsync();
            using var cmd = conn.CreateCommand();
            // Optimized partial update
            cmd.CommandText = "UPDATE Manifests SET Json = json_set(Json, '$.LastAccessedAt', @time), LastAccessedAt = @time WHERE Id = @id";
            cmd.Parameters.AddWithValue("@id", id);
            cmd.Parameters.AddWithValue("@time", timestamp);
            await cmd.ExecuteNonQueryAsync();
        }

        /// <summary>
        /// Get manifest
        /// </summary>
        /// <param name="id"></param>
        /// <returns></returns>
        public async Task<Manifest?> GetManifestAsync(string id)
        {
            using var conn = new SqliteConnection(_connectionString);
            await conn.OpenAsync();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = "SELECT Json FROM Manifests WHERE Id = @id";
            cmd.Parameters.AddWithValue("@id", id);

            var json = (string?)await cmd.ExecuteScalarAsync();
            return string.IsNullOrEmpty(json) ? null : JsonSerializer.Deserialize<Manifest>(json);
        }

        private async Task<string[]> RunSqlInternal(string sql, IEnumerable<SqliteParameter> parameters)
        {
            var results = new List<string>();
            using var conn = new SqliteConnection(_connectionString);
            await conn.OpenAsync();

            using var cmd = conn.CreateCommand();
            cmd.CommandText = sql;
            cmd.Parameters.AddRange(parameters);

            using var reader = await cmd.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                results.Add(reader.GetString(0));
            }
            return [.. results];
        }

        private static string MapOperator(string op)
        {
            return op switch
            {
                "==" => "=",
                "!=" => "<>",
                "CONTAINS" => "LIKE",
                ">" => ">",
                "<" => "<",
                _ => "="
            };
        }

        /// <summary>
        /// Safely dispose
        /// </summary>
        public void Dispose()
        {
            SqliteConnection.ClearAllPools();
            GC.SuppressFinalize(this);
        }
    }
}