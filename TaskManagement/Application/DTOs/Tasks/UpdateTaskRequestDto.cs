using System.ComponentModel.DataAnnotations;
using TaskStatus = Domain.Models.TaskStatus;

namespace Application.DTOs.Tasks
{
    public class UpdateTaskRequestDto
    {
        [Required] [MaxLength(100)] 
        public string Title { get; set; } = string.Empty;
        [MaxLength(500)]
        public string Description { get; set; } = string.Empty;
        [Required]
        public TaskStatus Status { get; set; } = TaskStatus.Todo;
    }
}
