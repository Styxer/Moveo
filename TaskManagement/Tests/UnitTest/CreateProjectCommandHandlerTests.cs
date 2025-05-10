

using Application.Commands.Projects;
using Application.DTOs.Projects;
using Application.Interfaces;
using Domain.Models;
using Infrastructure.Handlers.Projects;
using MockQueryable;
using Task = System.Threading.Tasks.Task;

namespace Tests.UnitTest
{
    // Note: We are now testing the Command Handlers directly
    public class CreateProjectCommandHandlerTests
    {
        private readonly Mock<IRepository<Project>> _mockProjectRepository;

        private readonly Mock<ILogger<CreateProjectCommandHandler>> _mockLogger;

        // Mock AutoMapper's IMapper
        private readonly Mock<IMapper> _mockMapper;
        private readonly Mock<IPublishEndpoint> _mockPublishEndpoint; // Mock the publish endpoint
        private readonly Mock<IDistributedCache> _mockCache; // Mock the distributed cache
        private readonly CreateProjectCommandHandler _handler;

        private readonly List<Project> _testProjects; // Sample data for tests

        public CreateProjectCommandHandlerTests()
        {
            _mockProjectRepository = new Mock<IRepository<Project>>();
            _mockLogger = new Mock<ILogger<CreateProjectCommandHandler>>();
            _mockMapper = new Mock<IMapper>(); // Mock AutoMapper
            _mockPublishEndpoint = new Mock<IPublishEndpoint>();
            _mockCache = new Mock<IDistributedCache>();

            _handler = new CreateProjectCommandHandler(
                _mockProjectRepository.Object,
                _mockLogger.Object,
                _mockMapper.Object, // Use the mocked mapper
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
        public async Task Handle_ValidCommand_CreatesProjectAndPublishesEvent()
        {
            // Arrange
            var projectDto = new CreateProjectRequestDto { Name = "New Project", Description = "Description" };
            var ownerId = "newUser";
            var command = new CreateProjectCommand(projectDto, ownerId);
            var createdProject = new Project
            {
                Name = "New Project", Description = "Description", OwnerId = ownerId, Id = Guid.NewGuid()
            }; // Simulate created entity
            var createdProjectDto = new ProjectDto
                { Name = "New Project", Description = "Description", Id = createdProject.Id }; // Simulate mapped DTO


            var mockProjectsQueryable = _testProjects.AsQueryable().BuildMock();
            _mockProjectRepository.Setup(r => r.AsQueryable()).Returns(mockProjectsQueryable);
            //  mockProjectsQueryable.Setup(m => m.AnyAsync(It.IsAny<Expression<Func<Project, bool>>>(), It.IsAny<CancellationToken>())).ReturnsAsync(false);


            _mockMapper.Setup(m => m.Map<Project>(projectDto)).Returns(createdProject);
            _mockMapper.Setup(m => m.Map<ProjectDto>(createdProject)).Returns(createdProjectDto);


            _mockProjectRepository.Setup(r => r.SaveChangesAsync()).ReturnsAsync(1);


            _mockPublishEndpoint
                .Setup(p => p.Publish(It.IsAny<Application.Events.Projects.ProjectCreatedEvent>(),
                    It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);


            _mockCache.Setup(c => c.RemoveAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .Returns(Task.CompletedTask);


            // Act
            var result = await _handler.Handle(command, CancellationToken.None);

            // Assert
            Assert.NotNull(result);
            Assert.Equal(projectDto.Name, result.Name);
            Assert.Equal(projectDto.Description, result.Description);
            Assert.NotEqual(Guid.Empty, result.Id); // Ensure an ID was generated

            _mockProjectRepository.Verify(r => r.AsQueryable(), Times.Once); // Verify validation check
            _mockProjectRepository.Verify(
                r => r.AddAsync(It.Is<Project>(p => p.OwnerId == ownerId && p.Name == projectDto.Name)), Times.Once);
            _mockProjectRepository.Verify(r => r.SaveChangesAsync(), Times.Once);
            _mockCache.Verify(c => c.RemoveAsync($"projects_user_{ownerId}", It.IsAny<CancellationToken>()),
                Times.Once);
            _mockCache.Verify(c => c.RemoveAsync("projects_all", It.IsAny<CancellationToken>()), Times.Once);
            _mockPublishEndpoint.Verify(
                p => p.Publish(
                    It.Is<Application.Events.Projects.ProjectCreatedEvent>(e =>
                        e.Name == projectDto.Name && e.OwnerId == ownerId), It.IsAny<CancellationToken>()), Times.Once);
        }

        [Fact]
        public async Task Handle_DuplicateNameForOwner_ThrowsInvalidOperationException()
        {
            // Arrange
            var projectDto = new CreateProjectRequestDto { Name = "Alpha Project", Description = "Description" };
            var ownerId = "user1";
            var command = new CreateProjectCommand(projectDto, ownerId);


            var mockProjectsQueryable = _testProjects.AsQueryable().BuildMock();
            _mockProjectRepository.Setup(r => r.AsQueryable()).Returns(mockProjectsQueryable);
            //mockProjectsQueryable.Setup(m => m.AnyAsync(It.IsAny<Expression<Func<Project, bool>>>(), It.IsAny<CancellationToken>())).ReturnsAsync(true);

            // Act & Assert
            await Assert.ThrowsAsync<InvalidOperationException>(() => _handler.Handle(command, CancellationToken.None));

            _mockProjectRepository.Verify(r => r.AsQueryable(), Times.Once);
            _mockMapper.Verify(m => m.Map<Project>(It.IsAny<CreateProjectRequestDto>()), Times.Never);
            _mockProjectRepository.Verify(r => r.AddAsync(It.IsAny<Project>()), Times.Never);
            _mockProjectRepository.Verify(r => r.SaveChangesAsync(), Times.Never);
            _mockCache.Verify(c => c.RemoveAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
            _mockPublishEndpoint.Verify(
                p => p.Publish(It.IsAny<Application.Events.Projects.ProjectCreatedEvent>(),
                    It.IsAny<CancellationToken>()), Times.Never);
        }


    }
}