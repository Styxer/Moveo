namespace Application.DTOs.Pagination
{
    public class PagedResultDto<T>()
    {
        public IEnumerable<T> Items { get; set; }
        public int TotalCount { get; set; }
        public int PageNumber { get; set; }
        public int PageSize { get; set; }
       
        public int TotalPages => (int)System.Math.Ceiling((double)TotalCount / PageSize);
    }
}
