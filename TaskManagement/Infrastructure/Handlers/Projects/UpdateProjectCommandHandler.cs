using Application.Commands.Projects;
using Application.Events.Projects;
using Application.Exceptions;
using Application.Interfaces;
using AutoMapper;
using Domain.Models;
using MassTransit;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Logging;

namespace Infrastructure.Handlers.Projects
{
    // Command Handler for updating a project
    public class UpdateProjectCommandHandler(
        IRepository<Project> projectRepository,
        ILogger<UpdateProjectCommandHandler> logger,
        IMapper mapper, 
        IPublishEndpoint publishEndpoint,
        IDistributedCache cache)
        : IRequestHandler<UpdateProjectCommand, Unit>
    {
        public async Task<Unit> Handle(UpdateProjectCommand request, CancellationToken cancellationToken)
        {
            logger.LogInformation("Handling UpdateProjectCommand for project {ProjectId} by user {UserId}, isAdmin: {IsAdmin}", request.ProjectId, request.UserId, request.IsAdmin);

            var project = await projectRepository.GetByIdAsync(request.ProjectId);

            if (project == null)
            {
                logger.LogWarning("Attempted to update non-existent project {ProjectId}.", request.ProjectId);
                throw new NotFoundException(nameof(Project), request.ProjectId);
            }

    
            if (!request.IsAdmin && project.OwnerId != request.UserId)
            {
                logger.LogWarning("User {UserId} attempted to update project {ProjectId} without permission.", request.UserId, request.ProjectId);
                throw new ForbiddenAccessException("You do not have permission to update this project.");
            }

            // --- Business Rule Validation Example (for update)
          
            if (project.Name != request.ProjectDto.Name) // Only check if the name is actually changing
            {
                var existingProjectWithSameName = await projectRepository.AsQueryable()
                                                                          .AnyAsync(p => p.OwnerId == project.OwnerId && p.Name == request.ProjectDto.Name && p.Id != request.ProjectId, cancellationToken);
                if (existingProjectWithSameName)
                {
                    logger.LogWarning("Attempted to rename project {ProjectId} to duplicate name '{ProjectName}' for owner {OwnerId}.", request.ProjectId, request.ProjectDto.Name, project.OwnerId);
                    throw new ConflictException($"A project with the name '{request.ProjectDto.Name}' already exists for this user.");
                }
            }
  

            var originalName = project.Name;
            var originalDescription = project.Description;

            
            mapper.Map(request.ProjectDto, project); 

            await projectRepository.SaveChangesAsync(); 

            logger.LogInformation("Project {ProjectId} updated successfully.", request.ProjectId);

            // --- Invalidate Cache ---
            // Invalidate cache entries related to this specific project and lists it might appear in
            await cache.RemoveAsync($"project_{project.Id}", cancellationToken); // Invalidate specific project cache
            await cache.RemoveAsync($"projects_user_{project.OwnerId}", cancellationToken); // Invalidate list for this owner
            await cache.RemoveAsync("projects_all", cancellationToken); // Invalidate list for all (if admin view is cached)
                                                                         // Invalidate cache for tasks within this project if cached separately
            await cache.RemoveAsync($"tasks_project_{project.Id}", cancellationToken);
            logger.LogInformation("Invalidated project cache after update for ProjectId: {ProjectId}", project.Id);
         


        
            if (project.Name == originalName && project.Description == originalDescription)
                return Unit.Value; 

            await publishEndpoint.Publish(new ProjectUpdatedEvent()
            {
                ProjectId = project.Id,
                Name = project.Name,
                Description = project.Description,
                OwnerId = project.OwnerId,
                Timestamp = DateTime.UtcNow
            }, cancellationToken);
            logger.LogInformation("Published ProjectUpdatedEvent for ProjectId: {ProjectId}", project.Id);
      

            return Unit.Value; 
        }
    }
}