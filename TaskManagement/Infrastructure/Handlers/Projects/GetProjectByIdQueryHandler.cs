using MediatR;
using Microsoft.Extensions.Logging;
// Reference repository interface
// Reference Domain models
using AutoMapper; // Use AutoMapper
// using TaskManagement.Application.Mapping; // Removed Mapperly interfaces
// Reference custom exceptions
using Microsoft.Extensions.Caching.Distributed; // Needed for IDistributedCache
using System.Text.Json;
using Application.Exceptions;
using Application.Queries.Projects; // Needed for JSON serialization/deserialization for caching

namespace TaskManagement.Infrastructure.Handlers.Projects
{
    // Query Handler for getting a project by ID
    public class GetProjectByIdQueryHandler(
        IRepository<Project> projectRepository,
        ILogger<GetProjectByIdQueryHandler> logger,
        IMapper mapper, // Inject AutoMapper's IMapper
        IDistributedCache cache)
        : IRequestHandler<GetProjectByIdQuery, ProjectDto>
    {
        private readonly IRepository<Project> _projectRepository = projectRepository; // Dependency on Application interface

        // Inject AutoMapper's IMapper
        // Inject IDistributedCache for caching


        // Inject IDistributedCache

        public async Task<ProjectDto> Handle(GetProjectByIdQuery request, CancellationToken cancellationToken)
        {
            logger.LogInformation("Handling GetProjectByIdQuery for project {ProjectId} by user {UserId}, isAdmin: {IsAdmin}", request.ProjectId, request.UserId, request.IsAdmin);

            // --- Cache Lookup ---
            var cacheKey = $"project_{request.ProjectId}";
            var cachedResult = await cache.GetStringAsync(cacheKey, cancellationToken);

            if (!string.IsNullOrEmpty(cachedResult))
            {
                logger.LogInformation("Returning cached result for query {CacheKey}", cacheKey);
                // Deserialize from JSON and return the cached result
                return JsonSerializer.Deserialize<ProjectDto>(cachedResult);
            }
            // --- End Cache Lookup ---


            var project = await _projectRepository.GetByIdAsync(request.ProjectId);

            if (project == null)
            {
                logger.LogWarning("Project {ProjectId} not found.", request.ProjectId);
                // Throw the custom NotFoundException
                throw new NotFoundException(nameof(Project), request.ProjectId);
            }

            // RBAC: Check if user is admin or owner
            if (!request.IsAdmin && project.OwnerId != request.UserId)
            {
                logger.LogWarning("User {UserId} attempted to access project {ProjectId} without permission.", request.UserId, request.ProjectId);
                // Throw the custom ForbiddenAccessException
                throw new ForbiddenAccessException("You do not have access to this project.");
            }

            // Use AutoMapper
            var result = mapper.Map<ProjectDto>(project);

            // --- Cache Write ---
            var cacheOptions = new DistributedCacheEntryOptions()
                .SetSlidingExpiration(TimeSpan.FromMinutes(5)); // Example: Cache for 5 minutes of inactivity

            var jsonResult = JsonSerializer.Serialize(result);
            await cache.SetStringAsync(cacheKey, jsonResult, cacheOptions, cancellationToken);
            logger.LogInformation("Cached result for query {CacheKey}", cacheKey);
            // --- End Cache Write ---


            return result;
        }
    }
}