using Application.Events.Projects;
using MassTransit;
using Microsoft.Extensions.Logging;

namespace Infrastructure.Consumers
{
    public class ProjectCreatedConsumer(ILogger<ProjectCreatedConsumer> logger) : IConsumer<ProjectCreatedEvent>
    {
       
        public Task Consume(ConsumeContext<ProjectCreatedEvent> context)
        {
            var message = context.Message;
            logger.LogInformation("Received ProjectCreatedEvent for ProjectId: {ProjectId}, Name: {Name}",
                message.ProjectId, message.Name);
           
            return Task.CompletedTask;
        }
    }
}
