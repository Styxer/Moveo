using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using TaskManagement.Frontend.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using TaskManagement.Application.DTOs.Projects; // Reference Project DTOs
using TaskManagement.Application.DTOs.Pagination; // Reference Pagination DTOs
using TaskManagement.Frontend.Services; // Reference API Client

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

        // Bind properties from query string for pagination, filtering, sorting
        [BindProperty(SupportsGet = true)]
        public ProjectQueryParameters QueryParameters { get; set; } = new ProjectQueryParameters();

        public async Task<IActionResult> OnGetAsync()
        {
            // Call the API client to get projects
            Projects = await _apiClient.GetProjectsAsync(QueryParameters);

            if (Projects == null)
            {
                // Handle API errors or access denied (e.g., redirect to login)
                _logger.LogWarning("Failed to load projects or access denied.");
                // Example: Redirect to login page if unauthorized
                // return RedirectToPage("/Auth/Login");
                // For now, just show an empty list or error message
                Projects = new PagedResultDto<ProjectDto> { Items = Enumerable.Empty<ProjectDto>(), TotalCount = 0 };
            }

            return Page();
        }

        // TODO: OnPost methods for filtering/sorting if using a form
        // public IActionResult OnPostAsync()
        // {
        //     // Redirect to OnGet with updated query parameters
        //     return RedirectToPage(new { QueryParameters.PageNumber, QueryParameters.PageSize, QueryParameters.SearchQuery, QueryParameters.SortBy, QueryParameters.SortOrder });
        // }
    }
}
