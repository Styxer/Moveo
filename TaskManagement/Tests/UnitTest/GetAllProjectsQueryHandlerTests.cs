using System.Text.Json;
using Application.DTOs.Pagination;
using Application.DTOs.Projects;
using Application.Interfaces;
using Application.Queries.Projects;
using Domain.Models;
using Infrastructure.Handlers.Projects;
using MockQueryable;
using Task = System.Threading.Tasks.Task;

namespace Tests.UnitTest;

public class GetAllProjectsQueryHandlerTests
{
    private readonly Mock<IRepository<Project>> _mockProjectRepository;
    private readonly Mock<IMapper> _mockMapper; 
    private readonly Mock<IDistributedCache> _mockCache; 
    private readonly GetAllProjectsQueryHandler _handler;

    private readonly List<Project> _testProjects; 

    public GetAllProjectsQueryHandlerTests()
    {
        _mockProjectRepository = new Mock<IRepository<Project>>();
        Mock<ILogger<GetAllProjectsQueryHandler>> mockLogger = new();
        _mockMapper = new Mock<IMapper>();
        _mockCache = new Mock<IDistributedCache>();

        _handler = new GetAllProjectsQueryHandler(
            _mockProjectRepository.Object,
            mockLogger.Object,
            _mockMapper.Object, 
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
            },
            new Project
            {
                Id = Guid.NewGuid(), 
                Name = "Delta Project",
                Description = "Project about Delta",
                OwnerId = "user1"
            },
            new Project
            {
                Id = Guid.NewGuid(),
                Name = "Epsilon Initiative",
                Description = "Initiative Epsilon",
                OwnerId = "user2"
            }
        ];
    }

    [Fact]
    public async Task Handle_AdminQuery_ReturnsAllProjects()
    {
        // Arrange
        var userId = "adminUser";
        var isAdmin = true;
        var queryParameters = new ProjectQueryParameters { PageNumber = 1, PageSize = 10 };
        var query = new GetAllProjectsQuery(userId, isAdmin, queryParameters);

       
        var mockProjectsQueryable = _testProjects.AsQueryable().BuildMock();
        _mockProjectRepository.Setup(r => r.AsQueryable()).Returns(mockProjectsQueryable);

        
        var projectDtos = _mockMapper.Object.Map<IEnumerable<ProjectDto>>(_testProjects);
        _mockMapper.Setup(m => m.Map<IEnumerable<ProjectDto>>(It.IsAny<IEnumerable<Project>>())).Returns(projectDtos);


 
        _mockCache.Setup(c => c.GetStringAsync(It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync((string)null);

    
        _mockCache.Setup(c => c.SetStringAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<DistributedCacheEntryOptions>(), It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);


        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(_testProjects.Count, result.TotalCount);
        Assert.Equal(_testProjects.Count, result.Items.Count());
        _mockProjectRepository.Verify(r => r.AsQueryable(), Times.Once);
        _mockCache.Verify(c => c.GetStringAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Once); // Cache lookup
        _mockCache.Verify(c => c.SetStringAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<DistributedCacheEntryOptions>(), It.IsAny<CancellationToken>()), Times.Once); // Cache write
    }

    [Fact]
    public async Task Handle_UserQuery_ReturnsOnlyOwnedProjects()
    {
        // Arrange
        var userId = "user1";
        var isAdmin = false;
        var queryParameters = new ProjectQueryParameters { PageNumber = 1, PageSize = 10 };
        var query = new GetAllProjectsQuery(userId, isAdmin, queryParameters);

        var ownedProjects = _testProjects.Where(p => p.OwnerId == userId).ToList();


        var mockProjectsQueryable = _testProjects.AsQueryable().BuildMock();
        _mockProjectRepository.Setup(r => r.AsQueryable()).Returns(mockProjectsQueryable);

    
        var projectDtos = _mockMapper.Object.Map<IEnumerable<ProjectDto>>(ownedProjects); // Use real mapper for this part
        _mockMapper.Setup(m => m.Map<IEnumerable<ProjectDto>>(It.IsAny<IEnumerable<Project>>())).Returns(projectDtos);
       
        _mockCache.Setup(c => c.GetStringAsync(It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync((string)null);
        _mockCache.Setup(c => c.SetStringAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<DistributedCacheEntryOptions>(), It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);


        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        var ownedProjectsCount = _testProjects.Count(p => p.OwnerId == userId);
        Assert.NotNull(result);
        Assert.Equal(ownedProjectsCount, result.TotalCount);
        Assert.Equal(ownedProjectsCount, result.Items.Count());
        Assert.True(result.Items.All(p => p.OwnerId == userId)); 
        _mockProjectRepository.Verify(r => r.AsQueryable(), Times.Once);
        _mockCache.Verify(c => c.GetStringAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Once); 
        _mockCache.Verify(c => c.SetStringAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<DistributedCacheEntryOptions>(), It.IsAny<CancellationToken>()), Times.Once); 
    }

    [Fact]
    public async Task Handle_QueryFromCache_ReturnsCachedProjects()
    {
        // Arrange
        var userId = "user1";
        var isAdmin = false;
        var queryParameters = new ProjectQueryParameters { PageNumber = 1, PageSize = 10 };
        var query = new GetAllProjectsQuery(userId, isAdmin, queryParameters);

        // Simulate a cached result
        var cachedProjects = new PagedResultDto<ProjectDto>
        {
            Items = new List<ProjectDto> { new()
            {
                Id = Guid.NewGuid(),
                Name = "Cached Project",
                Description = "From Cache"
            } },
            TotalCount = 1,
            PageNumber = 1,
            PageSize = 10
        };
        var cachedJson = JsonSerializer.Serialize(cachedProjects);

      
        _mockCache.Setup(c => c.GetStringAsync(It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync(cachedJson);

        
        _mockProjectRepository.Setup(r => r.AsQueryable()).Throws(new Exception("Repository should not be called"));
        _mockMapper.Setup(m => m.Map<IEnumerable<ProjectDto>>(It.IsAny<IEnumerable<Project>>())).Throws(new Exception("Mapper should not be called"));


        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(cachedProjects.TotalCount, result.TotalCount);
        Assert.Equal(cachedProjects.Items.Count(), result.Items.Count());
        Assert.Equal(cachedProjects.Items.First().Name, result.Items.First().Name); 

        _mockCache.Verify(c => c.GetStringAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Once); 
        _mockProjectRepository.Verify(r => r.AsQueryable(), Times.Never); 
        _mockMapper.Verify(m => m.Map<IEnumerable<ProjectDto>>(It.IsAny<IEnumerable<Project>>()), Times.Never); 
        _mockCache.Verify(c => c.SetStringAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<DistributedCacheEntryOptions>(), It.IsAny<CancellationToken>()), Times.Never); 
    }
    // TODO: tests for filtering, sorting, and pagination within GetAllProjectsQueryHandler

}