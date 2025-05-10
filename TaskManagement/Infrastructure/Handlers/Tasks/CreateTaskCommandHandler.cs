using MediatR;
using Microsoft.Extensions.Logging;
using TaskManagement.Application.Interfaces; // Reference repository interface
using TaskManagement.Application.DTOs.Tasks;
using TaskManagement.Domain.Models; // Reference Domain models
using System;
using System.Threading;
using System.Threading.Tasks;
using AutoMapper; // Use AutoMapper
// using TaskManagement.Application.Mapping; // Removed Mapperly interfaces
using Microsoft.EntityFrameworkCore; // Needed for AnyAsync
using MassTransit; // Added for MassTransit
// Reference custom exceptions
using Microsoft.Extensions.Caching.Distributed; // Needed for IDistributedCache
using System.Text.Json;
using Application.Commands.Tasks;
using Application.Exceptions; // Needed for JSON serialization/deserialization for caching

namespace TaskManagement.Infrastructure.Handlers.Tasks
{
    // Command Handler for creating a task
    public class CreateTaskCommandHandler : IRequestHandler<CreateTaskCommand, TaskDto>
    {
        private readonly IRepository<Task> _taskRepository; // Dependency on Application interface
        private readonly IRepository<Project> _projectRepository; // Need to check if project exists and is accessible
        private readonly ILogger<CreateTaskCommandHandler> _logger;
        // Inject AutoMapper's IMapper
        private readonly IMapper _mapper;
        // Inject IPublishEndpoint to publish events
        private readonly IPublishEndpoint _publishEndpoint;
        // Inject IDistributedCache for cache invalidation
        private readonly IDistributedCache _cache;


        public CreateTaskCommandHandler(IRepository<Task> taskRepository,
                                        IRepository<Project> projectRepository, // Inject Project Repository
                                        ILogger<CreateTaskCommandHandler> logger,
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

        public async Task<TaskDto> Handle(CreateTaskCommand request, CancellationToken cancellationToken)
        {
            _logger.LogInformation("Handling CreateTaskCommand for project {ProjectId}", request.ProjectId);

            // Check if the parent project exists (Access check is done in the controller before dispatching)
            var projectExists = await _projectRepository.AsQueryable().AnyAsync(p => p.Id == request.ProjectId, cancellationToken);
            if (!projectExists)
            {
                _logger.LogWarning("Attempted to create task for non-existent project {ProjectId}.", request.ProjectId);
                // Throw the custom NotFoundException
                throw new NotFoundException(nameof(Project), request.ProjectId);
            }

            // Additional validation specific to task creation within a project could go here
            // e.g., checking if the project is in a state that allows new tasks

            // Use AutoMapper
            var task = _mapper.Map<Task>(request.TaskDto);
            task.Id = Guid.NewGuid(); // Generate new ID
            task.ProjectId = request.ProjectId; // Assign to project

            await _taskRepository.AddAsync(task);
            await _taskRepository.SaveChangesAsync(); // Save the entity and the Outbox message in a single transaction

            _logger.LogInformation("Task {TaskId} created successfully in project {ProjectId}.", task.Id, request.ProjectId);

            // --- Invalidate Cache ---
            // Invalidate cache entries related to lists of tasks for this project
            await _cache.RemoveAsync($"tasks_project_{request.ProjectId}", cancellationToken); // Invalidate list for this project
            // Invalidate cache for the specific task if it was cached by ID (less likely for creation)
            // await _cache.RemoveAsync($"task_{task.Id}", cancellationToken);
            _logger.LogInformation("Invalidated task cache after creation for TaskId: {TaskId} in ProjectId: {ProjectId}", task.Id, request.ProjectId);
            // --- End Invalidate Cache ---


            // --- Publish the TaskCreatedEvent ---
            await _publishEndpoint.Publish(new Application.Events.Tasks.TaskCreatedEvent
            {
                TaskId = task.Id,
                ProjectId = task.ProjectId,
                Title = task.Title,
                Status = task.Status,
                Timestamp = DateTime.UtcNow
            }, cancellationToken);
            _logger.LogInformation("Published TaskCreatedEvent for TaskId: {TaskId}", task.Id);
            // --- End Publish ---

            // Use AutoMapper for the return value
            return _mapper.Map<TaskDto>(task);
        }
    }
}