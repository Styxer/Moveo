using Application.Commands.Tasks;
using Application.DTOs.Tasks;
using Application.Events.Task;
using Application.Exceptions;
using Application.Interfaces;
using AutoMapper;
using Domain.Models;
using MassTransit;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Logging;
using Task = Domain.Models.Task;



namespace Infrastructure.Handlers.Tasks
{
    // Command Handler for creating a task
    public class CreateTaskCommandHandler(
        IRepository<Task> taskRepository,
        IRepository<Project> projectRepository, 
        ILogger<CreateTaskCommandHandler> logger,
        IMapper mapper, 
        IPublishEndpoint publishEndpoint, 
        IDistributedCache cache)
        : IRequestHandler<CreateTaskCommand, TaskDto>{
  
        
        public async Task<TaskDto> Handle(CreateTaskCommand request, CancellationToken cancellationToken)
        {
            logger.LogInformation("Handling CreateTaskCommand for project {ProjectId}", request.ProjectId);

            // Check if the parent project exists (Access check is done in the controller before dispatching)
            var projectExists = await projectRepository.AsQueryable().AnyAsync(p => p.Id == request.ProjectId, cancellationToken);
            if (!projectExists)
            {
                logger.LogWarning("Attempted to create task for non-existent project {ProjectId}.", request.ProjectId);
                throw new NotFoundException(nameof(Project), request.ProjectId);
            }
            
            var task = mapper.Map<Task>(request.TaskDto);
            task.Id = Guid.NewGuid();
            task.ProjectId = request.ProjectId;

            await taskRepository.AddAsync(task);
            await taskRepository.SaveChangesAsync();

            logger.LogInformation("Task {TaskId} created successfully in project {ProjectId}.", task.Id, request.ProjectId);

            // --- Invalidate Cache ---
       
            await cache.RemoveAsync($"tasks_project_{request.ProjectId}", cancellationToken); // Invalidate list for this project/
            logger.LogInformation("Invalidated task cache after creation for TaskId: {TaskId} in ProjectId: {ProjectId}", task.Id, request.ProjectId);
           
            
            await publishEndpoint.Publish(new TaskCreatedEvent()
            {
                TaskId = task.Id,
                ProjectId = task.ProjectId,
                Title = task.Title,
                Status = task.Status,
                Timestamp = DateTime.UtcNow
            }, cancellationToken);
            logger.LogInformation("Published TaskCreatedEvent for TaskId: {TaskId}", task.Id);
       
            return mapper.Map<TaskDto>(task);
        }
    }
}