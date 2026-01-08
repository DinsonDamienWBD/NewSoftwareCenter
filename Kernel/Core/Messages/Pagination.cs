using System;

namespace Core.Messages
{
    /// <summary>
    /// Interface for Queries that support result pagination.
    /// </summary>
    public interface IPagedQuery
    {
        /// <summary>
        /// Page number (1-based).
        /// </summary>
        int Page { get; }

        /// <summary>
        /// Page size (number of items per page).
        /// </summary>
        int PageSize { get; }
    }

    /// <summary>
    /// Standard wrapper for paginated lists.
    /// </summary>
    /// <remarks>
    /// Parametrized constructor.
    /// </remarks>
    /// <param name="items"></param>
    /// <param name="totalCount"></param>
    /// <param name="page"></param>
    /// <param name="pageSize"></param>
    public class PagedResult<T>(IEnumerable<T> items, int totalCount, int page, int pageSize)
    {
        /// <summary>
        /// Items on the current page.
        /// </summary>
        public IEnumerable<T> Items { get; } = items;

        /// <summary>
        /// Complete count of items across all pages.
        /// </summary>
        public int TotalCount { get; } = totalCount;

        /// <summary>
        /// Page number (1-based).
        /// </summary>
        public int Page { get; } = page;

        /// <summary>
        /// Size of each page (number of items per page).
        /// </summary>
        public int PageSize { get; } = pageSize;

        /// <summary>
        /// Calculated total number of pages.
        /// </summary>
        public int TotalPages => (int)Math.Ceiling((double)TotalCount / PageSize);
    }

    /// <summary>
    /// Interface for sortable queries
    /// </summary>
    public interface ISortableQuery
    {
        /// <summary>
        /// Field to sort by
        /// </summary>
        string? SortBy { get; }

        /// <summary>
        /// Sort order
        /// </summary>
        bool SortDescending { get; }
    }

    /// <summary>
    /// Interface for filterable query
    /// </summary>
    public interface IFilterableQuery
    {
        /// <summary>
        /// Filter by
        /// </summary>
        string? FilterJson { get; } // Or generic dictionary
    }

    /// <summary>
    /// Sort descriptor
    /// </summary>
    public class SortDescriptor
    {
        /// <summary>
        /// Gte field to sort by
        /// </summary>
        public string? Field { get; set; }

        /// <summary>
        /// Get the sort order
        /// </summary>
        public bool Descending { get; set; }
    }
}