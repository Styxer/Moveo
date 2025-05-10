using AutoMapper;
using MassTransit;
using MediatR;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Logging;
using MockQueryable;
using Moq;

public class UpdateProjectCommandHandlerTests
{
    private readonly Mock<IRepository<Project>> _mockProjectRepository;
    private readonly Mock<ILogger<UpdateProjectCommandHandler>> _mockLogger;
    private readonly Mock<IMapper> _mockMapper; // Mock AutoMapper
    private readonly Mock<IPublishEndpoint> _mockPublishEndpoint; // Mock the publish endpoint
    private readonly Mock<IDistributedCache> _mockCache; // Mock the distributed cache
    private readonly UpdateProjectCommandHandler _handler;

    private readonly List<Project> _testProjects; // Sample data for tests

    public UpdateProjectCommandHandlerTests()
    {
        _mockProjectRepository = new Mock<IRepository<Project>>();
        _mockLogger = new Mock<ILogger<UpdateProjectCommandHandler>>();
        _mockMapper = new Mock<IMapper>(); // Mock AutoMapper
        _mockPublishEndpoint = new Mock<IPublishEndpoint>();
        _mockCache = new Mock<IDistributedCache>();

        _handler = new UpdateProjectCommandHandler(
            _mockProjectRepository.Object,
            _mockLogger.Object,
            _mockMapper.Object, // Use the mocked mapper
            _mockPublishEndpoint.Object,
            _mockCache.Object);

        // Initialize sample data
        _testProjects = new List<Project>
            {
                new Project { Id = Guid.NewGuid(), Name = "Alpha Project", Description = "Project about Alpha", OwnerId = "user1" },
                new Project { Id = Guid.NewGuid(), Name = "Beta Project", Description = "Project about Beta", OwnerId = "user1" },
                new Project { Id = Guid.NewGuid(), Name = "Gamma Project", Description = "Project about Gamma", OwnerId = "user2" },
            };
    }

