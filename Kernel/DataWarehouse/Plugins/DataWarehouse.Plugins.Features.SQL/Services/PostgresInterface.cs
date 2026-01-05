using DataWarehouse.Plugins.Features.SQL.Engine;
using DataWarehouse.SDK.Contracts;
using DataWarehouse.SDK.Primitives;
using Microsoft.Extensions.Logging;
using System.Text;
using System.Text.Json;

namespace DataWarehouse.Plugins.Features.SQL.Services
{
    /// <summary>
    /// Adapts the Metadata Index into a queryable SQL engine.
    /// Handles parsing and execution logic.
    /// </summary>
    /// <remarks>
    /// Constructor
    /// </remarks>
    /// <param name="index"></param>
    /// <param name="logger"></param>
    /// <exception cref="ArgumentNullException"></exception>
    public partial class PostgresInterface(IMetadataIndex index, ILogger logger)
    {
        private readonly IMetadataIndex _index = index ?? throw new ArgumentNullException(nameof(index));
        private readonly ILogger _logger = logger ?? throw new ArgumentNullException(nameof(logger));

        /// <summary>
        /// Executes a SELECT query against the underlying metadata index.
        /// </summary>
        public async Task<IEnumerable<Manifest>> ExecuteQueryAsync(string sql, CancellationToken ct = default)
        {
            _logger.LogInformation("SQL Exec: {Sql}", sql);

            if (string.IsNullOrWhiteSpace(sql)) return [];

            try
            {
                // 1. Parse SQL
                var parser = new SimpleSqlParser(sql);
                var query = parser.Parse();

                // 2. Validate Table
                if (!query.TableName.Equals("Manifests", StringComparison.OrdinalIgnoreCase))
                {
                    throw new InvalidOperationException($"Table '{query.TableName}' does not exist. Only 'Manifests' is supported.");
                }

                // 3. Build SQL for Underlying Engine
                // We reconstruct the SQL compatible with the Postgres JSONB schema.
                var sb = new StringBuilder();
                sb.Append("SELECT JsonData FROM Manifests");

                if (query.Conditions.Count > 0)
                {
                    sb.Append(" WHERE ");
                    for (int i = 0; i < query.Conditions.Count; i++)
                    {
                        var c = query.Conditions[i];
                        if (i > 0) sb.Append($" {c.Logic} ");

                        // JSONB Property Access Mapping
                        // e.g. "Size" -> "JsonData->>'SizeBytes'"
                        string field = MapFieldToColumn(c.Field);

                        // Handle numeric comparison vs string comparison
                        if (long.TryParse(c.Value, out _))
                        {
                            // Numeric Cast
                            sb.Append($"({field})::numeric {c.Operator} {c.Value}");
                        }
                        else
                        {
                            // String
                            sb.Append($"{field} {c.Operator} '{c.Value}'");
                        }
                    }
                }

                sb.Append($" LIMIT {query.Limit}");
                string translatedSql = sb.ToString();

                // 4. Execute via Index Plugin
                // The index plugin expects a Raw SQL that it can execute against its DB.
                string[] jsonResults = await _index.ExecuteQueryAsync(translatedSql, query.Limit);

                // 5. Hydrate Objects
                var manifests = new List<Manifest>();
                foreach (var json in jsonResults)
                {
                    var m = JsonSerializer.Deserialize<Manifest>(json);
                    if (m != null) manifests.Add(m);
                }

                return manifests;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to execute SQL");
                return [];
            }
        }

        private static string MapFieldToColumn(string field)
        {
            // Maps virtual SQL columns to JSONB paths or Real Columns
            return field.ToLower() switch
            {
                "id" => "Id",
                "containerid" => "ContainerId",
                "bloburi" => "BlobUri",
                "ownerid" => "OwnerId",
                "size" => "SizeBytes",
                "sizebytes" => "SizeBytes",
                "tier" => "JsonData->>'CurrentTier'",
                "summary" => "JsonData->>'ContentSummary'",
                _ => $"JsonData->>'{field}'" // Generic JSON property access
            };
        }

