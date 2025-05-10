namespace Application.DTOs.Pagination
{
    // Base class for combined pagination, filtering, and sorting parameters
    public abstract class QueryParameters : PaginationParams
    {
        public string SearchQuery { get; set; } = string.Empty;

        public string SortBy { get; set; } = string.Empty;

        public string SortOrder { get; set; } = string.Empty;
    }
}
