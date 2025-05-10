using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using TaskManagement.Application.DTOs.Projects; // Reference Project DTOs
using TaskManagement.Frontend.Services; // Reference API Client
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

        [BindProperty] // Keep the project ID for the POST handler
        public Guid ProjectId { get; set; }

        public async Task<IActionResult> OnGetAsync(Guid id)
        {
            var projectDto = await _apiClient.GetProjectByIdAsync(id);

            if (projectDto == null)
            {
                _logger.LogWarning("Project {ProjectId} not found or access denied for edit.", id);
                return RedirectToPage("/Index"); // Redirect if not found or no access
            }

            // Map DTO to the update request model for the form
            Project = new UpdateProjectRequestDto
            {
                Name = projectDto.Name,
                Description = projectDto.Description
            };
            ProjectId = id; // Store the ID

            return Page();
        }

        public async Task<IActionResult> OnPostAsync()
        {
            if (!ModelState.IsValid)
            {
                return Page(); // Re-display form with validation errors
            }

            try
            {
                var success = await _apiClient.UpdateProjectAsync(ProjectId, Project);

                if (success)
                {
                    // Redirect to the project details page or the index page
                    return RedirectToPage("./Details", new { id = ProjectId });
                }
                else
                {
                    // Handle API error (e.g., access denied, not found, validation/conflict already handled)
                    _logger.LogWarning("API client failed to update project {ProjectId}.", ProjectId);
                    ModelState.AddModelError(string.Empty, "Failed to update project. Please try again.");
                    return Page();
                }
            }
            catch (HttpRequestException ex)
            {
                // Handle HTTP request errors
                _logger.LogError(ex, "HTTP request error during project update for {ProjectId}.", ProjectId);
                ModelState.AddModelError(string.Empty, $"Error updating project: {ex.Message}");
                return Page();
            }
            catch (Exception ex)
            {
                // Handle other unexpected errors
                _logger.LogError(ex, "An unexpected error occurred during project update for {ProjectId}.", ProjectId);
                ModelState.AddModelError(string.Empty, "An unexpected error occurred. Please try again.");
                return Page();
            }
        }
    }
}