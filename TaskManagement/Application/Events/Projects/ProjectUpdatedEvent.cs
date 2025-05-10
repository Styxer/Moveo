using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Application.Events.Projects
{
    public record ProjectUpdatedEvent
    {
        public Guid ProjectId { get; init; }
        public string Name { get; init; } = string.Empty;
        public string Description { get; init; } = string.Empty;
        public string OwnerId { get; init; } = string.Empty;
        public DateTime Timestamp { get; init; } = DateTime.UtcNow;
    }
}
