
using TaskStatus = Domain.Models.TaskStatus;

namespace Application.DTOs.Tasks
{
    
    public class TaskDto
    {
        public Guid Id { get; set; }
        public string Title { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public TaskStatus Status { get; set; } = TaskStatus.Todo;
        public Guid ProjectId { get; set; }
    }
}
