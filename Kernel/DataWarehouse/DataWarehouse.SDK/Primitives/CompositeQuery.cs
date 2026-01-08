namespace DataWarehouse.SDK.Primitives
{
    /// <summary>
    /// Composite Query
    /// </summary>
    public class CompositeQuery
    {
        /// <summary>
        /// Logic
        /// </summary>
        public string Logic { get; set; } = "AND"; // AND / OR

        /// <summary>
        /// Filters
        /// </summary>
        public List<QueryFilter> Filters { get; set; } = [];
    }

    /// <summary>
    /// Query filters
    /// </summary>
    public class QueryFilter
    {
        /// <summary>
        /// Field ID
        /// </summary>
        public string Field { get; set; } = string.Empty;

        /// <summary>
        /// Operator
        /// ==, !=, >, <, CONTAINS
        /// </summary>
        public string Operator { get; set; } = "=="; 

        /// <summary>
        /// Value
        /// </summary>
        public object Value { get; set; } = string.Empty;
    }
}
