using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using TaskManagement.Application.DTOs.Projects;
using TaskManagement.Frontend.Services; 
using System.Threading.Tasks;
using System;

namespace TaskManagement.Frontend.Pages.Projects
{
    public class DeleteModel : PageModel
    {
        private readonly ApiClient _apiClient;
        private readonly ILogger<DeleteModel> _logger;

        public DeleteModel(ApiClient apiClient, ILogger<DeleteModel> logger)
        {
            _apiClient = apiClient;
            _logger = logger;
        }

        public ProjectDto? Project { get; set; }

        [BindProperty] 
        public Guid ProjectId { get; set; }


        public async Task<IActionResult> OnGetAsync(Guid id)
        {
            var projectDto = await _apiClient.GetProjectByIdAsync(id);

            if (projectDto == null)
            {
                _logger.LogWarning("Project {ProjectId} not found or access denied for delete confirmation.", id);
                return RedirectToPage("/Index"); 
            }

            Project = projectDto;
            ProjectId = id; 

            return Page();
        }

        public async Task<IActionResult> OnPostAsync()
        {
            try
            {
                var success = await _apiClient.DeleteProjectAsync(ProjectId);

                if (success)
                {                   
                    return RedirectToPage("/Index");
                }
                else
                {                  
                    _logger.LogWarning("API client failed to delete project {ProjectId}.", ProjectId);
                    ModelState.AddModelError(string.Empty, "Failed to delete project. Please try again.");                 
                    Project = await _apiClient.GetProjectByIdAsync(ProjectId);
                    return Page();
                }
            }
            catch (HttpRequestException ex)
            {               
                _logger.LogError(ex, "HTTP request error during project deletion for {ProjectId}.", ProjectId);
                ModelState.AddModelError(string.Empty, $"Error deleting project: {ex.Message}");               
                Project = await _apiClient.GetProjectByIdAsync(ProjectId);
                return Page();
            }
            catch (Exception ex)
            {                
                _logger.LogError(ex, "An unexpected error occurred during project deletion for {ProjectId}.", ProjectId);
                ModelState.AddModelError(string.Empty, "An unexpected error occurred. Please try again.");         
                Project = await _apiClient.GetProjectByIdAsync(ProjectId);
                return Page();
            }
        }
    }
}
