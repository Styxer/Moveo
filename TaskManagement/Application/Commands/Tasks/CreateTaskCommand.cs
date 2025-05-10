using Application.DTOs.Tasks;
using MediatR;

namespace Application.Commands.Tasks
{
    // Command to create a new task within a project
    public class CreateTaskCommand(Guid projectId, CreateTaskRequestDto taskDto) : IRequest<TaskDto>
    {
        public Guid ProjectId { get; } = projectId;
        public CreateTaskRequestDto TaskDto { get; } = taskDto;
    }
}