using Application.Commands.Projects;
using Application.Events.Projects;
using Application.Exceptions;
using Application.Interfaces;
using Domain.Models;
using MassTransit;
using MediatR;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Logging;



namespace Infrastructure.Handlers.Projects
{
   
    public class DeleteProjectCommandHandler(
        IRepository<Project> projectRepository,
        ILogger<DeleteProjectCommandHandler> logger,
        IPublishEndpoint publishEndpoint, // Inject IPublishEndpoint
        IDistributedCache cache)
        : IRequestHandler<DeleteProjectCommand, Unit>
    {
        // Inject IDistributedCache

        public async Task<Unit> Handle(DeleteProjectCommand request, CancellationToken cancellationToken)
        {
            logger.LogInformation("Handling DeleteProjectCommand for project {ProjectId} by user {UserId}, isAdmin: {IsAdmin}", request.ProjectId, request.UserId, request.IsAdmin);

            var project = await projectRepository.GetByIdAsync(request.ProjectId)!;

            if (project == null)
            {
                logger.LogWarning("Attempted to delete non-existent project {ProjectId}.", request.ProjectId);
                throw new NotFoundException(nameof(Project), request.ProjectId);
            }

          
            if (!request.IsAdmin && project.OwnerId != request.UserId)
            {
                logger.LogWarning("User {UserId} attempted to delete project {ProjectId} without permission.", request.UserId, request.ProjectId);
                throw new ForbiddenAccessException("You do not have permission to delete this project.");
            }

            projectRepository.Remove(project);
            await projectRepository.SaveChangesAsync();

            logger.LogInformation("Project {ProjectId} deleted successfully.", request.ProjectId);

            // --- Invalidate Cache ---
            // Invalidate cache entries related to this specific project and lists it might appear in
            await cache.RemoveAsync($"project_{project.Id}", cancellationToken); // Invalidate specific project cache
            await cache.RemoveAsync($"projects_user_{project.OwnerId}", cancellationToken); // Invalidate list for this owner
            await cache.RemoveAsync("projects_all", cancellationToken); // Invalidate list for all (if admin view is cached)
                                                                       
            await cache.RemoveAsync($"tasks_project_{project.Id}", cancellationToken);
            logger.LogInformation("Invalidated project cache after deletion for ProjectId: {ProjectId}", project.Id);
            // --- End Invalidate Cache ---


            await publishEndpoint.Publish(new ProjectDeletedEvent(
            {
                ProjectId = project.Id,
                OwnerId = project.OwnerId,
                Timestamp = DateTime.UtcNow
            }, cancellationToken);
            logger.LogInformation("Published ProjectDeletedEvent for ProjectId: {ProjectId}", project.Id);
           

            return Unit.Value; 
        }
    }
}