using MediatR;
using Microsoft.Extensions.Logging;
// Reference repository interface
// Reference Domain models
// Needed for KeyNotFoundException
using AutoMapper; // Use AutoMapper
// using TaskManagement.Application.Mapping; // Removed Mapperly interfaces
// Needed for AnyAsync
using MassTransit; // Added for MassTransit
// Reference custom exceptions
using Microsoft.Extensions.Caching.Distributed; // Needed for IDistributedCache
using Application.Commands.Projects;
using Application.Exceptions; // Needed for JSON serialization/deserialization for caching

namespace TaskManagement.Infrastructure.Handlers.Projects
{
    // Command Handler for updating a project
    public class UpdateProjectCommandHandler(
        IRepository<Project> projectRepository,
        ILogger<UpdateProjectCommandHandler> logger,
        IMapper mapper, // Inject AutoMapper's IMapper
        IPublishEndpoint publishEndpoint, // Inject IPublishEndpoint
        IDistributedCache cache)
        : IRequestHandler<UpdateProjectCommand, Unit>
    {
        private readonly IRepository<Project> _projectRepository = projectRepository; // Dependency on Application interface

        // Inject AutoMapper's IMapper
        // Inject IPublishEndpoint to publish events
        // Inject IDistributedCache for cache invalidation


        // Inject IDistributedCache

        public async Task<Unit> Handle(UpdateProjectCommand request, CancellationToken cancellationToken)
        {
            logger.LogInformation("Handling UpdateProjectCommand for project {ProjectId} by user {UserId}, isAdmin: {IsAdmin}", request.ProjectId, request.UserId, request.IsAdmin);

            var project = await _projectRepository.GetByIdAsync(request.ProjectId);

            if (project == null)
            {
                logger.LogWarning("Attempted to update non-existent project {ProjectId}.", request.ProjectId);
                // Throw the custom NotFoundException
                throw new NotFoundException(nameof(Project), request.ProjectId);
            }

            // RBAC: Check if user is admin or owner
            if (!request.IsAdmin && project.OwnerId != request.UserId)
            {
                logger.LogWarning("User {UserId} attempted to update project {ProjectId} without permission.", request.UserId, request.ProjectId);
                // Throw the custom ForbiddenAccessException
                throw new ForbiddenAccessException("You do not have permission to update this project.");
            }

            // --- Business Rule Validation Example (for update) ---
            // Check if updating the name would create a duplicate for this owner (excluding the current project)
            if (project.Name != request.ProjectDto.Name) // Only check if the name is actually changing
            {
                var existingProjectWithSameName = await _projectRepository.AsQueryable()
                                                                          .AnyAsync(p => p.OwnerId == project.OwnerId && p.Name == request.ProjectDto.Name && p.Id != request.ProjectId, cancellationToken);
                if (existingProjectWithSameName)
                {
                    logger.LogWarning("Attempted to rename project {ProjectId} to duplicate name '{ProjectName}' for owner {OwnerId}.", request.ProjectId, request.ProjectDto.Name, project.OwnerId);
                    // Throw the custom ConflictException
                    throw new ConflictException($"A project with the name '{request.ProjectDto.Name}' already exists for this user.");
                }
            }
            // --- End Validation Example ---

            // Store original values before mapping for the event
            var originalName = project.Name;
            var originalDescription = project.Description;

            // Use AutoMapper to update the existing entity
            mapper.Map(request.ProjectDto, project); // AutoMapper can map to existing objects

            await _projectRepository.SaveChangesAsync(); // Save the entity and the Outbox message in a single transaction

            logger.LogInformation("Project {ProjectId} updated successfully.", request.ProjectId);

            // --- Invalidate Cache ---
            // Invalidate cache entries related to this specific project and lists it might appear in
            await cache.RemoveAsync($"project_{project.Id}", cancellationToken); // Invalidate specific project cache
            await cache.RemoveAsync($"projects_user_{project.OwnerId}", cancellationToken); // Invalidate list for this owner
            await cache.RemoveAsync("projects_all", cancellationToken); // Invalidate list for all (if admin view is cached)
                                                                         // Invalidate cache for tasks within this project if cached separately
            await cache.RemoveAsync($"tasks_project_{project.Id}", cancellationToken);
            logger.LogInformation("Invalidated project cache after update for ProjectId: {ProjectId}", project.Id);
            // --- End Invalidate Cache ---


            // --- Publish the ProjectUpdatedEvent ---
            // Only publish if relevant properties changed
            if (project.Name != originalName || project.Description != originalDescription)
            {
                await publishEndpoint.Publish(new Application.Events.Projects.ProjectUpdatedEvent
                {
                    ProjectId = project.Id,
                    Name = project.Name,
                    Description = project.Description,
                    OwnerId = project.OwnerId,
                    Timestamp = DateTime.UtcNow
                }, cancellationToken);
                logger.LogInformation("Published ProjectUpdatedEvent for ProjectId: {ProjectId}", project.Id);
            }
            // --- End Publish ---

            return Unit.Value; // Indicate success with no specific return value
        }
    }
}