    [Fact]
    public async Task Handle_ValidCommand_Owner_UpdatesProjectAndPublishesEvent()
    {
        // Arrange
        var userId = "user1";
        var projectIdToUpdate = _testProjects.First(p => p.OwnerId == userId && p.Name == "Alpha Project").Id;
        var existingProject = _testProjects.First(p => p.Id == projectIdToUpdate);
        var updateDto = new UpdateProjectRequestDto { Name = "Updated Alpha", Description = "Updated Description" };
        var command = new TaskManagement.Application.Commands.Projects.UpdateProjectCommand(projectIdToUpdate, updateDto, userId, isAdmin: false);

        _mockProjectRepository.Setup(r => r.GetByIdAsync(projectIdToUpdate)).ReturnsAsync(existingProject);
        // Mock AsQueryable().AnyAsync() to return false for duplicate name check
        var mockProjectsQueryable = _testProjects.AsQueryable().BuildMock();
        _mockProjectRepository.Setup(r => r.AsQueryable()).Returns(mockProjectsQueryable);
        mockProjectsQueryable.Setup(m => m.AnyAsync(It.IsAny<Expression<Func<Project, bool>>>(), It.IsAny<CancellationToken>())).ReturnsAsync(false);

        // Mock AutoMapper mapping to update the existing object
        _mockMapper.Setup(m => m.Map(updateDto, existingProject));

        // Mock SaveChangesAsync
        _mockProjectRepository.Setup(r => r.SaveChangesAsync()).ReturnsAsync(1);

        // Mock Publish Endpoint (verify it's called)
        _mockPublishEndpoint.Setup(p => p.Publish(It.IsAny<Application.Events.Projects.ProjectUpdatedEvent>(), It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);

        // Mock Cache Remove
        _mockCache.Setup(c => c.RemoveAsync(It.IsAny<string>(), It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);


        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        Assert.Equal(Unit.Value, result); // Update command returns Unit

        _mockProjectRepository.Verify(r => r.GetByIdAsync(projectIdToUpdate), Times.Once);
        _mockProjectRepository.Verify(r => r.AsQueryable(), Times.Once); // Verify validation check
        _mockMapper.Verify(m => m.Map(updateDto, existingProject), Times.Once);
        _mockProjectRepository.Verify(r => r.SaveChangesAsync(), Times.Once);
        _mockCache.Verify(c => c.RemoveAsync($"project_{projectIdToUpdate}", It.IsAny<CancellationToken>()), Times.Once);
        _mockCache.Verify(c => c.RemoveAsync($"projects_user_{userId}", It.IsAny<CancellationToken>()), Times.Once);
        _mockCache.Verify(c => c.RemoveAsync("projects_all", It.IsAny<CancellationToken>()), Times.Once);
        _mockCache.Verify(c => c.RemoveAsync($"tasks_project_{projectIdToUpdate}", It.IsAny<CancellationToken>()), Times.Once);
        _mockPublishEndpoint.Verify(p => p.Publish(It.Is<Application.Events.Projects.ProjectUpdatedEvent>(
            e => e.ProjectId == projectIdToUpdate && e.Name == updateDto.Name && e.Description == updateDto.Description
        ), It.IsAny<CancellationToken>()), Times.Once);

        // Verify the entity was updated (check the original object passed to SaveChangesAsync if possible, or rely on integration tests)
        Assert.Equal(updateDto.Name, existingProject.Name);
        Assert.Equal(updateDto.Description, existingProject.Description);
    }

    [Fact]
    public async Task Handle_ValidCommand_Admin_UpdatesProjectAndPublishesEvent()
    {
        // Arrange
        var userId = "adminUser";
        var projectIdToUpdate = _testProjects.First(p => p.OwnerId == "user1").Id; // Owned by another user
        var existingProject = _testProjects.First(p => p.Id == projectIdToUpdate);
        var updateDto = new UpdateProjectRequestDto { Name = "Updated by Admin", Description = "Admin Desc" };
        var command = new TaskManagement.Application.Commands.Projects.UpdateProjectCommand(projectIdToUpdate, updateDto, userId, isAdmin: true);

        _mockProjectRepository.Setup(r => r.GetByIdAsync(projectIdToUpdate)).ReturnsAsync(existingProject);
        // Mock AsQueryable().AnyAsync() to return false for duplicate name check
        var mockProjectsQueryable = _testProjects.AsQueryable().BuildMock();
        _mockProjectRepository.Setup(r => r.AsQueryable()).Returns(mockProjectsQueryable);
        mockProjectsQueryable.Setup(m => m.AnyAsync(It.IsAny<Expression<Func<Project, bool>>>(), It.IsAny<CancellationToken>())).ReturnsAsync(false);


        // Mock AutoMapper mapping to update the existing object
        _mockMapper.Setup(m => m.Map(updateDto, existingProject));

        // Mock SaveChangesAsync
        _mockProjectRepository.Setup(r => r.SaveChangesAsync()).ReturnsAsync(1);

        // Mock Publish Endpoint (verify it's called)
        _mockPublishEndpoint.Setup(p => p.Publish(It.IsAny<Application.Events.Projects.ProjectUpdatedEvent>(), It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);

        // Mock Cache Remove
        _mockCache.Setup(c => c.RemoveAsync(It.IsAny<string>(), It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);


        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        Assert.Equal(Unit.Value, result); // Update command returns Unit

        _mockProjectRepository.Verify(r => r.GetByIdAsync(projectIdToUpdate), Times.Once);
        _mockProjectRepository.Verify(r => r.AsQueryable(), Times.Once); // Verify validation check
        _mockMapper.Verify(m => m.Map(updateDto, existingProject), Times.Once);
        _mockProjectRepository.Verify(r => r.SaveChangesAsync(), Times.Once);
        _mockCache.Verify(c => c.RemoveAsync($"project_{projectIdToUpdate}", It.IsAny<CancellationToken>()), Times.Once);
        // Admin update should invalidate the owner's cache and all cache
        _mockCache.Verify(c => c.RemoveAsync($"projects_user_{existingProject.OwnerId}", It.IsAny<CancellationToken>()), Times.Once);
        _mockCache.Verify(c => c.RemoveAsync("projects_all", It.IsAny<CancellationToken>()), Times.Once);
        _mockCache.Verify(c => c.RemoveAsync($"tasks_project_{projectIdToUpdate}", It.IsAny<CancellationToken>()), Times.Once);
        _mockPublishEndpoint.Verify(p => p.Publish(It.Is<Application.Events.Projects.ProjectUpdatedEvent>(
            e => e.ProjectId == projectIdToUpdate && e.Name == updateDto.Name && e.Description == updateDto.Description
        ), It.IsAny<CancellationToken>()), Times.Once);

        // Verify the entity was updated
        Assert.Equal(updateDto.Name, existingProject.Name);
        Assert.Equal(updateDto.Description, existingProject.Description);
    }

    [Fact]
    public async Task Handle_NonOwnerNonAdmin_ThrowsUnauthorizedAccessException()
    {
        // Arrange
        var userId = "user3"; // Neither owner nor admin
        var projectIdToUpdate = _testProjects.First(p => p.OwnerId == "user1").Id;
        var existingProject = _testProjects.First(p => p.Id == projectIdToUpdate);
        var updateDto = new UpdateProjectRequestDto { Name = "New Name", Description = "New Desc" };
        var command = new TaskManagement.Application.Commands.Projects.UpdateProjectCommand(projectIdToUpdate, updateDto, userId, isAdmin: false);

        _mockProjectRepository.Setup(r => r.GetByIdAsync(projectIdToUpdate)).ReturnsAsync(existingProject);

        // Act & Assert
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            _handler.Handle(command, CancellationToken.None));

        _mockProjectRepository.Verify(r => r.GetByIdAsync(projectIdToUpdate), Times.Once);
        _mockProjectRepository.Verify(r => r.AsQueryable(), Times.Never); // Validation check should not happen if auth fails first
        _mockMapper.Verify(m => m.Map(It.IsAny<UpdateProjectRequestDto>(), It.IsAny<Project>()), Times.Never); // Mapping should not happen
        _mockProjectRepository.Verify(r => r.SaveChangesAsync(), Times.Never); // Save should not happen
        _mockCache.Verify(c => c.RemoveAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never); // Cache should not be touched
        _mockPublishEndpoint.Verify(p => p.Publish(It.IsAny<Application.Events.Projects.ProjectUpdatedEvent>(), It.IsAny<CancellationToken>()), Times.Never); // Event should not be published
    }

    // Add more tests for UpdateProjectCommandHandler covering other scenarios...

}