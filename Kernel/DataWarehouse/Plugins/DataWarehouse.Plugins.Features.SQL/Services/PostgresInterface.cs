using DataWarehouse.SDK.Contracts;
using DataWarehouse.SDK.Primitives;
using Microsoft.Extensions.Logging;

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

            if (string.IsNullOrWhiteSpace(sql))
                return [];

            var normalizedSql = sql.Trim();

            // Simple Parser Logic (God Tier V5)
            // supports: SELECT * FROM Manifests WHERE [Condition]
            if (normalizedSql.StartsWith("SELECT", StringComparison.OrdinalIgnoreCase))
            {
                var allItems = _index.EnumerateAllAsync(ct);
                var result = new List<Manifest>();

                await foreach (var item in allItems)
                {
                    if (EvaluateWhereClause(item, normalizedSql))
                    {
                        result.Add(item);
                    }
                }

                _logger.LogDebug("SQL Result: {Count} rows", result.Count);
                return result;
            }

            _logger.LogWarning("Unsupported SQL command ignored.");
            return [];
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