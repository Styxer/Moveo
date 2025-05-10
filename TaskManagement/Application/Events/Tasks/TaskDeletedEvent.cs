using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Application.Events.Task
{
    public record TaskDeletedEvent
    {
        public Guid TaskId { get; init; }
        public Guid ProjectId { get; init; }
        public DateTime Timestamp { get; init; } = DateTime.UtcNow;
    }
}
