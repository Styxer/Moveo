using Application.Commands.Projects;
using Application.DTOs.Projects;
using Application.Interfaces;
using Domain.Models;
using Infrastructure.Handlers.Projects;
using MockQueryable;
using Task = System.Threading.Tasks.Task;

namespace Tests.UnitTest;

public class UpdateProjectCommandHandlerTests
{
    private readonly Mock<IRepository<Project>> _mockProjectRepository;
    private readonly Mock<IMapper> _mockMapper; 
    private readonly Mock<IPublishEndpoint> _mockPublishEndpoint;
    private readonly Mock<IDistributedCache> _mockCache; 
    private readonly UpdateProjectCommandHandler _handler;

    private readonly List<Project> _testProjects; 

    public UpdateProjectCommandHandlerTests()
    {
        _mockProjectRepository = new Mock<IRepository<Project>>();
        Mock<ILogger<UpdateProjectCommandHandler>> mockLogger = new();
        _mockMapper = new Mock<IMapper>();
        _mockPublishEndpoint = new Mock<IPublishEndpoint>();
        _mockCache = new Mock<IDistributedCache>();

        _handler = new UpdateProjectCommandHandler(
            _mockProjectRepository.Object,
            mockLogger.Object,
            _mockMapper.Object,
            _mockPublishEndpoint.Object,
            _mockCache.Object);

        // Initialize sample data
        _testProjects =
        [
            new Project
            {
                Id = Guid.NewGuid(), 
                Name = "Alpha Project",
                Description = "Project about Alpha",
                OwnerId = "user1"
            },
            new Project
            {
                Id = Guid.NewGuid(),
                Name = "Beta Project", 
                Description = "Project about Beta",
                OwnerId = "user1"
            },
            new Project
            {
                Id = Guid.NewGuid(),
                Name = "Gamma Project", 
                Description = "Project about Gamma",
                OwnerId = "user2"
            }
        ];
    }

