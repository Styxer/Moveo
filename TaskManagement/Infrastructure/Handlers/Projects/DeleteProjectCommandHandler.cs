using MediatR;
using Microsoft.Extensions.Logging;
// Reference repository interface
// Reference Domain models
// Needed for KeyNotFoundException
using MassTransit; // Added for MassTransit
// Reference custom exceptions
using Microsoft.Extensions.Caching.Distributed; // Needed for IDistributedCache
using Application.Commands.Projects;
using Application.Exceptions; // Needed for JSON serialization/deserialization for caching

namespace TaskManagement.Infrastructure.Handlers.Projects
{
    // Command Handler for deleting a project
    public class DeleteProjectCommandHandler(
        IRepository<Project> projectRepository,
        ILogger<DeleteProjectCommandHandler> logger,
        IPublishEndpoint publishEndpoint, // Inject IPublishEndpoint
        IDistributedCache cache)
        : IRequestHandler<DeleteProjectCommand, Unit>
    {
        private readonly IRepository<Project> _projectRepository = projectRepository; // Dependency on Application interface

        // Inject IPublishEndpoint to publish events
        // Inject IDistributedCache for cache invalidation


        // Inject IDistributedCache

        public async Task<Unit> Handle(DeleteProjectCommand request, CancellationToken cancellationToken)
        {
            logger.LogInformation("Handling DeleteProjectCommand for project {ProjectId} by user {UserId}, isAdmin: {IsAdmin}", request.ProjectId, request.UserId, request.IsAdmin);

            var project = await _projectRepository.GetByIdAsync(request.ProjectId);

            if (project == null)
            {
                logger.LogWarning("Attempted to delete non-existent project {ProjectId}.", request.ProjectId);
                // Throw the custom NotFoundException
                throw new NotFoundException(nameof(Project), request.ProjectId);
            }

            // RBAC: Check if user is admin or owner
            if (!request.IsAdmin && project.OwnerId != request.UserId)
            {
                logger.LogWarning("User {UserId} attempted to delete project {ProjectId} without permission.", request.UserId, request.ProjectId);
                // Throw the custom ForbiddenAccessException
                throw new ForbiddenAccessException("You do not have permission to delete this project.");
            }

            _projectRepository.Remove(project);
            await _projectRepository.SaveChangesAsync(); // Save the entity and the Outbox message in a single transaction

            logger.LogInformation("Project {ProjectId} deleted successfully.", request.ProjectId);

            // --- Invalidate Cache ---
            // Invalidate cache entries related to this specific project and lists it might appear in
            await cache.RemoveAsync($"project_{project.Id}", cancellationToken); // Invalidate specific project cache
            await cache.RemoveAsync($"projects_user_{project.OwnerId}", cancellationToken); // Invalidate list for this owner
            await cache.RemoveAsync("projects_all", cancellationToken); // Invalidate list for all (if admin view is cached)
                                                                         // Invalidate cache for tasks within this project if cached separately
            await cache.RemoveAsync($"tasks_project_{project.Id}", cancellationToken);
            logger.LogInformation("Invalidated project cache after deletion for ProjectId: {ProjectId}", project.Id);
            // --- End Invalidate Cache ---


            // --- Publish the ProjectDeletedEvent ---
            await publishEndpoint.Publish(new Application.Events.Projects.ProjectDeletedEvent
            {
                ProjectId = project.Id,
                OwnerId = project.OwnerId,
                Timestamp = DateTime.UtcNow
            }, cancellationToken);
            logger.LogInformation("Published ProjectDeletedEvent for ProjectId: {ProjectId}", project.Id);
            // --- End Publish ---

            return Unit.Value; // Indicate success
        }
    }
}