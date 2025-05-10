using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using TaskManagement.Application.DTOs.Projects; // Reference Project DTOs
using TaskManagement.Frontend.Services; // Reference API Client
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

        [BindProperty] // Keep the project ID for the POST handler
        public Guid ProjectId { get; set; }


        public async Task<IActionResult> OnGetAsync(Guid id)
        {
            var projectDto = await _apiClient.GetProjectByIdAsync(id);

            if (projectDto == null)
            {
                _logger.LogWarning("Project {ProjectId} not found or access denied for delete confirmation.", id);
                return RedirectToPage("/Index"); // Redirect if not found or no access
            }

            Project = projectDto;
            ProjectId = id; // Store the ID

            return Page();
        }

        public async Task<IActionResult> OnPostAsync()
        {
            try
            {
                var success = await _apiClient.DeleteProjectAsync(ProjectId);

                if (success)
                {
                    // Redirect to the index page after deletion
                    return RedirectToPage("/Index");
                }
                else
                {
                    // Handle API error (e.g., access denied, not found already handled)
                    _logger.LogWarning("API client failed to delete project {ProjectId}.", ProjectId);
                    ModelState.AddModelError(string.Empty, "Failed to delete project. Please try again.");
                    // Re-fetch project details to display the confirmation page again
                    Project = await _apiClient.GetProjectByIdAsync(ProjectId);
                    return Page();
                }
            }
            catch (HttpRequestException ex)
            {
                // Handle HTTP request errors
                _logger.LogError(ex, "HTTP request error during project deletion for {ProjectId}.", ProjectId);
                ModelState.AddModelError(string.Empty, $"Error deleting project: {ex.Message}");
                // Re-fetch project details
                Project = await _apiClient.GetProjectByIdAsync(ProjectId);
                return Page();
            }
            catch (Exception ex)
            {
                // Handle other unexpected errors
                _logger.LogError(ex, "An unexpected error occurred during project deletion for {ProjectId}.", ProjectId);
                ModelState.AddModelError(string.Empty, "An unexpected error occurred. Please try again.");
                // Re-fetch project details
                Project = await _apiClient.GetProjectByIdAsync(ProjectId);
                return Page();
            }
        }
    }
}