using MediatR;
using Microsoft.Extensions.Logging;
using TaskManagement.Application.Interfaces; // Reference repository interface
using TaskManagement.Application.DTOs.Tasks;
using TaskManagement.Domain.Models; // Reference Domain models
using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore; // Needed for Include
using AutoMapper; // Use AutoMapper
// using TaskManagement.Application.Mapping; // Removed Mapperly interfaces
// Reference custom exceptions
using Microsoft.Extensions.Caching.Distributed; // Needed for IDistributedCache
using System.Text.Json;
using Application.Exceptions;
using Application.Queries.Tasks; // Needed for JSON serialization/deserialization for caching

namespace TaskManagement.Infrastructure.Handlers.Tasks
{
    // Query Handler for getting a task by ID
    public class GetTaskByIdQueryHandler : IRequestHandler<GetTaskByIdQuery, TaskDto>
    {
        private readonly IRepository<Task> _taskRepository; // Dependency on Application interface
        private readonly IRepository<Project> _projectRepository; // Need to check project access
        private readonly ILogger<GetTaskByIdQueryHandler> _logger;
        // Inject AutoMapper's IMapper
        private readonly IMapper _mapper;
        // Inject IDistributedCache for caching
        private readonly IDistributedCache _cache;


        public GetTaskByIdQueryHandler(IRepository<Task> taskRepository,
                                       IRepository<Project> projectRepository, // Inject Project Repository
                                       ILogger<GetTaskByIdQueryHandler> logger,
                                       IMapper mapper, // Inject AutoMapper's IMapper
                                       IDistributedCache cache) // Inject IDistributedCache
        {
            _taskRepository = taskRepository;
            _projectRepository = projectRepository;
            _logger = logger;
            _mapper = mapper;
            _cache = cache;
        }

        public async Task<TaskDto> Handle(GetTaskByIdQuery request, CancellationToken cancellationToken)
        {
            _logger.LogInformation("Handling GetTaskByIdQuery for task {TaskId}", request.TaskId);

            // --- Cache Lookup ---
            var cacheKey = $"task_{request.TaskId}";
            var cachedResult = await _cache.GetStringAsync(cacheKey, cancellationToken);

            if (!string.IsNullOrEmpty(cachedResult))
            {
                _logger.LogInformation("Returning cached result for query {CacheKey}", cacheKey);
                // Deserialize from JSON and return the cached result
                return JsonSerializer.Deserialize<TaskDto>(cachedResult);
            }
            // --- End Cache Lookup ---


            // Fetch the task including its parent project for access check
            var task = await _taskRepository.AsQueryable()
                                            .Include(t => t.Project)
                                            .FirstOrDefaultAsync(t => t.Id == request.TaskId, cancellationToken);

            if (task == null)
            {
                _logger.LogWarning("Task {TaskId} not found.", request.TaskId);
                // Throw the custom NotFoundException
                throw new NotFoundException(nameof(Task), request.TaskId);
            }

            // RBAC: Check if user is admin or owner of the parent project
            if (!request.IsAdmin && task.Project.OwnerId != request.UserId)
            {
                _logger.LogWarning("User {UserId} attempted to access task {TaskId} in project {ProjectId} without permission.", request.UserId, request.TaskId, task.ProjectId);
                // Throw the custom ForbiddenAccessException
                throw new ForbiddenAccessException("You do not have permission to access this task.");
            }

            // Use AutoMapper
            var result = _mapper.Map<TaskDto>(task);

            // --- Cache Write ---
            var cacheOptions = new DistributedCacheEntryOptions()
                .SetSlidingExpiration(TimeSpan.FromMinutes(5)); // Example: Cache for 5 minutes of inactivity

            var jsonResult = JsonSerializer.Serialize(result);
            await _cache.SetStringAsync(cacheKey, jsonResult, cacheOptions, cancellationToken);
            _logger.LogInformation("Cached result for query {CacheKey}", cacheKey);
            // --- End Cache Write ---

            return result;
        }
    }
}