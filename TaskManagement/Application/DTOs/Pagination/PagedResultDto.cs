namespace Application.DTOs.Pagination
{
    public class PagedResultDto<T>(IEnumerable<T> items)
    {
        public IEnumerable<T> Items { get; set; } = items;
        public int TotalCount { get; set; }
        public int PageNumber { get; set; }
        public int PageSize { get; set; }
       
        public int TotalPages => (int)System.Math.Ceiling((double)TotalCount / PageSize);
    }
}
