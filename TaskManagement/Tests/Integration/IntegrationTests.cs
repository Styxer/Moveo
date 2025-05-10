using Application.DTOs.Pagination;
using Application.DTOs.Projects;
using Infrastructure.Data;
using TaskManagement.Tests;

namespace Tests.Integration;

public class IntegrationTests : IClassFixture<CustomWebApplicationFactory>, IAsyncLifetime
{
    private readonly CustomWebApplicationFactory _factory;
    private readonly HttpClient _client;

    public IntegrationTests(CustomWebApplicationFactory factory)
    {
        _factory = factory;
        _client = _factory.CreateClient(); 
    }


    public async Task InitializeAsync()
    {
       
        await _factory.ResetDatabaseAsync();


        using (var scope = _factory.Services.CreateScope())
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            _factory.SeedData(dbContext);
        }

    
        using (var scope = _factory.Services.CreateScope())
        {
            var cache = scope.ServiceProvider.GetRequiredService<IDistributedCache>();

        }
    }

   
    public Task DisposeAsync()
    {
      //TODO: CLEAN RESOURCES
        return Task.CompletedTask;
    }


    [Fact]
    public async Task GetProjects_ReturnsProjectsForAuthenticatedUser()
    {
        

        // Act
        var response = await _client.GetAsync("/api/projects");

        // Assert
        response.EnsureSuccessStatusCode(); // Status Code 200-299
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var responseString = await response.Content.ReadAsStringAsync();
        var pagedResult = JsonSerializer.Deserialize<PagedResultDto<ProjectDto>>(responseString, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

    
        Assert.NotNull(pagedResult);
        Assert.Equal(2, pagedResult.TotalCount); // Should only see projects owned by test_user_id
        Assert.Equal(2, pagedResult.Items.Count());
        Assert.True(pagedResult.Items.All(p => p.Name.StartsWith("User 1 Project"))); // Verify correct projects are returned
    }

    [Fact]
    public async Task GetProjectById_OwnedProject_ReturnsProject()
    {
        // Arrange
        Guid ownedProjectId;
        using (var scope = _factory.Services.CreateScope())
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            ownedProjectId = dbContext.Projects.First(p => p.OwnerId == "test_user_id").Id;
        }

        // Act
        var response = await _client.GetAsync($"/api/projects/{ownedProjectId}");

        // Assert
        response.EnsureSuccessStatusCode(); // Status Code 200-299
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var responseString = await response.Content.ReadAsStringAsync();
        var project = JsonSerializer.Deserialize<ProjectDto>(responseString, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

        Assert.NotNull(project);
        Assert.Equal(ownedProjectId, project.Id);
        Assert.StartsWith("User 1 Project", project.Name);
    }

    [Fact]
    public async Task GetProjectById_NotOwnedProject_ReturnsForbidden()
    {
        // Arrange
        Guid notOwnedProjectId;
        using (var scope = _factory.Services.CreateScope())
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            // Get the ID of a project NOT owned by the test user
            notOwnedProjectId = dbContext.Projects.First(p => p.OwnerId != "test_user_id").Id;
        }

        // Act
        var response = await _client.GetAsync($"/api/projects/{notOwnedProjectId}");

        // Assert
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode); // Should be 403 Forbidden
    }

    [Fact]
    public async Task CreateProject_ValidData_CreatesProject()
    {
        // Arrange
        var createDto = new CreateProjectRequestDto
        {
            Name = "New Test Project",
            Description = "This is a new project created via integration test."
        };
        var jsonContent = new StringContent(JsonSerializer.Serialize(createDto), Encoding.UTF8, "application/json");

        // Act
        var response = await _client.PostAsync("/api/projects", jsonContent);

        // Assert
        response.EnsureSuccessStatusCode(); // Status Code 200-299
        Assert.Equal(HttpStatusCode.Created, response.StatusCode); // Should be 201 Created

        var responseString = await response.Content.ReadAsStringAsync();
        var createdProject = JsonSerializer.Deserialize<ProjectDto>(responseString, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

        Assert.NotNull(createdProject);
        Assert.Equal(createDto.Name, createdProject.Name);
        Assert.Equal(createDto.Description, createdProject.Description);
        Assert.NotEqual(Guid.Empty, createdProject.Id); // Ensure an ID was generated

        // Verify the project was actually added to the database
        using var scope = _factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var projectInDb = await dbContext.Projects.FindAsync(createdProject.Id);
        Assert.NotNull(projectInDb);
        Assert.Equal(createDto.Name, projectInDb.Name);
        Assert.Equal("test_user_id", projectInDb.OwnerId); // Verify owner is the authenticated test user
    }

    [Fact]
    public async Task CreateProject_DuplicateNameForUser_ReturnsConflict()
    {
        // Arrange
        string existingProjectName;
        using (var scope = _factory.Services.CreateScope())
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            existingProjectName = dbContext.Projects.First(p => p.OwnerId == "test_user_id").Name;
        }

        var createDto = new CreateProjectRequestDto
        {
            Name = existingProjectName, 
            Description = "This should fail."
        };
        var jsonContent = new StringContent(JsonSerializer.Serialize(createDto), Encoding.UTF8, "application/json");

        // Act
        var response = await _client.PostAsync("/api/projects", jsonContent);

        // Assert
        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode); // Should be 409 Conflict
        var responseString = await response.Content.ReadAsStringAsync();
        Assert.Contains($"A project with the name '{existingProjectName}' already exists for this user.", responseString);
    }

    // TODO: more integration tests

}