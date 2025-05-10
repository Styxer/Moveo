using System.Text.Json;
using Application.DTOs.Projects;
using Application.Exceptions;
using Application.Interfaces;
using Application.Queries.Projects;
using AutoMapper;
using Domain.Models;
using MediatR;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Logging;

namespace Infrastructure.Handlers.Projects
{
    // Query Handler for getting a project by ID
    public class GetProjectByIdQueryHandler(
        IRepository<Project> projectRepository,
        ILogger<GetProjectByIdQueryHandler> logger,
        IMapper mapper,
        IDistributedCache cache)
        : IRequestHandler<GetProjectByIdQuery, ProjectDto>
    {
        public async Task<ProjectDto> Handle(GetProjectByIdQuery request, CancellationToken cancellationToken)
        {
            logger.LogInformation("Handling GetProjectByIdQuery for project {ProjectId} by user {UserId}, isAdmin: {IsAdmin}", request.ProjectId, request.UserId, request.IsAdmin);

            // --- Cache Lookup ---
            var cacheKey = $"project_{request.ProjectId}";
            var cachedResult = await cache.GetStringAsync(cacheKey, cancellationToken);

            if (!string.IsNullOrEmpty(cachedResult))
            {
                logger.LogInformation("Returning cached result for query {CacheKey}", cacheKey);
                return JsonSerializer.Deserialize<ProjectDto>(cachedResult);
            }
          


            var project = await projectRepository.GetByIdAsync(request.ProjectId);

            if (project == null)
            {
                logger.LogWarning("Project {ProjectId} not found.", request.ProjectId);
                throw new NotFoundException(nameof(Project), request.ProjectId);
            }

       
            if (!request.IsAdmin && project.OwnerId != request.UserId)
            {
                logger.LogWarning("User {UserId} attempted to access project {ProjectId} without permission.", request.UserId, request.ProjectId);
                throw new ForbiddenAccessException("You do not have access to this project.");
            }

            // Use AutoMapper
            var result = mapper.Map<ProjectDto>(project);

            // --- Cache Write ---
            var cacheOptions = new DistributedCacheEntryOptions()
                .SetSlidingExpiration(TimeSpan.FromMinutes(5)); 

            var jsonResult = JsonSerializer.Serialize(result);
            await cache.SetStringAsync(cacheKey, jsonResult, cacheOptions, cancellationToken);
            logger.LogInformation("Cached result for query {CacheKey}", cacheKey);
     


            return result;
        }
    }
}