    [Fact]
    public async Task Handle_ValidCommand_Owner_UpdatesProjectAndPublishesEvent()
    {
        // Arrange
        var userId = "user1";
        var projectIdToUpdate = _testProjects.First(p => p.OwnerId == userId && p.Name == "Alpha Project").Id;
        var existingProject = _testProjects.First(p => p.Id == projectIdToUpdate);
        var updateDto = new UpdateProjectRequestDto { Name = "Updated Alpha", Description = "Updated Description" };
        var command = new UpdateProjectCommand(projectIdToUpdate, updateDto, userId, isAdmin: false);

        _mockProjectRepository.Setup(r => r.GetByIdAsync(projectIdToUpdate)).ReturnsAsync(existingProject);
      
        var mockProjectsQueryable = _testProjects.AsQueryable().BuildMock();
        _mockProjectRepository.Setup(r => r.AsQueryable()).Returns(mockProjectsQueryable);
        //mockProjectsQueryable.Setup(m => m.AnyAsync(It.IsAny<Expression<Func<Project, bool>>>(), It.IsAny<CancellationToken>())).ReturnsAsync(false);


        _mockMapper.Setup(m => m.Map(updateDto, existingProject));

     
        _mockProjectRepository.Setup(r => r.SaveChangesAsync()).ReturnsAsync(1);

        _mockPublishEndpoint.Setup(p => p.Publish(It.IsAny<Application.Events.Projects.ProjectUpdatedEvent>(), It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
      
        _mockCache.Setup(c => c.RemoveAsync(It.IsAny<string>(), It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);


        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        Assert.Equal(Unit.Value, result); 

        _mockProjectRepository.Verify(r => r.GetByIdAsync(projectIdToUpdate), Times.Once);
        _mockProjectRepository.Verify(r => r.AsQueryable(), Times.Once);
        _mockMapper.Verify(m => m.Map(updateDto, existingProject), Times.Once);
        _mockProjectRepository.Verify(r => r.SaveChangesAsync(), Times.Once);
        _mockCache.Verify(c => c.RemoveAsync($"project_{projectIdToUpdate}", It.IsAny<CancellationToken>()), Times.Once);
        _mockCache.Verify(c => c.RemoveAsync($"projects_user_{userId}", It.IsAny<CancellationToken>()), Times.Once);
        _mockCache.Verify(c => c.RemoveAsync("projects_all", It.IsAny<CancellationToken>()), Times.Once);
        _mockCache.Verify(c => c.RemoveAsync($"tasks_project_{projectIdToUpdate}", It.IsAny<CancellationToken>()), Times.Once);
        _mockPublishEndpoint.Verify(p => p.Publish(It.Is<Application.Events.Projects.ProjectUpdatedEvent>(
            e => e.ProjectId == projectIdToUpdate && e.Name == updateDto.Name && e.Description == updateDto.Description
        ), It.IsAny<CancellationToken>()), Times.Once);

       
        Assert.Equal(updateDto.Name, existingProject.Name);
        Assert.Equal(updateDto.Description, existingProject.Description);
    }

    [Fact]
    public async Task Handle_ValidCommand_Admin_UpdatesProjectAndPublishesEvent()
    {
        // Arrange
        var userId = "adminUser";
        var projectIdToUpdate = _testProjects.First(p => p.OwnerId == "user1").Id;
        var existingProject = _testProjects.First(p => p.Id == projectIdToUpdate);
        var updateDto = new UpdateProjectRequestDto { Name = "Updated by Admin", Description = "Admin Desc" };
        var command = new UpdateProjectCommand(projectIdToUpdate, updateDto, userId, isAdmin: true);

        _mockProjectRepository.Setup(r => r.GetByIdAsync(projectIdToUpdate)).ReturnsAsync(existingProject);
   
        var mockProjectsQueryable = _testProjects.AsQueryable().BuildMock();
        _mockProjectRepository.Setup(r => r.AsQueryable()).Returns(mockProjectsQueryable);
       // mockProjectsQueryable.Setup(m => m.AnyAsync(It.IsAny<Expression<Func<Project, bool>>>(), It.IsAny<CancellationToken>())).ReturnsAsync(false);
       
        _mockMapper.Setup(m => m.Map(updateDto, existingProject));
        _mockProjectRepository.Setup(r => r.SaveChangesAsync()).ReturnsAsync(1);
        _mockPublishEndpoint.Setup(p => p.Publish(It.IsAny<Application.Events.Projects.ProjectUpdatedEvent>(), It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
        _mockCache.Setup(c => c.RemoveAsync(It.IsAny<string>(), It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
        
        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        Assert.Equal(Unit.Value, result); 

        _mockProjectRepository.Verify(r => r.GetByIdAsync(projectIdToUpdate), Times.Once);
        _mockProjectRepository.Verify(r => r.AsQueryable(), Times.Once);
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
        var command = new UpdateProjectCommand(projectIdToUpdate, updateDto, userId, isAdmin: false);

        _mockProjectRepository.Setup(r => r.GetByIdAsync(projectIdToUpdate)).ReturnsAsync(existingProject);

        // Act & Assert
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => _handler.Handle(command, CancellationToken.None));

        _mockProjectRepository.Verify(r => r.GetByIdAsync(projectIdToUpdate), Times.Once);
        _mockProjectRepository.Verify(r => r.AsQueryable(), Times.Never); 
        _mockMapper.Verify(m => m.Map(It.IsAny<UpdateProjectRequestDto>(), It.IsAny<Project>()), Times.Never);
        _mockProjectRepository.Verify(r => r.SaveChangesAsync(), Times.Never); // Save should not happen
        _mockCache.Verify(c => c.RemoveAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
        _mockPublishEndpoint.Verify(p => p.Publish(It.IsAny<Application.Events.Projects.ProjectUpdatedEvent>(), It.IsAny<CancellationToken>()), Times.Never); 
    }

    // TODO :more tests for UpdateProjectCommandHandler covering other scenarios...

}