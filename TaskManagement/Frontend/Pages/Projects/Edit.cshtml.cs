using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using TaskManagement.Application.DTOs.Projects;
using TaskManagement.Frontend.Services; 
using System.Threading.Tasks;
using System;

namespace TaskManagement.Frontend.Pages.Projects
{
    public class EditModel : PageModel
    {
        private readonly ApiClient _apiClient;
        private readonly ILogger<EditModel> _logger;

        public EditModel(ApiClient apiClient, ILogger<EditModel> logger)
        {
            _apiClient = apiClient;
            _logger = logger;
        }

        [BindProperty]
        public UpdateProjectRequestDto Project { get; set; } = new UpdateProjectRequestDto();

        [BindProperty] 
        public Guid ProjectId { get; set; }

        public async Task<IActionResult> OnGetAsync(Guid id)
        {
            var projectDto = await _apiClient.GetProjectByIdAsync(id);

            if (projectDto == null)
            {
                _logger.LogWarning("Project {ProjectId} not found or access denied for edit.", id);
                return RedirectToPage("/Index"); 
            }

            /
            Project = new UpdateProjectRequestDto
            {
                Name = projectDto.Name,
                Description = projectDto.Description
            };
            ProjectId = id;

            return Page();
        }

        public async Task<IActionResult> OnPostAsync()
        {
            if (!ModelState.IsValid)
            {
                return Page(); 
            }

            try
            {
                var success = await _apiClient.UpdateProjectAsync(ProjectId, Project);

                if (success)
                {                   
                    return RedirectToPage("./Details", new { id = ProjectId });
                }
                else
                {                  
                    _logger.LogWarning("API client failed to update project {ProjectId}.", ProjectId);
                    ModelState.AddModelError(string.Empty, "Failed to update project. Please try again.");
                    return Page();
                }
            }
            catch (HttpRequestException ex)
            {             
                _logger.LogError(ex, "HTTP request error during project update for {ProjectId}.", ProjectId);
                ModelState.AddModelError(string.Empty, $"Error updating project: {ex.Message}");
                return Page();
            }
            catch (Exception ex)
            {              
                _logger.LogError(ex, "An unexpected error occurred during project update for {ProjectId}.", ProjectId);
                ModelState.AddModelError(string.Empty, "An unexpected error occurred. Please try again.");
                return Page();
            }
        }
    }
}
