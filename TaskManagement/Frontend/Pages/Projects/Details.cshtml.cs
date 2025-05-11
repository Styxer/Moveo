using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using TaskManagement.Application.DTOs.Projects;
using TaskManagement.Application.DTOs.Tasks; 
using TaskManagement.Application.DTOs.Pagination; 
using TaskManagement.Frontend.Services; 
using System.Threading.Tasks;
using System;
using System.Linq; 

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
      
        [BindProperty(SupportsGet = true)]
        public TaskQueryParameters TaskQueryParameters { get; set; } = new TaskQueryParameters();


        public async Task<IActionResult> OnGetAsync(Guid id)
        {          
            Project = await _apiClient.GetProjectByIdAsync(id);

            if (Project == null)
            {              
                _logger.LogWarning("Project {ProjectId} not found or access denied for details.", id);             
                return RedirectToPage("/Index"); // Simple redirect 
            }

            Tasks = await _apiClient.GetTasksByProjectIdAsync(id, TaskQueryParameters);

            if (Tasks == null)
            {
                _logger.LogWarning("Failed to load tasks for project {ProjectId} or access denied.", id);
                Tasks = new PagedResultDto<TaskDto> { Items = Enumerable.Empty<TaskDto>(), TotalCount = 0 };              
                 TempData["ErrorMessage"] = "Failed to load tasks.";
            }

            return Page();
        }

      
         public IActionResult OnPostAsync(Guid id)
         {             
             return RedirectToPage(new { id, TaskQueryParameters.PageNumber, TaskQueryParameters.PageSize, TaskQueryParameters.SearchQuery, TaskQueryParameters.SortBy, TaskQueryParameters.SortOrder });
         }
    }
}
