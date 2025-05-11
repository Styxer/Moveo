
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using TaskManagement.Application.DTOs.Auth; 
using TaskManagement.Application.DTOs.Projects;
using TaskManagement.Application.DTOs.Tasks; 
using TaskManagement.Application.DTOs.Pagination; 

namespace TaskManagement.Frontend.Services
{
    public class ApiClient
    {
        private readonly HttpClient _httpClient;
        private readonly ILogger<ApiClient> _logger;
        private readonly IHttpContextAccessor _httpContextAccessor; 

        public ApiClient(HttpClient httpClient, ILogger<ApiClient> logger, IHttpContextAccessor httpContextAccessor)
        {
            _httpClient = httpClient;
            _logger = logger;
            _httpContextAccessor = httpContextAccessor;       
        }

      
        private string? GetJwtToken()
        {            
            return _httpContextAccessor.HttpContext?.Session.GetString("JwtToken");
        }

       
        private void AddAuthorizationHeader()
        {
            var token = GetJwtToken();
            if (!string.IsNullOrEmpty(token))
            {
                _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
            }
            else
            {               
                _httpClient.DefaultRequestHeaders.Authorization = null;
            }
        }

        // --- Authentication Endpoints ---

        public async Task<AuthResponseDto?> LoginAsync(LoginRequestDto loginRequest)
        {
            var jsonContent = new StringContent(JsonSerializer.Serialize(loginRequest), Encoding.UTF8, "application/json");
            var response = await _httpClient.PostAsync("/api/auth/login", jsonContent);

            if (response.IsSuccessStatusCode)
            {
                var content = await response.Content.ReadAsStringAsync();
                return JsonSerializer.Deserialize<AuthResponseDto>(content, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
            }
            else if (response.StatusCode == System.Net.HttpStatusCode.Unauthorized || response.StatusCode == System.Net.HttpStatusCode.Forbidden)
            {               
                _logger.LogWarning("Login failed: {StatusCode}", response.StatusCode);
                return null;
            }
            else
            {                
                _logger.LogError("API Error during login: {StatusCode}", response.StatusCode);
                throw new HttpRequestException($"API Error: {response.StatusCode}");
            }
        }

        // --- Project Endpoints ---

        public async Task<PagedResultDto<ProjectDto>?> GetProjectsAsync(ProjectQueryParameters queryParameters)
        {
            AddAuthorizationHeader();             
            var queryString = $"?pageNumber={queryParameters.PageNumber}&pageSize={queryParameters.PageSize}";
            if (!string.IsNullOrEmpty(queryParameters.SearchQuery))
            {
                queryString += $"&searchQuery={Uri.EscapeDataString(queryParameters.SearchQuery)}";
            }
            if (!string.IsNullOrEmpty(queryParameters.SortBy))
            {
                queryString += $"&sortBy={Uri.EscapeDataString(queryParameters.SortBy)}";
            }
            if (!string.IsNullOrEmpty(queryParameters.SortOrder))
            {
                queryString += $"&sortOrder={Uri.EscapeDataString(queryParameters.SortOrder)}";
            }

            var response = await _httpClient.GetAsync($"/api/projects{queryString}");

            if (response.IsSuccessStatusCode)
            {
                var content = await response.Content.ReadAsStringAsync();
                return JsonSerializer.Deserialize<PagedResultDto<ProjectDto>>(content, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
            }
            else if (response.StatusCode == System.Net.HttpStatusCode.Unauthorized || response.StatusCode == System.Net.HttpStatusCode.Forbidden)
            {              
                _logger.LogWarning("Access denied to get projects: {StatusCode}", response.StatusCode);               
                return null;
            }
            else if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
            {
                _logger.LogWarning("Projects endpoint not found: {StatusCode}", response.StatusCode);
                return null; 
            }
            else
            {
                _logger.LogError("API Error during get projects: {StatusCode}", response.StatusCode);
                throw new HttpRequestException($"API Error: {response.StatusCode}");
            }
        }

        public async Task<ProjectDto?> GetProjectByIdAsync(Guid id)
        {
            AddAuthorizationHeader(); 
            var response = await _httpClient.GetAsync($"/api/projects/{id}");

            if (response.IsSuccessStatusCode)
            {
                var content = await response.Content.ReadAsStringAsync();
                return JsonSerializer.Deserialize<ProjectDto>(content, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
            }
            else if (response.StatusCode == System.Net.HttpStatusCode.Unauthorized || response.StatusCode == System.Net.HttpStatusCode.Forbidden)
            {
                _logger.LogWarning("Access denied to get project {ProjectId}: {StatusCode}", id, response.StatusCode);
                return null;
            }
            else if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
            {
                _logger.LogWarning("Project {ProjectId} not found: {StatusCode}", id, response.StatusCode);
                return null;
            }
            else
            {
                _logger.LogError("API Error during get project {ProjectId}: {StatusCode}", id, response.StatusCode);
                throw new HttpRequestException($"API Error: {response.StatusCode}");
            }
        }

        public async Task<ProjectDto?> CreateProjectAsync(CreateProjectRequestDto createRequest)
        {
            AddAuthorizationHeader(); 
            var jsonContent = new StringContent(JsonSerializer.Serialize(createRequest), Encoding.UTF8, "application/json");
            var response = await _httpClient.PostAsync("/api/projects", jsonContent);

            if (response.IsSuccessStatusCode)
            {
                var content = await response.Content.ReadAsStringAsync();
                return JsonSerializer.Deserialize<ProjectDto>(content, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
            }
            else if (response.StatusCode == System.Net.HttpStatusCode.Unauthorized || response.StatusCode == System.Net.HttpStatusCode.Forbidden)
            {
                _logger.LogWarning("Access denied to create project: {StatusCode}", response.StatusCode);
                return null;
            }
            else if (response.StatusCode == System.Net.HttpStatusCode.BadRequest || response.StatusCode == System.Net.HttpStatusCode.Conflict)
            {     
                var errorContent = await response.Content.ReadAsStringAsync();
                _logger.LogWarning("API Error during create project: {StatusCode} - {Error}", response.StatusCode, errorContent);             
                throw new HttpRequestException($"API Error: {response.StatusCode} - {errorContent}");
            }
            else
            {
                _logger.LogError("API Error during create project: {StatusCode}", response.StatusCode);
                throw new HttpRequestException($"API Error: {response.StatusCode}");
            }
        }

        public async Task<bool> UpdateProjectAsync(Guid id, UpdateProjectRequestDto updateRequest)
        {
            AddAuthorizationHeader(); 
            var jsonContent = new StringContent(JsonSerializer.Serialize(updateRequest), Encoding.UTF8, "application/json");
            var response = await _httpClient.PutAsync($"/api/projects/{id}", jsonContent);

            if (response.IsSuccessStatusCode)
            {
                return true; 
            }
            else if (response.StatusCode == System.Net.HttpStatusCode.Unauthorized || response.StatusCode == System.Net.HttpStatusCode.Forbidden)
            {
                _logger.LogWarning("Access denied to update project {ProjectId}: {StatusCode}", id, response.StatusCode);
                return false;
            }
            else if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
            {
                _logger.LogWarning("Project {ProjectId} not found for update: {StatusCode}", id, response.StatusCode);
                return false;
            }
            else if (response.StatusCode == System.Net.HttpStatusCode.BadRequest || response.StatusCode == System.Net.HttpStatusCode.Conflict)
            {                
                var errorContent = await response.Content.ReadAsStringAsync();
                _logger.LogWarning("API Error during update project {ProjectId}: {StatusCode} - {Error}", id, response.StatusCode, errorContent);
                throw new HttpRequestException($"API Error: {response.StatusCode} - {errorContent}");
            }
            else
            {
                _logger.LogError("API Error during update project {ProjectId}: {StatusCode}", id, response.StatusCode);
                throw new HttpRequestException($"API Error: {response.StatusCode}");
            }
        }

        public async Task<bool> DeleteProjectAsync(Guid id)
        {
            AddAuthorizationHeader(); 
            var response = await _httpClient.DeleteAsync($"/api/projects/{id}");

            if (response.IsSuccessStatusCode)
            {
                return true; 
            }
            else if (response.StatusCode == System.Net.HttpStatusCode.Unauthorized || response.StatusCode == System.Net.HttpStatusCode.Forbidden)
            {
                _logger.LogWarning("Access denied to delete project {ProjectId}: {StatusCode}", id, response.StatusCode);
                return false;
            }
            else if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
            {
                _logger.LogWarning("Project {ProjectId} not found for deletion: {StatusCode}", id, response.StatusCode);
                return false;
            }
            else
            {
                _logger.LogError("API Error during delete project {ProjectId}: {StatusCode}", id, response.StatusCode);
                throw new HttpRequestException($"API Error: {response.StatusCode}");
            }
        }

        // --- Task Endpoints ---

        public async Task<PagedResultDto<TaskDto>?> GetTasksByProjectIdAsync(Guid projectId, TaskQueryParameters queryParameters)
        {
            AddAuthorizationHeader(); \            
            var queryString = $"?pageNumber={queryParameters.PageNumber}&pageSize={queryParameters.PageSize}";
            if (!string.IsNullOrEmpty(queryParameters.SearchQuery))
            {
                queryString += $"&searchQuery={Uri.EscapeDataString(queryParameters.SearchQuery)}";
            }
            if (!string.IsNullOrEmpty(queryParameters.SortBy))
            {
                queryString += $"&sortBy={Uri.EscapeDataString(queryParameters.SortBy)}";
            }
            if (!string.IsNullOrEmpty(queryParameters.SortOrder))
            {
                queryString += $"&sortOrder={Uri.EscapeDataString(queryParameters.SortOrder)}";
            }

            var response = await _httpClient.GetAsync($"/api/projects/{projectId}/tasks{queryString}");

            if (response.IsSuccessStatusCode)
            {
                var content = await response.Content.ReadAsStringAsync();
                return JsonSerializer.Deserialize<PagedResultDto<TaskDto>>(content, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
            }
            else if (response.StatusCode == System.Net.HttpStatusCode.Unauthorized || response.StatusCode == System.Net.HttpStatusCode.Forbidden)
            {
                _logger.LogWarning("Access denied to get tasks for project {ProjectId}: {StatusCode}", projectId, response.StatusCode);
                return null;
            }
            else if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
            {
                _logger.LogWarning("Project {ProjectId} not found for tasks: {StatusCode}", projectId, response.StatusCode);
                return null; 
            }
            else
            {
                _logger.LogError("API Error during get tasks for project {ProjectId}: {StatusCode}", projectId, response.StatusCode);
                throw new HttpRequestException($"API Error: {response.StatusCode}");
            }
        }

        public async Task<TaskDto?> GetTaskByIdAsync(Guid taskId)
        {
            AddAuthorizationHeader();
            var response = await _httpClient.GetAsync($"/api/tasks/{taskId}"); 

            if (response.IsSuccessStatusCode)
            {
                var content = await response.Content.ReadAsStringAsync();
                return JsonSerializer.Deserialize<TaskDto>(content, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
            }
            else if (response.StatusCode == System.Net.HttpStatusCode.Unauthorized || response.StatusCode == System.Net.HttpStatusCode.Forbidden)
            {
                _logger.LogWarning("Access denied to get task {TaskId}: {StatusCode}", taskId, response.StatusCode);
                return null;
            }
            else if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
            {
                _logger.LogWarning("Task {TaskId} not found: {StatusCode}", taskId, response.StatusCode);
                return null;
            }
            else
            {
                _logger.LogError("API Error during get task {TaskId}: {StatusCode}", taskId, response.StatusCode);
                throw new HttpRequestException($"API Error: {response.StatusCode}");
            }
        }

        public async Task<TaskDto?> CreateTaskAsync(Guid projectId, CreateTaskRequestDto createRequest)
        {
            AddAuthorizationHeader(); 
            var jsonContent = new StringContent(JsonSerializer.Serialize(createRequest), Encoding.UTF8, "application/json");
            var response = await _httpClient.PostAsync($"/api/projects/{projectId}/tasks", jsonContent);

            if (response.IsSuccessStatusCode)
            {
                var content = await response.Content.ReadAsStringAsync();
                return JsonSerializer.Deserialize<TaskDto>(content, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
            }
            else if (response.StatusCode == System.Net.HttpStatusCode.Unauthorized || response.StatusCode == System.Net.HttpStatusCode.Forbidden)
            {
                _logger.LogWarning("Access denied to create task for project {ProjectId}: {StatusCode}", projectId, response.StatusCode);
                return null;
            }
            else if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
            {
                _logger.LogWarning("Project {ProjectId} not found for task creation: {StatusCode}", projectId, response.StatusCode);
                return null;
            }
            else if (response.StatusCode == System.Net.HttpStatusCode.BadRequest || response.StatusCode == System.Net.HttpStatusCode.Conflict)
            {               
                var errorContent = await response.Content.ReadAsStringAsync();
                _logger.LogWarning("API Error during create task for project {ProjectId}: {StatusCode} - {Error}", projectId, response.StatusCode, errorContent);
                throw new HttpRequestException($"API Error: {response.StatusCode} - {errorContent}");
            }
            else
            {
                _logger.LogError("API Error during create task for project {ProjectId}: {StatusCode}", projectId, response.StatusCode);
                throw new HttpRequestException($"API Error: {response.StatusCode}");
            }
        }

        public async Task<bool> UpdateTaskAsync(Guid taskId, UpdateTaskRequestDto updateRequest)
        {
            AddAuthorizationHeader();
            var jsonContent = new StringContent(JsonSerializer.Serialize(updateRequest), Encoding.UTF8, "application/json");
            var response = await _httpClient.PutAsync($"/api/tasks/{taskId}", jsonContent); 

            if (response.IsSuccessStatusCode)
            {
                return true; 
            }
            else if (response.StatusCode == System.Net.HttpStatusCode.Unauthorized || response.StatusCode == System.Net.HttpStatusCode.Forbidden)
            {
                _logger.LogWarning("Access denied to update task {TaskId}: {StatusCode}", taskId, response.StatusCode);
                return false;
            }
            else if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
            {
                _logger.LogWarning("Task {TaskId} not found for update: {StatusCode}", taskId, response.StatusCode);
                return false;
            }
            else if (response.StatusCode == System.Net.HttpStatusCode.BadRequest || response.StatusCode == System.Net.HttpStatusCode.Conflict)
            {               
                var errorContent = await response.Content.ReadAsStringAsync();
                _logger.LogWarning("API Error during update task {TaskId}: {StatusCode} - {Error}", taskId, response.StatusCode, errorContent);
                throw new HttpRequestException($"API Error: {response.StatusCode} - {errorContent}");
            }
            else
            {
                _logger.LogError("API Error during update task {TaskId}: {StatusCode}", taskId, response.StatusCode);
                throw new HttpRequestException($"API Error: {response.StatusCode}");
            }
        }

        public async Task<bool> DeleteTaskAsync(Guid taskId)
        {
            AddAuthorizationHeader(); 
            var response = await _httpClient.DeleteAsync($"/api/tasks/{taskId}"); // Note the global task route

            if (response.IsSuccessStatusCode)
            {
                return true; 
            }
            else if (response.StatusCode == System.Net.HttpStatusCode.Unauthorized || response.StatusCode == System.Net.HttpStatusCode.Forbidden)
            {
                _logger.LogWarning("Access denied to delete task {TaskId}: {StatusCode}", taskId, response.StatusCode);
                return false;
            }
            else if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
            {
                _logger.LogWarning("Task {TaskId} not found for deletion: {StatusCode}", taskId, response.StatusCode);
                return false;
            }
            else
            {
                _logger.LogError("API Error during delete task {TaskId}: {StatusCode}", taskId, response.StatusCode);
                throw new HttpRequestException($"API Error: {response.StatusCode}");
            }
        }
    }
}

