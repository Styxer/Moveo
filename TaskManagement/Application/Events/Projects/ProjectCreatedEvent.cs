namespace Application.Events.Projects
{
    public record ProjectCreatedEvent
    {
        public Guid ProjectId { get; init; }
        public string Name { get; init; } = string.Empty;
        public string Description { get; init; } = string.Empty;
        public string OwnerId { get; init; } = string.Empty;
        public DateTime Timestamp { get; init; } = DateTime.UtcNow;
    }
}
