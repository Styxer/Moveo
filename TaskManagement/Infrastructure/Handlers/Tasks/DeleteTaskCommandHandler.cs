using MediatR;
using Microsoft.Extensions.Logging;
using TaskManagement.Application.Interfaces; // Reference repository interface
using TaskManagement.Domain.Models; // Reference Domain models
using System;
using System.Collections.Generic; // Needed for KeyNotFoundException
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore; // Needed for Include
using MassTransit; // Added for MassTransit
// Reference custom exceptions
using Microsoft.Extensions.Caching.Distributed; // Needed for IDistributedCache
using System.Text.Json;
using Application.Commands.Tasks;
using Application.Exceptions; // Needed for JSON serialization/deserialization for caching

namespace TaskManagement.Infrastructure.Handlers.Tasks
{
    // Command Handler for deleting a task
    public class DeleteTaskCommandHandler : IRequestHandler<DeleteTaskCommand, Unit>
    {
        private readonly IRepository<Task> _taskRepository; // Dependency on Application interface
        private readonly IRepository<Project> _projectRepository; // Need to check project access
        private readonly ILogger<DeleteTaskCommandHandler> _logger;
        // Inject IPublishEndpoint to publish events
        private readonly IPublishEndpoint _publishEndpoint;
        // Inject IDistributedCache for cache invalidation
        private readonly IDistributedCache _cache;


        public DeleteTaskCommandHandler(IRepository<Task> taskRepository,
                                        IRepository<Project> projectRepository, // Inject Project Repository
                                        ILogger<DeleteTaskCommandHandler> logger,
                                        IPublishEndpoint publishEndpoint, // Inject IPublishEndpoint
                                        IDistributedCache cache) // Inject IDistributedCache
        {
            _taskRepository = taskRepository;
            _projectRepository = projectRepository;
            _logger = logger;
            _publishEndpoint = publishEndpoint;
            _cache = cache;
        }

        public async Task<Unit> Handle(DeleteTaskCommand request, CancellationToken cancellationToken)
        {
            _logger.LogInformation("Handling DeleteTaskCommand for task {TaskId}", request.TaskId);

            // Fetch the task including its parent project for access check
            var task = await _taskRepository.AsQueryable()
                                            .Include(t => t.Project)
                                            .FirstOrDefaultAsync(t => t.Id == request.TaskId, cancellationToken);

            if (task == null)
            {
                _logger.LogWarning("Attempted to delete non-existent task {TaskId}.", request.TaskId);
                // Throw the custom NotFoundException
                throw new NotFoundException(nameof(Task), request.TaskId);
            }

            // RBAC: Check if user is admin or owner of the parent project
            if (!request.IsAdmin && task.Project.OwnerId != request.UserId)
            {
                _logger.LogWarning("User {UserId} attempted to delete task {TaskId} in project {ProjectId} without permission.", request.UserId, request.TaskId, task.ProjectId);
                // Throw the custom ForbiddenAccessException
                throw new ForbiddenAccessException("You do not have permission to delete this task.");
            }

            // Additional validation specific to task deletion could go here
            // e.g., preventing deletion if the task is in a certain status

            var projectId = task.ProjectId; // Store ProjectId before removing the task

            _taskRepository.Remove(task);
            await _taskRepository.SaveChangesAsync(); // Save the entity and the Outbox message in a single transaction

            _logger.LogInformation("Task {TaskId} deleted successfully.", request.TaskId);

            // --- Invalidate Cache ---
            // Invalidate cache entries related to this specific task and lists it might appear in
            await _cache.RemoveAsync($"task_{request.TaskId}", cancellationToken); // Invalidate specific task cache
            await _cache.RemoveAsync($"tasks_project_{projectId}", cancellationToken); // Invalidate list for this project
            // Invalidate cache for the parent project if it includes task summaries
            await _cache.RemoveAsync($"project_{projectId}", cancellationToken);
            await _cache.RemoveAsync($"projects_user_{task.Project.OwnerId}", cancellationToken);
            await _cache.RemoveAsync("projects_all", cancellationToken);
            _logger.LogInformation("Invalidated task cache after deletion for TaskId: {TaskId} in ProjectId: {ProjectId}", request.TaskId, projectId);
            // --- End Invalidate Cache ---


            // --- Publish the TaskDeletedEvent ---
            await _publishEndpoint.Publish(new Application.Events.Tasks.TaskDeletedEvent
            {
                TaskId = request.TaskId,
                ProjectId = projectId,
                Timestamp = DateTime.UtcNow
            }, cancellationToken);
            _logger.LogInformation("Published TaskDeletedEvent for TaskId: {TaskId}", request.TaskId);
            // --- End Publish ---

            return Unit.Value; // Indicate success
        }
    }
}