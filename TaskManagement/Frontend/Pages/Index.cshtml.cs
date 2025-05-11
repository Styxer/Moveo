using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using TaskManagement.Frontend.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using TaskManagement.Application.DTOs.Projects; 
using TaskManagement.Application.DTOs.Pagination; 
using TaskManagement.Frontend.Services; 

namespace Frontend.Pages
{
    public class IndexModel : PageModel
    {
        private readonly ApiClient _apiClient;
        private readonly ILogger<IndexModel> _logger;

        public IndexModel(ApiClient apiClient, ILogger<IndexModel> logger)
        {
            _apiClient = apiClient;
            _logger = logger;
        }

        public PagedResultDto<ProjectDto>? Projects { get; set; }
      
        [BindProperty(SupportsGet = true)]
        public ProjectQueryParameters QueryParameters { get; set; } = new ProjectQueryParameters();

        public async Task<IActionResult> OnGetAsync()
        {
      
            Projects = await _apiClient.GetProjectsAsync(QueryParameters);

            if (Projects == null)
            {
               
                _logger.LogWarning("Failed to load projects or access denied.");          
              
                Projects = new PagedResultDto<ProjectDto> { Items = Enumerable.Empty<ProjectDto>(), TotalCount = 0 };
            }

             return RedirectToPage("/Auth/Login");
        }

        
         public IActionResult OnPostAsync()
         {
             // Redirect to OnGet with updated query parameters
            return RedirectToPage(new { QueryParameters.PageNumber, QueryParameters.PageSize, QueryParameters.SearchQuery, QueryParameters.SortBy, QueryParameters.SortOrder });
         }
    }
}
