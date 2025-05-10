
using TaskStatus = Domain.Models.TaskStatus;

namespace Application.Events.Task
{
    public record TaskCreatedEvent
    {
        public Guid TaskId { get; init; }
        public Guid ProjectId { get; init; }
        public string Title { get; init; } = string.Empty;
        public TaskStatus Status { get; init; }
        public DateTime Timestamp { get; init; } = DateTime.UtcNow;
    }
}
