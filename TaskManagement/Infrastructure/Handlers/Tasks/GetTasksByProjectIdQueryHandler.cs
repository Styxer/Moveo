using MediatR;
using Microsoft.Extensions.Logging;
using AutoMapper;
using Microsoft.Extensions.Caching.Distributed; 
using System.Text.Json;
using Application.DTOs.Pagination;
using Application.DTOs.Tasks;
using Application.Exceptions;
using Application.Interfaces;
using Application.Queries.Tasks;
using Domain.Models;
using Task = Domain.Models.Task;


namespace TaskManagement.Infrastructure.Handlers.Tasks
{
    // Query Handler for getting tasks by project ID
    public class GetTasksByProjectIdQueryHandler(
        IRepository<Task> taskRepository,
        IRepository<Project> projectRepository, 
        ILogger<GetTasksByProjectIdQueryHandler> logger,
        IMapper mapper, 
        IDistributedCache cache)
        : IRequestHandler<GetTasksByProjectIdQuery, PagedResultDto<TaskDto>>{

        public async Task<PagedResultDto<TaskDto>> Handle(GetTasksByProjectIdQuery request, CancellationToken cancellationToken)
        {
            logger.LogInformation("Handling GetTasksByProjectIdQuery for project {ProjectId}, Query: {Query}", request.ProjectId, request.QueryParameters.SearchQuery);

            // Check project access first
            var project = await projectRepository.GetByIdAsync(request.ProjectId);
            if (project == null)
            {
                logger.LogWarning("Attempted to get tasks for non-existent project {ProjectId} by user {UserId}", request.ProjectId, request.UserId);
                throw new NotFoundException(nameof(Project), request.ProjectId);
            }

            if (!request.IsAdmin && project.OwnerId != request.UserId)
            {
                logger.LogWarning("User {UserId} attempted to access tasks for project {ProjectId} without permission.", request.UserId, request.ProjectId);
                throw new ForbiddenAccessException("You do not have access to this project's tasks.");
            }
            
            // Generate a cache key based on project ID and query parameters
            var cacheKey = $"tasks_project_{request.ProjectId}_page_{request.QueryParameters.PageNumber}_size_{request.QueryParameters.PageSize}_search_{request.QueryParameters.SearchQuery ?? "none"}_sortby_{request.QueryParameters.SortBy ?? "none"}_sortorder_{request.QueryParameters.SortOrder ?? "none"}";

            var cachedResult = await cache.GetStringAsync(cacheKey, cancellationToken);

            if (!string.IsNullOrEmpty(cachedResult))
            {
                logger.LogInformation("Returning cached result for query {CacheKey}", cacheKey);
                return JsonSerializer.Deserialize<PagedResultDto<TaskDto>>(cachedResult);
            }
           

            var query = taskRepository.AsQueryable()
                                       .Where(t => t.ProjectId == request.ProjectId);

            // Apply Filtering
            if (!string.IsNullOrWhiteSpace(request.QueryParameters.SearchQuery))
            {
                var searchQuery = request.QueryParameters.SearchQuery.ToLower();
                query = query.Where(x => x.Title.ToLower().Contains(searchQuery) ||
                                         (x.Description.ToLower().Contains(searchQuery)));
            }

            // Apply Sorting
            query = query.(request.QueryParameters.SortBy, request.QueryParameters.SortOrder); //TODO:FIX

            
            var pagedTasks = await query.(request.QueryParameters.PageNumber, request.QueryParameters.PageSize);  //TODO:FIX

         
            var taskDtos = mapper.Map<IEnumerable<TaskDto>>(pagedTasks.Items);

            var result = new PagedResultDto<TaskDto>
            {
                Items = taskDtos,
                TotalCount = pagedTasks.TotalCount,
                PageNumber = pagedTasks.PageNumber,
                PageSize = pagedTasks.PageSize
            };

       
            var cacheOptions = new DistributedCacheEntryOptions()
                .SetSlidingExpiration(TimeSpan.FromMinutes(5)); 

            var jsonResult = JsonSerializer.Serialize(result);
            await cache.SetStringAsync(cacheKey, jsonResult, cacheOptions, cancellationToken);
            logger.LogInformation("Cached result for query {CacheKey}", cacheKey);
   

            return result;
        }
    }
}