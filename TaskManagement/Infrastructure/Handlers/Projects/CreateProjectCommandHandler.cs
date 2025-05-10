using MediatR;
using Microsoft.Extensions.Logging;
using AutoMapper;

using Microsoft.EntityFrameworkCore;
using MassTransit; 

using Microsoft.Extensions.Caching.Distributed; 
using Application.Commands.Projects;
using Application.DTOs.Projects;
using Application.Exceptions;
using Application.Interfaces;
using Domain.Models;

namespace Infrastructure.Handlers.Projects
{
    // Command Handler for creating a project
    // Implements IRequestHandler<TCommand, TResponse>
    public class CreateProjectCommandHandler(
        IRepository<Project> projectRepository,
        ILogger<CreateProjectCommandHandler> logger,
        IMapper mapper, // Inject AutoMapper's IMapper
        IPublishEndpoint publishEndpoint, // Inject IPublishEndpoint
        IDistributedCache cache)
        : IRequestHandler<CreateProjectCommand, ProjectDto>{
    




        public async Task<ProjectDto> Handle(CreateProjectCommand request, CancellationToken cancellationToken)
        {
            logger.LogInformation("Handling CreateProjectCommand for owner {OwnerId}", request.OwnerId);

            // --- Business Rule Validation Example ---
            // Check if a project with the same name already exists for this owner
            var existingProject = await projectRepository.AsQueryable()
                                                          .AnyAsync(p => p.OwnerId == request.OwnerId && p.Name == request.ProjectDto.Name, cancellationToken);
            if (existingProject)
            {
                logger.LogWarning("Attempted to create duplicate project name '{ProjectName}' for owner {OwnerId}.", request.ProjectDto.Name, request.OwnerId);
                // Throw the custom ConflictException
                throw new ConflictException($"A project with the name '{request.ProjectDto.Name}' already exists for this user.");
            }
            // --- End Validation Example ---


            // Use AutoMapper
            var project = mapper.Map<Project>(request.ProjectDto);
            project.Id = Guid.NewGuid(); // Generate new ID
            project.OwnerId = request.OwnerId; // Assign owner

            await projectRepository.AddAsync(project);
            await projectRepository.SaveChangesAsync(); // Save the entity and the Outbox message in a single transaction

            logger.LogInformation("Project {ProjectId} created successfully.", project.Id);

            // --- Invalidate Cache ---
            // Invalidate cache entries related to lists of projects for this owner or all projects
            // Cache keys need to be designed to allow targeted invalidation.
            // Example keys: "projects_user_{ownerId}_page_{page}_size_{size}_search_{search}_sort_{sort}", "projects_all_page_{page}_size_{size}_search_{search}_sort_{sort}"
            // For simplicity here, we'll invalidate a few common keys. In a real app, you'd need a more robust key generation/invalidation strategy.
            await cache.RemoveAsync($"projects_user_{request.OwnerId}", cancellationToken); // Invalidate list for this owner
            await cache.RemoveAsync("projects_all", cancellationToken); // Invalidate list for all (if admin view is cached)
            // Invalidate cache for the specific project if it was cached by ID (less likely for creation)
            // await _cache.RemoveAsync($"project_{project.Id}", cancellationToken);
            logger.LogInformation("Invalidated project cache after creation for ProjectId: {ProjectId}", project.Id);
            // --- End Invalidate Cache ---


            // --- Publish the ProjectCreatedEvent ---
            // This message is saved to the Outbox within the transaction
            await publishEndpoint.Publish(new Application.Events.Projects.ProjectCreatedEvent
            {
                ProjectId = project.Id,
                Name = project.Name,
                Description = project.Description,
                OwnerId = project.OwnerId,
                Timestamp = DateTime.UtcNow // Include timestamp
            }, cancellationToken);
            logger.LogInformation("Published ProjectCreatedEvent for ProjectId: {ProjectId}", project.Id);
            // --- End Publish ---

            // Use AutoMapper for the return value
            return mapper.Map<ProjectDto>(project);
        }
    }
}