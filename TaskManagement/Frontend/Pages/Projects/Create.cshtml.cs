using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using TaskManagement.Application.DTOs.Projects; // Reference Project DTOs
using TaskManagement.Frontend.Services; // Reference API Client
using System.Threading.Tasks;
using System;

namespace TaskManagement.Frontend.Pages.Projects
{
    public class CreateModel : PageModel
    {
        private readonly ApiClient _apiClient;
        private readonly ILogger<CreateModel> _logger;

        public CreateModel(ApiClient apiClient, ILogger<CreateModel> logger)
        {
            _apiClient = apiClient;
            _logger = logger;
        }

        [BindProperty]
        public CreateProjectRequestDto Project { get; set; } = new CreateProjectRequestDto();

        public IActionResult OnGet()
        {
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
                var createdProject = await _apiClient.CreateProjectAsync(Project);

                if (createdProject != null)
                {
                    // Redirect to the project details page or the index page
                    return RedirectToPage("/Index");
                }
                else
                {
                    // Handle API error (e.g., access denied, validation failed already handled by API client)
                    _logger.LogWarning("API client failed to create project.");
                    ModelState.AddModelError(string.Empty, "Failed to create project. Please try again.");
                    return Page();
                }
            }
            catch (HttpRequestException ex)
            {
                // Handle HTTP request errors (e.g., network issues, API returned 400/409 with details)
                _logger.LogError(ex, "HTTP request error during project creation.");
                ModelState.AddModelError(string.Empty, $"Error creating project: {ex.Message}");
                return Page();
            }
            catch (Exception ex)
            {
                // Handle other unexpected errors
                _logger.LogError(ex, "An unexpected error occurred during project creation.");
                ModelState.AddModelError(string.Empty, "An unexpected error occurred. Please try again.");
                return Page();
            }
        }
    }
}