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
// using AutoMapper; // Removed AutoMapper
// using TaskManagement.Application.Mapping; // Removed Mapperly interfaces
// Reference custom exceptions
using Microsoft.Extensions.Caching.Distributed; // Needed for IDistributedCache
using System.Text.Json;
using Application.Commands.Tasks;
using Application.Exceptions; // Needed for JSON serialization/deserialization for caching
using AutoMapper; // Use AutoMapper

namespace TaskManagement.Infrastructure.Handlers.Tasks
{
    // Command Handler for updating a task
    public class UpdateTaskCommandHandler : IRequestHandler<UpdateTaskCommand, Unit>
    {
        private readonly IRepository<Task> _taskRepository; // Dependency on Application interface
        private readonly IRepository<Project> _projectRepository; // Need to check project access
        private readonly ILogger<UpdateTaskCommandHandler> _logger;
        // Inject AutoMapper's IMapper
        private readonly IMapper _mapper;
        // Inject IPublishEndpoint to publish events
        private readonly IPublishEndpoint _publishEndpoint;
        // Inject IDistributedCache for cache invalidation
        private readonly IDistributedCache _cache;


        public UpdateTaskCommandHandler(IRepository<Task> taskRepository,
                                        IRepository<Project> projectRepository, // Inject Project Repository
                                        ILogger<UpdateTaskCommandHandler> logger,
                                        IMapper mapper, // Inject AutoMapper's IMapper
                                        IPublishEndpoint publishEndpoint, // Inject IPublishEndpoint
                                        IDistributedCache cache) // Inject IDistributedCache
        {
            _taskRepository = taskRepository;
            _projectRepository = projectRepository;
            _logger = logger;
            _mapper = mapper;
            _publishEndpoint = publishEndpoint;
            _cache = cache;
        }

        public async Task<Unit> Handle(UpdateTaskCommand request, CancellationToken cancellationToken)
        {
            _logger.LogInformation("Handling UpdateTaskCommand for task {TaskId}", request.TaskId);

            // Fetch the task including its parent project for access check
            var task = await _taskRepository.AsQueryable()
                                            .Include(t => t.Project)
                                            .FirstOrDefaultAsync(t => t.Id == request.TaskId, cancellationToken);

            if (task == null)
            {
                _logger.LogWarning("Attempted to update non-existent task {TaskId}.", request.TaskId);
                // Throw the custom NotFoundException
                throw new NotFoundException(nameof(Task), request.TaskId);
            }

            // RBAC: Check if user is admin or owner of the parent project
            if (!request.IsAdmin && task.Project.OwnerId != request.UserId)
            {
                _logger.LogWarning("User {UserId} attempted to update task {TaskId} in project {ProjectId} without permission.", request.UserId, request.TaskId, task.ProjectId);
                // Throw the custom ForbiddenAccessException
                throw new ForbiddenAccessException("You do not have permission to update this task.");
            }

            // Additional validation specific to task updates could go here
            // e.g., checking if the status transition is allowed

            // Store original values before mapping for the event
            var originalTitle = task.Title;
            var originalDescription = task.Description;
            var originalStatus = task.Status;

            // Use AutoMapper to update the existing entity
            _mapper.Map(request.TaskDto, task); // AutoMapper can map to existing objects

            await _taskRepository.SaveChangesAsync(); // Save the entity and the Outbox message in a single transaction

            _logger.LogInformation("Task {TaskId} updated successfully.", request.TaskId);

            // --- Invalidate Cache ---
            // Invalidate cache entries related to this specific task and lists it might appear in
            await _cache.RemoveAsync($"task_{task.Id}", cancellationToken); // Invalidate specific task cache
            await _cache.RemoveAsync($"tasks_project_{task.ProjectId}", cancellationToken); // Invalidate list for this project
            // Invalidate cache for the parent project if it includes task summaries
            await _cache.RemoveAsync($"project_{task.ProjectId}", cancellationToken);
            await _cache.RemoveAsync($"projects_user_{task.Project.OwnerId}", cancellationToken);
            await _cache.RemoveAsync("projects_all", cancellationToken);
            _logger.LogInformation("Invalidated task cache after update for TaskId: {TaskId} in ProjectId: {ProjectId}", task.Id, task.ProjectId);
            // --- End Invalidate Cache ---


            // --- Publish the TaskUpdatedEvent ---
            // Only publish if relevant properties changed
            if (task.Title != originalTitle || task.Description != originalDescription || task.Status != originalStatus)
            {
                await _publishEndpoint.Publish(new Application.Events.Tasks.TaskUpdatedEvent
                {
                    TaskId = task.Id,
                    ProjectId = task.ProjectId,
                    Title = task.Title,
                    Description = task.Description,
                    Status = task.Status,
                    Timestamp = DateTime.UtcNow
                }, cancellationToken);
                _logger.LogInformation("Published TaskUpdatedEvent for TaskId: {TaskId}", task.Id);
            }
            // --- End Publish ---

            return Unit.Value; // Indicate success
        }
    }
}