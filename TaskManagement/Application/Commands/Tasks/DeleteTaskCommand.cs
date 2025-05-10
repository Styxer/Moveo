using MediatR;

namespace Application.Commands.Tasks
{
    // Command to delete a task
    public class DeleteTaskCommand(Guid taskId, string userId, bool isAdmin) : IRequest<Unit>
    {
        public Guid TaskId { get; } = taskId;

        // User/Admin info needed for access check in handler
        public string UserId { get; } = userId;
        public bool IsAdmin { get; } = isAdmin;
    }
}