using Application.DTOs.Tasks;
using MediatR;

namespace Application.Commands.Tasks
{
    // Command to update an existing task
    public class UpdateTaskCommand(Guid taskId, UpdateTaskRequestDto taskDto, string userId, bool isAdmin)
        : IRequest<Unit>
    {
        public Guid TaskId { get; } = taskId;

        public UpdateTaskRequestDto TaskDto { get; } = taskDto;

        // User/Admin info needed for access check in handler
        public string UserId { get; } = userId;
        public bool IsAdmin { get; } = isAdmin;
    }
}