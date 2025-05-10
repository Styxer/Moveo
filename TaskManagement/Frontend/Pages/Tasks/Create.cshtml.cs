using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using TaskManagement.Application.DTOs.Tasks; // Reference Task DTOs
using TaskManagement.Domain.Models; // Reference TaskStatus enum
using TaskManagement.Frontend.Services; // Reference API Client
using System.Threading.Tasks;
using System;
using Microsoft.AspNetCore.Mvc.Rendering; // Needed for SelectList

namespace TaskManagement.Frontend.Pages.Tasks
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
        public CreateTaskRequestDto Task { get; set; } = new CreateTaskRequestDto();

        [BindProperty(SupportsGet = true)] // Bind ProjectId from route/query
        public Guid ProjectId { get; set; }

        // SelectList for Task Status
        public SelectList StatusOptions { get; set; } = new SelectList(Enum.GetNames(typeof(TaskStatus)));


        public async Task<IActionResult> OnGetAsync(Guid projectId)
        {
            // Optional: Verify the project exists and user has access before showing the form
            // This check could be done by calling API client GetProjectByIdAsync(projectId)
            // If it returns null (not found or forbidden), redirect.
            // For simplicity, we'll assume the project exists and access is handled by the API on POST.

            ProjectId = projectId; // Store the project ID

            return Page();
        }

        public async Task<IActionResult> OnPostAsync()
        {
            if (!ModelState.IsValid)
            {
                // Re-populate StatusOptions if validation fails
                StatusOptions = new SelectList(Enum.GetNames(typeof(TaskStatus)));
                return Page(); // Re-display form with validation errors
            }

            try
            {
                // Call the API client to create the task
                var createdTask = await _apiClient.CreateTaskAsync(ProjectId, Task);

                if (createdTask != null)
                {
                    // Redirect to the project details page after creating the task
                    return RedirectToPage("/Projects/Details", new { id = ProjectId });
                }
                else
                {
                    // Handle API error (e.g., access denied, project not found, validation/conflict already handled)
                    _logger.LogWarning("API client failed to create task for project {ProjectId}.", ProjectId);
                    ModelState.AddModelError(string.Empty, "Failed to create task. Please try again.");
                    // Re-populate StatusOptions
                    StatusOptions = new SelectList(Enum.GetNames(typeof(TaskStatus)));
                    return Page();
                }
            }
            catch (HttpRequestException ex)
            {
                // Handle HTTP request errors
                _logger.LogError(ex, "HTTP request error during task creation for project {ProjectId}.", ProjectId);
                ModelState.AddModelError(string.Empty, $"Error creating task: {ex.Message}");
                // Re-populate StatusOptions
                StatusOptions = new SelectList(Enum.GetNames(typeof(TaskStatus)));
                return Page();
            }
            catch (Exception ex)
            {
                // Handle other unexpected errors
                _logger.LogError(ex, "An unexpected error occurred during task creation for project {ProjectId}.", ProjectId);
                ModelState.AddModelError(string.Empty, "An unexpected error occurred. Please try again.");
                // Re-populate StatusOptions
                StatusOptions = new SelectList(Enum.GetNames(typeof(TaskStatus)));
                return Page();
            }
        }
    }
}