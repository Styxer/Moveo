using MassTransit;
using Microsoft.Extensions.Logging;

using System.Threading.Tasks;
using Application.Events.Task;

namespace Infrastructure.Consumers
{
    namespace TaskManagement.Infrastructure.Consumers
    {
        // Consumer to handle the TaskCreatedEvent
        public class TaskCreatedConsumer(ILogger<TaskCreatedConsumer> logger) : IConsumer<TaskCreatedEvent>
        {
            public Task Consume(ConsumeContext<TaskCreatedEvent> context)
            {
                var message = context.Message;
                logger.LogInformation("Received TaskCreatedEvent for TaskId: {TaskId}, Title: {Title}, ProjectId: {ProjectId}",
                    message.TaskId, message.Title, message.ProjectId);

                // Implement logic to react to a task being created, e.g.:
                // - Update a project's task count in a read model.
                // - Notify team members.

                return Task.CompletedTask;
            }
        }
    }
}
