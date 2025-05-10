
public class IntegrationTests : IClassFixture<CustomWebApplicationFactory>, IAsyncLifetime
{
    private readonly CustomWebApplicationFactory _factory;
    private readonly HttpClient _client;

    public IntegrationTests(CustomWebApplicationFactory factory)
    {
        _factory = factory;
        _client = _factory.CreateClient(); // Create an HttpClient to interact with the test host
    }

    // Use IAsyncLifetime on the test class to manage database resetting before each test method
    // This method runs before *each* test method in this class
    public async Task InitializeAsync()
    {
        // Reset the database to a clean state before each test method using Respawn
        await _factory.ResetDatabaseAsync();

        // Seed the database with test data after resetting
        using (var scope = _factory.Services.CreateScope())
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            _factory.SeedData(dbContext);
        }

        // Optional: Clear the Redis cache before each test method
        using (var scope = _factory.Services.CreateScope())
        {
            var cache = scope.ServiceProvider.GetRequiredService<IDistributedCache>();
            // Note: Clearing the entire Redis cache in integration tests can be tricky
            // if multiple test fixtures or processes are using the same Redis instance.
            // A safer approach is to use unique keys per test or per fixture, or
            // rely on cache expiration. For simplicity here, we'll just note it.
            // await cache.RemoveAsync("some_key"); // Example
        }
    }

    // This method runs after *all* test methods in this class have run
    public Task DisposeAsync()
    {
        // Clean up resources if needed after all tests in this fixture run
        // The factory's DisposeAsync handles the container cleanup
        return Task.CompletedTask;
    }


    [Fact]
    public async Task GetProjects_ReturnsProjectsForAuthenticatedUser()
    {
        // Arrange - Database is reset and seeded in InitializeAsync

        // Act
        var response = await _client.GetAsync("/api/projects");

        // Assert
        response.EnsureSuccessStatusCode(); // Status Code 200-299
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var responseString = await response.Content.ReadAsStringAsync();
        var pagedResult = JsonSerializer.Deserialize<PagedResultDto<ProjectDto>>(responseString, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

        // The TestAuthHandler uses "test_user_id". We seeded 2 projects for this user.
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
            // Get the ID of a project owned by the test user
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
        using (var scope = _factory.Services.CreateScope())
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var projectInDb = await dbContext.Projects.FindAsync(createdProject.Id);
            Assert.NotNull(projectInDb);
            Assert.Equal(createDto.Name, projectInDb.Name);
            Assert.Equal("test_user_id", projectInDb.OwnerId); // Verify owner is the authenticated test user
        }
    }

    [Fact]
    public async Task CreateProject_DuplicateNameForUser_ReturnsConflict()
    {
        // Arrange
        string existingProjectName;
        using (var scope = _factory.Services.CreateScope())
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            // Get the name of an existing project owned by the test user
            existingProjectName = dbContext.Projects.First(p => p.OwnerId == "test_user_id").Name;
        }

        var createDto = new CreateProjectRequestDto
        {
            Name = existingProjectName, // Use a duplicate name
            Description = "This should fail."
        };
        var jsonContent = new StringContent(JsonSerializer.Serialize(createDto), Encoding.UTF8, "application/json");

        // Act
        var response = await _client.PostAsync("/api/projects", jsonContent);

        // Assert
        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode); // Should be 409 Conflict
                                                                    // Optional: Assert the error message content
        var responseString = await response.Content.ReadAsStringAsync();
        Assert.Contains($"A project with the name '{existingProjectName}' already exists for this user.", responseString);
    }

    // Add more integration tests for:
    // - PUT /api/projects/{id} (owned, not owned, non-existent, duplicate name)
    // - DELETE /api/projects/{id} (owned, not owned, non-existent)
    // - GET /api/projects/{projectId}/tasks (owned project, not owned project, non-existent project)
    // - GET /api/tasks/{taskId} (task in owned project, task in not owned project, non-existent task)
    // - POST /api/projects/{projectId}/tasks (owned project, not owned project, non-existent project, validation)
    // - PUT /api/tasks/{taskId} (task in owned project, task in not owned project, non-existent task, validation)
    // - DELETE /api/tasks/{taskId} (task in owned project, task in not owned project, non-existent task)
    // - Test pagination, filtering, sorting on GET /api/projects and GET /api/projects/{projectId}/tasks
    // - Test authentication failure for unauthorized requests
    // - Test validation errors from FluentValidation (should return 400 Bad Request)
    // - Test caching behavior (optional, can be tricky in integration tests)
    // - Test Outbox pattern (optional, requires polling Outbox tables or using test consumers)
}