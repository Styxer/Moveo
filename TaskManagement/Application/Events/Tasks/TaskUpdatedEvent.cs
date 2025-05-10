using TaskStatus = Domain.Models.TaskStatus;
namespace Application.Events.Tasks
{
    public record TaskUpdatedEvent
    {
        public Guid TaskId { get; init; }
        public Guid ProjectId { get; init; }
        public string Title { get; init; } = string.Empty;
        public string Description { get; init; } = string.Empty;
        public TaskStatus Status { get; init; }
        public DateTime Timestamp { get; init; } = DateTime.UtcNow;
    }
}
