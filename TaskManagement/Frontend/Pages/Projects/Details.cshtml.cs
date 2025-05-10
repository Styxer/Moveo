using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using TaskManagement.Application.DTOs.Projects; // Reference Project DTOs
using TaskManagement.Application.DTOs.Tasks; // Reference Task DTOs
using TaskManagement.Application.DTOs.Pagination; // Reference Pagination DTOs
using TaskManagement.Frontend.Services; // Reference API Client
using System.Threading.Tasks;
using System;
using System.Linq; // Needed for Any()

namespace TaskManagement.Frontend.Pages.Projects
{
    public class DetailsModel : PageModel
    {
        private readonly ApiClient _apiClient;
        private readonly ILogger<DetailsModel> _logger;

        public DetailsModel(ApiClient apiClient, ILogger<DetailsModel> logger)
        {
            _apiClient = apiClient;
            _logger = logger;
        }

        public ProjectDto? Project { get; set; }
        public PagedResultDto<TaskDto>? Tasks { get; set; }

        // Bind properties from query string for task pagination/filtering/sorting
        [BindProperty(SupportsGet = true)]
        public TaskQueryParameters TaskQueryParameters { get; set; } = new TaskQueryParameters();


        public async Task<IActionResult> OnGetAsync(Guid id)
        {
            // Get project details
            Project = await _apiClient.GetProjectByIdAsync(id);

            if (Project == null)
            {
                // Handle not found or access denied
                _logger.LogWarning("Project {ProjectId} not found or access denied for details.", id);
                // Redirect to a not found page or the index page
                return RedirectToPage("/Index"); // Simple redirect for now
            }

            // Get tasks for this project
            Tasks = await _apiClient.GetTasksByProjectIdAsync(id, TaskQueryParameters);

            if (Tasks == null)
            {
                _logger.LogWarning("Failed to load tasks for project {ProjectId} or access denied.", id);
                Tasks = new PagedResultDto<TaskDto> { Items = Enumerable.Empty<TaskDto>(), TotalCount = 0 };
                // Optional: Add error message to TempData
                // TempData["ErrorMessage"] = "Failed to load tasks.";
            }


            return Page();
        }

        // Optional: OnPost methods for task filtering/sorting if using a form
        // public IActionResult OnPostAsync(Guid id)
        // {
        //     // Redirect to OnGet with updated task query parameters
        //     return RedirectToPage(new { id, TaskQueryParameters.PageNumber, TaskQueryParameters.PageSize, TaskQueryParameters.SearchQuery, TaskQueryParameters.SortBy, TaskQueryParameters.SortOrder });
        // }
    }
}