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
    
    public class CreateProjectCommandHandler(
        IRepository<Project> projectRepository,
        ILogger<CreateProjectCommandHandler> logger,
        IMapper mapper,
        IPublishEndpoint publishEndpoint, 
        IDistributedCache cache)
        : IRequestHandler<CreateProjectCommand, ProjectDto>{
    




        public async Task<ProjectDto> Handle(CreateProjectCommand request, CancellationToken cancellationToken)
        {
            logger.LogInformation("Handling CreateProjectCommand for owner {OwnerId}", request.OwnerId);

          
            var existingProject = await projectRepository.AsQueryable()
                                                          .AnyAsync(p => p.OwnerId == request.OwnerId && p.Name == request.ProjectDto.Name, cancellationToken);
            if (existingProject)
            {
                logger.LogWarning("Attempted to create duplicate project name '{ProjectName}' for owner {OwnerId}.", request.ProjectDto.Name, request.OwnerId);
            
                throw new ConflictException($"A project with the name '{request.ProjectDto.Name}' already exists for this user.");
            }
          


            // Use AutoMapper
            var project = mapper.Map<Project>(request.ProjectDto);
            project.Id = Guid.NewGuid(); 
            project.OwnerId = request.OwnerId; 

            await projectRepository.AddAsync(project);
            await projectRepository.SaveChangesAsync();
            logger.LogInformation("Project {ProjectId} created successfully.", project.Id);

            // --- Invalidate Cache ---
        
            // For simplicity here, i'll invalidate a few common keys. In a real app, i need a more robust key generation/invalidation strategy.
            await cache.RemoveAsync($"projects_user_{request.OwnerId}", cancellationToken); 
            await cache.RemoveAsync("projects_all", cancellationToken); 
     
            logger.LogInformation("Invalidated project cache after creation for ProjectId: {ProjectId}", project.Id);
    


            // --- Publish the ProjectCreatedEvent ---
            
            await publishEndpoint.Publish(new Application.Events.Projects.ProjectCreatedEvent
            {
                ProjectId = project.Id,
                Name = project.Name,
                Description = project.Description,
                OwnerId = project.OwnerId,
                Timestamp = DateTime.UtcNow // Include timestamp
            }, cancellationToken);
            logger.LogInformation("Published ProjectCreatedEvent for ProjectId: {ProjectId}", project.Id);
       

        
            return mapper.Map<ProjectDto>(project);
        }
    }
}