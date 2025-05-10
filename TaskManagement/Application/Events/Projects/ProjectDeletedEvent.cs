namespace Application.Events.Projects
{
    public record ProjectDeletedEvent
    {
        public Guid ProjectId { get; init; }
        public string OwnerId { get; init; } = string.Empty;
        public DateTime Timestamp { get; init; } = DateTime.UtcNow;
    }
}
