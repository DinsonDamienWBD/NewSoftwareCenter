using DataWarehouse.Contracts;
using DataWarehouse.Primitives;
using Microsoft.Data.Sqlite;
using System.Runtime.CompilerServices;
using System.Text.Json;

namespace DataWarehouse.Plugins
{
    /// <summary>
    /// Sqlite based queryable metadata index
    /// </summary>
    public class SqliteMetadataIndex : IQueryableIndex, IDisposable
    {
        /// <summary>
        /// ID
        /// </summary>
        public string Id => "SqliteIndex";

        /// <summary>
        /// Version
        /// </summary>
        public string Version => "1.0";

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
                Cache = SqliteCacheMode.Shared
            };
            _connectionString = builder.ToString();
            InitializeDb();
        }

        /// <summary>
        /// Initialize the DB
        /// </summary>
        private void InitializeDb()
        {
            using var conn = new SqliteConnection(_connectionString);
            conn.Open();
            // Create Table with JSON support
            var cmd = conn.CreateCommand();
            cmd.CommandText = @"
                CREATE TABLE IF NOT EXISTS Manifests (
                    Id TEXT PRIMARY KEY,
                    Json TEXT,
                    ContentSummary TEXT -- Indexed for Full Text Search (FTS) in future
                );
                CREATE INDEX IF NOT EXISTS idx_json ON Manifests(Json);
            ";
            cmd.ExecuteNonQuery();
        }

        /// <summary>
        /// Index the manifest
        /// </summary>
        /// <param name="manifest"></param>
        /// <returns></returns>
        public async Task IndexManifestAsync(Manifest manifest)
        {
            using var conn = new SqliteConnection(_connectionString);
            await conn.OpenAsync();

            using var cmd = conn.CreateCommand();
            cmd.CommandText = @"
                INSERT INTO Manifests (Id, Json, ContentSummary) 
                VALUES (@id, @json, @summary)
                ON CONFLICT(Id) DO UPDATE SET 
                    Json = @json,
                    ContentSummary = @summary;
            ";

            cmd.Parameters.AddWithValue("@id", manifest.Id);
            cmd.Parameters.AddWithValue("@json", JsonSerializer.Serialize(manifest));
            cmd.Parameters.AddWithValue("@summary", manifest.ContentSummary ?? "");

            await cmd.ExecuteNonQueryAsync();
        }

        /// <summary>
        /// Execute query
        /// </summary>
        /// <param name="query"></param>
        /// <param name="limit"></param>
        /// <returns></returns>
        public async Task<string[]> ExecuteQueryAsync(CompositeQuery query, int limit = 50)
        {
            // Translate CompositeQuery to SQL JSON Query
            // SQLite JSON Syntax: json_extract(Json, '$.SizeBytes') > 1024

            var whereClauses = new List<string>();
            var parameters = new List<SqliteParameter>();
            int pIndex = 0;

            foreach (var f in query.Filters)
            {
                string jsonPath = f.Field.StartsWith("Tags.")
                    ? $"$.Tags.\"{f.Field[5..]}\"" // Handle Tag lookup
                    : $"$.{f.Field}";                      // Handle Root lookup

                string paramName = $"@p{pIndex++}";
                string sqlOp = MapOperator(f.Operator);

                whereClauses.Add($"json_extract(Json, '{jsonPath}') {sqlOp} {paramName}");
                parameters.Add(new SqliteParameter(paramName, f.Value.ToString()));
            }

            string logic = query.Logic == "OR" ? "OR" : "AND";
            string sql = $"SELECT Id FROM Manifests WHERE {string.Join($" {logic} ", whereClauses)} LIMIT {limit}";

            return await RunSqlInternal(sql, parameters);
        }

        /// <summary>
        /// Execute SQL
        /// </summary>
        /// <param name="sqlQuery"></param>
        /// <param name="limit"></param>
        /// <returns></returns>
        public async Task<string[]> ExecuteSqlAsync(string sqlQuery, int limit = 50)
        {
            // DANGER: SQL Injection risk if exposed directly to users.
            // In a real product, we would use a whitelist parser.
            // For "God Tier" internal tools, we assume the Manager sanitizes inputs.

            // Allow users to write: "SELECT Id FROM Manifests WHERE ..."
            // Or simplified: "json_extract(Json, '$.SizeBytes') > 100"

            string sql = sqlQuery.Trim();
            if (!sql.StartsWith("SELECT", StringComparison.OrdinalIgnoreCase))
            {
                // Auto-wrap partial WHERE clauses
                sql = $"SELECT Id FROM Manifests WHERE {sql} LIMIT {limit}";
            }

            return await RunSqlInternal(sql, []);
        }

        /// <summary>
        /// Run SQL
        /// </summary>
        /// <param name="sql"></param>
        /// <param name="parameters"></param>
        /// <returns></returns>
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

        /// <summary>
        /// Search
        /// </summary>
        /// <param name="query"></param>
        /// <param name="vector"></param>
        /// <param name="limit"></param>
        /// <returns></returns>
        public Task<string[]> SearchAsync(string query, float[]? vector, int limit)
        {
            // Basic LIKE search implementation
            string sql = $"SELECT Id FROM Manifests WHERE ContentSummary LIKE @q LIMIT {limit}";
            return RunSqlInternal(sql, [new SqliteParameter("@q", $"%{query}%")]);
        }

        private static string MapOperator(string op)
        {
            return op switch
            {
                "==" => "=",
                "!=" => "<>",
                "CONTAINS" => "LIKE", // Requires % wildcard in value
                ">" => ">",
                "<" => "<",
                _ => "="
            };
        }

        /// <summary>
        /// Enumerate all
        /// </summary>
        /// <param name="ct"></param>
        /// <returns></returns>
        public async IAsyncEnumerable<Manifest> EnumerateAllAsync([EnumeratorCancellation] CancellationToken ct = default)
        {
            // Create a new connection for the enumeration to avoid threading conflicts with other active reads
            using var conn = new SqliteConnection(_connectionString);
            await conn.OpenAsync(ct);

            using var cmd = conn.CreateCommand();
            // Select the raw JSON blob so we can hydrate the full object
            cmd.CommandText = "SELECT Json FROM Manifests";

            using var reader = await cmd.ExecuteReaderAsync(ct);
            while (await reader.ReadAsync(ct))
            {
                var json = reader.GetString(0);
                var manifest = JsonSerializer.Deserialize<Manifest>(json);
                if (manifest != null)
                {
                    yield return manifest;
                }
            }
        }

        /// <summary>
        /// Safely dispose
        /// </summary>
        public void Dispose()
        {
            // If we held onto a specific connection field (e.g. _persistentConnection), we would dispose it here.
            // Even if we don't, implementing the pattern correctly prevents static analysis warnings.

            // [FIX CA1816] Suppress Finalize
            GC.SuppressFinalize(this);
        }
    }
}