        /// <summary>
        /// Naive SQL WHERE clause evaluator. 
        /// In a real engine, this would use an Expression Tree builder.
        /// </summary>
        private static bool EvaluateWhereClause(Manifest item, string sql)
        {
            // If no WHERE clause, return everything
            int whereIndex = sql.IndexOf("WHERE", StringComparison.OrdinalIgnoreCase);
            if (whereIndex == -1) return true;

            string condition = sql[(whereIndex + 5)..].Trim();

            // Support: ID = 'value'
            if (condition.StartsWith("ID", StringComparison.OrdinalIgnoreCase))
            {
                string val = ExtractValue(condition);
                return item.Id.Equals(val, StringComparison.OrdinalIgnoreCase);
            }

            // Support: TIER = 'value'
            if (condition.StartsWith("TIER", StringComparison.OrdinalIgnoreCase))
            {
                string val = ExtractValue(condition);
                return (item.CurrentTier ?? "").Equals(val, StringComparison.OrdinalIgnoreCase);
            }

            // Support: CONTAINER = 'value'
            if (condition.StartsWith("CONTAINER", StringComparison.OrdinalIgnoreCase))
            {
                string val = ExtractValue(condition);
                return item.ContainerId.Equals(val, StringComparison.OrdinalIgnoreCase);
            }

            return false; // Unknown filter -> No match (Strict)
        }

        private static string ExtractValue(string condition)
        {
            // extracting 'value' from: FIELD = 'value'
            int quoteStart = condition.IndexOf('\'');
            int quoteEnd = condition.LastIndexOf('\'');
            if (quoteStart != -1 && quoteEnd > quoteStart)
            {
                return condition.Substring(quoteStart + 1, quoteEnd - quoteStart - 1);
            }
            return string.Empty;
        }

        // --- Nested Logger Adapter ---

        /// <summary>
        /// A lightweight, allocation-free adapter bridging IKernelContext to ILogger.
        /// </summary>
        public class ContextLoggerAdapter<T>(IKernelContext context) : ILogger<T>
        {
            private readonly IKernelContext _context = context;

            /// <summary>
            /// Begin scope
            /// </summary>
            /// <typeparam name="TState"></typeparam>
            /// <param name="state"></param>
            /// <returns></returns>
            public IDisposable? BeginScope<TState>(TState state) where TState : notnull => NullScope.Instance;

            /// <summary>
            /// Is enabled
            /// </summary>
            /// <param name="logLevel"></param>
            /// <returns></returns>
            public bool IsEnabled(LogLevel logLevel) => true;

            /// <summary>
            /// Log
            /// </summary>
            /// <typeparam name="TState"></typeparam>
            /// <param name="logLevel"></param>
            /// <param name="eventId"></param>
            /// <param name="state"></param>
            /// <param name="exception"></param>
            /// <param name="formatter"></param>
            public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
            {
                string message = formatter(state, exception);

                if (logLevel >= LogLevel.Error)
                {
                    _context.LogError(message, exception);
                }
                else if (logLevel == LogLevel.Warning)
                {
                    _context.LogWarning(message);
                }
                else
                {
                    // Map Debug/Info/Trace to Info (or Debug if available)
                    if (logLevel == LogLevel.Debug || logLevel == LogLevel.Trace)
                        _context.LogDebug(message);
                    else
                        _context.LogInfo(message);
                }
            }

            /// <summary>
            /// Null scope
            /// </summary>
            private sealed class NullScope : IDisposable
            {
                /// <summary>
                /// Nullscope instance
                /// </summary>
                public static readonly NullScope Instance = new();

                /// <summary>
                /// Safely dispose
                /// </summary>
                public void Dispose() { }
            }
        }
    }
}