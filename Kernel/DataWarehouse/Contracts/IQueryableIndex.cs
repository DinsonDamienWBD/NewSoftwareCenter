// NOTE: These are now NATIVE to DataWarehouse. 
// A user downloading "Cosmic.DataWarehouse" from NuGet gets these.

using System.Collections.Generic;
using System.Threading.Tasks;

namespace DataWarehouse.Contracts
{
    /// <summary>
    /// extend the index to accept structured queries.
    /// A simplified NoSQL-style filter definition
    /// </summary>
    public record QueryFilter
    {
        /// <summary>
        /// Field
        /// </summary>
        public string Field { get; init; } = string.Empty;

        /// <summary>
        /// Operator
        /// </summary>
        public string Operator { get; init; } = "=="; // ==, !=, >, <, CONTAINS

        /// <summary>
        /// Value
        /// </summary>
        public object Value { get; init; } = null!;
    }

    /// <summary>
    /// Query
    /// </summary>
    public record CompositeQuery
    {
        /// <summary>
        /// Filters
        /// </summary>
        public List<QueryFilter> Filters { get; init; } = new();

        /// <summary>
        /// Logic
        /// </summary>
        public string Logic { get; init; } = "AND"; // AND / OR
    }

    /// <summary>
    /// Make metadata queryable
    /// </summary>
    public interface IQueryableIndex : IMetadataIndex
    {
        /// <summary>
        /// Executes a structured NoSQL-style query against metadata.
        /// </summary>
        Task<string[]> ExecuteQueryAsync(CompositeQuery query, int limit = 50);

        /// <summary>
        /// (Optional) Executes a raw SQL-like string against metadata.
        /// e.g. "SELECT Id FROM Manifest WHERE Tags CONTAINS 'Urgent' AND Size > 1024"
        /// </summary>
        Task<string[]> ExecuteSqlAsync(string sqlQuery, int limit = 50);
    }
}