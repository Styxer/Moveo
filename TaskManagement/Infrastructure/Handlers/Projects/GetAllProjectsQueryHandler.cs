using System.Text.Json;
using Application.DTOs.Pagination;
using Application.DTOs.Projects;
using Application.Interfaces;
using Application.Queries.Projects;
using AutoMapper;
using Domain.Models;
using MediatR;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Logging;



namespace Infrastructure.Handlers.Projects
{
    // Query Handler for getting all projects
    public class GetAllProjectsQueryHandler(
        IRepository<Project> projectRepository,
        ILogger<GetAllProjectsQueryHandler> logger,
        IMapper mapper, 
        IDistributedCache cache)
        : IRequestHandler<GetAllProjectsQuery, PagedResultDto<ProjectDto>>
    {
        private readonly IRepository<Project> _projectRepository = projectRepository; 

        public async Task<PagedResultDto<ProjectDto>> Handle(GetAllProjectsQuery request, CancellationToken cancellationToken)
        {
            logger.LogInformation("Handling GetAllProjectsQuery for user {UserId}, isAdmin: {IsAdmin}, Query: {Query}", request.UserId, request.IsAdmin, request.QueryParameters.SearchQuery);

            // --- Cache Lookup ---
            // Generate a cache key based on query parameters and user identity
            var cacheKey = $"projects_user_{request.UserId}_page_{request.QueryParameters.PageNumber}_size_{request.QueryParameters.PageSize}_search_{request.QueryParameters.SearchQuery ?? "none"}_sortby_{request.QueryParameters.SortBy ?? "none"}_sortorder_{request.QueryParameters.SortOrder ?? "none"}";
            if (request.IsAdmin) // Admins see all, so use a different key
            {
                cacheKey = $"projects_all_page_{request.QueryParameters.PageNumber}_size_{request.QueryParameters.PageSize}_search_{request.QueryParameters.SearchQuery ?? "none"}_sortby_{request.QueryParameters.SortBy ?? "none"}_sortorder_{request.QueryParameters.SortOrder ?? "none"}";
            }

            var cachedResult = await cache.GetStringAsync(cacheKey, cancellationToken);

            if (!string.IsNullOrEmpty(cachedResult))
            {
                logger.LogInformation("Returning cached result for query {CacheKey}", cacheKey);
                // Deserialize from JSON and return the cached result
                return JsonSerializer.Deserialize<PagedResultDto<ProjectDto>>(cachedResult);
            }
            // --- End Cache Lookup ---


            var query = _projectRepository.AsQueryable();

            // RBAC: Admins see all, Users only see their own
            if (!request.IsAdmin)
            {
                query = query.Where(p => p.OwnerId == request.UserId);
            }

            // Apply Filtering
            if (!string.IsNullOrWhiteSpace(request.QueryParameters.SearchQuery))
            {
                var searchQuery = request.QueryParameters.SearchQuery.ToLower();
                query = query.Where(p => p.Name.ToLower().Contains(searchQuery) ||
                                         (p.Description != null && p.Description.ToLower().Contains(searchQuery)));
            }

            // Apply Sorting
            query = query.ApplySorting(request.QueryParameters.SortBy, request.QueryParameters.SortOrder);

            // Apply pagination - uses extension from Application layer
            var pagedProjects = await query.ToPagedResultAsync(request.QueryParameters.PageNumber, request.QueryParameters.PageSize);

            // Use AutoMapper
            var projectDtos = mapper.Map<IEnumerable<ProjectDto>>(pagedProjects.Items);

            var result = new PagedResultDto<ProjectDto>
            {
                Items = projectDtos,
                TotalCount = pagedProjects.TotalCount,
                PageNumber = pagedProjects.PageNumber,
                PageSize = pagedProjects.PageSize
            };

            // --- Cache Write ---
            // Serialize the result to JSON and store it in the cache
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