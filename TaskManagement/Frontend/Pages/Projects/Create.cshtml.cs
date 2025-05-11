using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using TaskManagement.Application.DTOs.Projects; 
using TaskManagement.Frontend.Services; 
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
                return Page();
            }

            try
            {
                var createdProject = await _apiClient.CreateProjectAsync(Project);

                if (createdProject != null)
                {                 
                    return RedirectToPage("/Index");
                }
                else
                {                    
                    _logger.LogWarning("API client failed to create project.");
                    ModelState.AddModelError(string.Empty, "Failed to create project. Please try again.");
                    return Page();
                }
            }
            catch (HttpRequestException ex)
            {              
                _logger.LogError(ex, "HTTP request error during project creation.");
                ModelState.AddModelError(string.Empty, $"Error creating project: {ex.Message}");
                return Page();
            }
            catch (Exception ex)
            {             
                _logger.LogError(ex, "An unexpected error occurred during project creation.");
                ModelState.AddModelError(string.Empty, "An unexpected error occurred. Please try again.");
                return Page();
            }
        }
    }
}
