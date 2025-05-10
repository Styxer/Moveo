using MediatR;

namespace Application.Commands.Projects
{
    // Command to delete a project
    public class DeleteProjectCommand(Guid projectId, string userId, bool isAdmin) : IRequest<Unit>
    {
        public Guid ProjectId { get; } = projectId;
        public string UserId { get; } = userId;
        public bool IsAdmin { get; } = isAdmin;
    }
}