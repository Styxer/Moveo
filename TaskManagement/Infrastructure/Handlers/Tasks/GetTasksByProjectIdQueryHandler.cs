using MediatR;
using Microsoft.Extensions.Logging;
using TaskManagement.Application.Interfaces; // Reference repository interface
using TaskManagement.Application.DTOs.Tasks;
using TaskManagement.Application.DTOs.Pagination; // Reference Pagination DTOs
using TaskManagement.Domain.Models; // Reference Domain models
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using AutoMapper; // Use AutoMapper
// using TaskManagement.Application.Mapping; // Removed Mapperly interfaces
using Microsoft.EntityFrameworkCore; // Needed for AnyAsync, ToListAsync etc.
using TaskManagement.Application.Extensions; // For pagination and sorting extensions
// Reference custom exceptions
using Microsoft.Extensions.Caching.Distributed; // Needed for IDistributedCache
using System.Text.Json;
using Application.Exceptions;
using Application.Queries.Tasks; // Needed for JSON serialization/deserialization for caching

namespace TaskManagement.Infrastructure.Handlers.Tasks
{
    // Query Handler for getting tasks by project ID
    public class GetTasksByProjectIdQueryHandler : IRequestHandler<GetTasksByProjectIdQuery, PagedResultDto<TaskDto>>
    {
        private readonly IRepository<Task> _taskRepository; // Dependency on Application interface
        private readonly IRepository<Project> _projectRepository; // Need to check project access
        private readonly ILogger<GetTasksByProjectIdQueryHandler> _logger;
        // Inject AutoMapper's IMapper
        private readonly IMapper _mapper;
        // Inject IDistributedCache for caching
        private readonly IDistributedCache _cache;


        public GetTasksByProjectIdQueryHandler(IRepository<Task> taskRepository,
                                               IRepository<Project> projectRepository, // Inject Project Repository
                                               ILogger<GetTasksByProjectIdQueryHandler> logger,
                                               IMapper mapper, // Inject AutoMapper's IMapper
                                               IDistributedCache cache) // Inject IDistributedCache
        {
            _taskRepository = taskRepository;
            _projectRepository = projectRepository;
            _logger = logger;
            _mapper = mapper;
            _cache = cache;
        }

        public async Task<PagedResultDto<TaskDto>> Handle(GetTasksByProjectIdQuery request, CancellationToken cancellationToken)
        {
            _logger.LogInformation("Handling GetTasksByProjectIdQuery for project {ProjectId}, Query: {Query}", request.ProjectId, request.QueryParameters.SearchQuery);

            // Check project access first
            var project = await _projectRepository.GetByIdAsync(request.ProjectId);
            if (project == null)
            {
                _logger.LogWarning("Attempted to get tasks for non-existent project {ProjectId} by user {UserId}", request.ProjectId, request.UserId);
                // Throw the custom NotFoundException
                throw new NotFoundException(nameof(Project), request.ProjectId);
            }

            if (!request.IsAdmin && project.OwnerId != request.UserId)
            {
                _logger.LogWarning("User {UserId} attempted to access tasks for project {ProjectId} without permission.", request.UserId, request.ProjectId);
                // Throw the custom ForbiddenAccessException
                throw new ForbiddenAccessException("You do not have access to this project's tasks.");
            }

            // --- Cache Lookup ---
            // Generate a cache key based on project ID and query parameters
            var cacheKey = $"tasks_project_{request.ProjectId}_page_{request.QueryParameters.PageNumber}_size_{request.QueryParameters.PageSize}_search_{request.QueryParameters.SearchQuery ?? "none"}_sortby_{request.QueryParameters.SortBy ?? "none"}_sortorder_{request.QueryParameters.SortOrder ?? "none"}";

            var cachedResult = await _cache.GetStringAsync(cacheKey, cancellationToken);

            if (!string.IsNullOrEmpty(cachedResult))
            {
                _logger.LogInformation("Returning cached result for query {CacheKey}", cacheKey);
                // Deserialize from JSON and return the cached result
                return JsonSerializer.Deserialize<PagedResultDto<TaskDto>>(cachedResult);
            }
            // --- End Cache Lookup ---


            var query = _taskRepository.AsQueryable()
                                       .Where(t => t.ProjectId == request.ProjectId);

            // Apply Filtering
            if (!string.IsNullOrWhiteSpace(request.QueryParameters.SearchQuery))
            {
                var searchQuery = request.QueryParameters.SearchQuery.ToLower();
                query = query.Where(t => t.Title.ToLower().Contains(searchQuery) ||
                                         (t.Description != null && t.Description.ToLower().Contains(searchQuery)));
            }

            // Apply Sorting
            query = query.ApplySorting(request.QueryParameters.SortBy, request.QueryParameters.SortOrder);

            // Apply pagination - uses extension from Application layer
            var pagedTasks = await query.ToPagedResultAsync(request.QueryParameters.PageNumber, request.QueryParameters.PageSize);

            // Use AutoMapper
            var taskDtos = _mapper.Map<IEnumerable<TaskDto>>(pagedTasks.Items);

            var result = new PagedResultDto<TaskDto>
            {
                Items = taskDtos,
                TotalCount = pagedTasks.TotalCount,
                PageNumber = pagedTasks.PageNumber,
                PageSize = pagedTasks.PageSize
            };

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