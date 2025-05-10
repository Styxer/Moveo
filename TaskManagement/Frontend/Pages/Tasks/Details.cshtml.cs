using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using TaskManagement.Application.DTOs.Tasks; // Reference Task DTOs
using TaskManagement.Frontend.Services; // Reference API Client
using System.Threading.Tasks;
using System;

namespace TaskManagement.Frontend.Pages.Tasks
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

        public TaskDto? Task { get; set; }
        public Guid ProjectId { get; set; } // To link back to the project

        public async Task<IActionResult> OnGetAsync(Guid taskId)
        {
            Task = await _apiClient.GetTaskByIdAsync(taskId);

            if (Task == null)
            {
                _logger.LogWarning("Task {TaskId} not found or access denied for details.", taskId);
                // Redirect to a not found page or the index page
                return RedirectToPage("/Index"); // Simple redirect for now
            }

            ProjectId = Task.ProjectId; // Store the parent project ID

            return Page();
        }
    }
}