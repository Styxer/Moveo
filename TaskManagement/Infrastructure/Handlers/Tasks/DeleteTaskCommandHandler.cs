using Application.Commands.Tasks;
using Application.Events.Task;
using Application.Exceptions;
using Application.Interfaces;
using Domain.Models;
using MassTransit;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Logging;
using Task = Domain.Models.Task;


namespace Infrastructure.Handlers.Tasks
{
    // Command Handler for deleting a task
    public class DeleteTaskCommandHandler(
        IRepository<Task> taskRepository,
        IRepository<Project> projectRepository, 
        ILogger<DeleteTaskCommandHandler> logger,
        IPublishEndpoint publishEndpoint, 
        IDistributedCache cache)
        : IRequestHandler<DeleteTaskCommand, Unit>
    {
      
        private readonly IRepository<Project> _projectRepository = projectRepository; 

      


        // Inject IDistributedCache

        public async Task<Unit> Handle(DeleteTaskCommand request, CancellationToken cancellationToken)
        {
            logger.LogInformation("Handling DeleteTaskCommand for task {TaskId}", request.TaskId);

            
            var task = await taskRepository.AsQueryable()
                                            .Include(t => t.Project)
                                            .FirstOrDefaultAsync(t => t.Id == request.TaskId, cancellationToken);

            if (task == null)
            {
                logger.LogWarning("Attempted to delete non-existent task {TaskId}.", request.TaskId);
                throw new NotFoundException(nameof(Task), request.TaskId);
            }

            // RBAC: Check if user is admin or owner of the parent project
            if (!request.IsAdmin && task.Project?.OwnerId != request.UserId)
            {
                logger.LogWarning("User {UserId} attempted to delete task {TaskId} in project {ProjectId} without permission.", request.UserId, request.TaskId, task.ProjectId);
                throw new ForbiddenAccessException("You do not have permission to delete this task.");
            }

          

            var projectId = task.ProjectId; 

            taskRepository.Remove(task);
            await taskRepository.SaveChangesAsync(); 

            logger.LogInformation("Task {TaskId} deleted successfully.", request.TaskId);

        
            await cache.RemoveAsync($"task_{request.TaskId}", cancellationToken); // Invalidate specific task cache
            await cache.RemoveAsync($"tasks_project_{projectId}", cancellationToken); // Invalidate list for this project
            // Invalidate cache for the parent project if it includes task summaries
            await cache.RemoveAsync($"project_{projectId}", cancellationToken);
            await cache.RemoveAsync($"projects_user_{task.Project.OwnerId}", cancellationToken);
            await cache.RemoveAsync("projects_all", cancellationToken);
            logger.LogInformation("Invalidated task cache after deletion for TaskId: {TaskId} in ProjectId: {ProjectId}", request.TaskId, projectId);
      


        
            await publishEndpoint.Publish(new TaskDeletedEvent()
            {
                TaskId = request.TaskId,
                ProjectId = projectId,
                Timestamp = DateTime.UtcNow
            }, cancellationToken);
            logger.LogInformation("Published TaskDeletedEvent for TaskId: {TaskId}", request.TaskId);
          
            return Unit.Value;
        }
    }
}