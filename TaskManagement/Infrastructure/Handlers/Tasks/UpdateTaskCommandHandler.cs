using MediatR;
using Microsoft.Extensions.Logging;
using MassTransit;
using Application.Commands.Tasks;
using Application.Exceptions;
using Application.Interfaces;
using AutoMapper;
using Domain.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Distributed;
using Task = Domain.Models.Task;
using Application.Events.Task;
using Application.Events.Tasks;


namespace TaskManagement.Infrastructure.Handlers.Tasks
{
    // Command Handler for updating a task
    public class UpdateTaskCommandHandler(
        IRepository<Task> taskRepository,
        IRepository<Project> projectRepository, 
        ILogger<UpdateTaskCommandHandler> logger,
        IMapper mapper, 
        IPublishEndpoint publishEndpoint,
        IDistributedCache cache)
        : IRequestHandler<UpdateTaskCommand, Unit>{
      

        public async Task<Unit> Handle(UpdateTaskCommand request, CancellationToken cancellationToken)
        {
            logger.LogInformation("Handling UpdateTaskCommand for task {TaskId}", request.TaskId);

            // Fetch the task including its parent project for access check
            var task = await taskRepository.AsQueryable()
                                            .Include(t => t.Project)
                                            .FirstOrDefaultAsync(t => t.Id == request.TaskId, cancellationToken);

            if (task == null)
            {
                logger.LogWarning("Attempted to update non-existent task {TaskId}.", request.TaskId);
                throw new NotFoundException(nameof(Task), request.TaskId);
            }
      
            if (!request.IsAdmin && task.Project.OwnerId != request.UserId)
            {
                logger.LogWarning("User {UserId} attempted to update task {TaskId} in project {ProjectId} without permission.", request.UserId, request.TaskId, task.ProjectId);
                throw new ForbiddenAccessException("You do not have permission to update this task.");
            }

           
            var originalTitle = task.Title;
            var originalDescription = task.Description;
            var originalStatus = task.Status;

          
            mapper.Map(request.TaskDto, task);

            await taskRepository.SaveChangesAsync(); 

            logger.LogInformation("Task {TaskId} updated successfully.", request.TaskId);

            // --- Invalidate Cache ---
            // Invalidate cache entries related to this specific task and lists it might appear in
            await cache.RemoveAsync($"task_{task.Id}", cancellationToken); // Invalidate specific task cache
            await cache.RemoveAsync($"tasks_project_{task.ProjectId}", cancellationToken); // Invalidate list for this project
            // Invalidate cache for the parent project if it includes task summaries
            await cache.RemoveAsync($"project_{task.ProjectId}", cancellationToken);
            await cache.RemoveAsync($"projects_user_{task.Project.OwnerId}", cancellationToken);
            await cache.RemoveAsync("projects_all", cancellationToken);
            logger.LogInformation("Invalidated task cache after update for TaskId: {TaskId} in ProjectId: {ProjectId}", task.Id, task.ProjectId);


            if (task.Title == originalTitle && task.Description == originalDescription && task.Status == originalStatus)
                return Unit.Value; 
            await publishEndpoint.Publish(new TaskUpdatedEvent()
            {
                TaskId = task.Id,
                ProjectId = task.ProjectId,
                Title = task.Title,
                Description = task.Description,
                Status = task.Status,
                Timestamp = DateTime.UtcNow
            }, cancellationToken);
            logger.LogInformation("Published TaskUpdatedEvent for TaskId: {TaskId}", task.Id);
           

            return Unit.Value; 
        }
    }
}