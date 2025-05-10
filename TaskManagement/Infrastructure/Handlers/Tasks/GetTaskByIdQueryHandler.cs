using MediatR;
using Microsoft.Extensions.Logging;
using AutoMapper; 
using Microsoft.Extensions.Caching.Distributed;
using System.Text.Json;
using Application.DTOs.Tasks;
using Application.Exceptions;
using Application.Interfaces;
using Application.Queries.Tasks;
using Domain.Models;
using Microsoft.EntityFrameworkCore;
using Task = Domain.Models.Task;


namespace TaskManagement.Infrastructure.Handlers.Tasks
{
    // Query Handler for getting a task by ID
    public class GetTaskByIdQueryHandler(
        IRepository<Task> taskRepository,
        IRepository<Project> projectRepository, 
        ILogger<GetTaskByIdQueryHandler> logger,
        IMapper mapper,
        IDistributedCache cache)
        : IRequestHandler<GetTaskByIdQuery, TaskDto> {
        public async Task<TaskDto> Handle(GetTaskByIdQuery request, CancellationToken cancellationToken)
        {
            logger.LogInformation("Handling GetTaskByIdQuery for task {TaskId}", request.TaskId);

            // --- Cache Lookup ---
            var cacheKey = $"task_{request.TaskId}";
            var cachedResult = await cache.GetStringAsync(cacheKey, cancellationToken);

            if (!string.IsNullOrEmpty(cachedResult))
            {
                logger.LogInformation("Returning cached result for query {CacheKey}", cacheKey);
                return JsonSerializer.Deserialize<TaskDto>(cachedResult);
            }
            // --- End Cache Lookup ---


            // Fetch the task including its parent project for access check
            var task = await taskRepository.AsQueryable()
                                            .Include(t => t.Project)
                                            .FirstOrDefaultAsync(t => t.Id == request.TaskId, cancellationToken);

            if (task == null)
            {
                logger.LogWarning("Task {TaskId} not found.", request.TaskId);
                throw new NotFoundException(nameof(Task), request.TaskId);
            }

        
            if (!request.IsAdmin && task.Project.OwnerId != request.UserId)
            {
                logger.LogWarning("User {UserId} attempted to access task {TaskId} in project {ProjectId} without permission.", request.UserId, request.TaskId, task.ProjectId);
                throw new ForbiddenAccessException("You do not have permission to access this task.");
            }

            var result = mapper.Map<TaskDto>(task);

      
            var cacheOptions = new DistributedCacheEntryOptions()
                .SetSlidingExpiration(TimeSpan.FromMinutes(5)); 

            var jsonResult = JsonSerializer.Serialize(result);
            await cache.SetStringAsync(cacheKey, jsonResult, cacheOptions, cancellationToken);
            logger.LogInformation("Cached result for query {CacheKey}", cacheKey);
        

            return result;
        }
    }
}