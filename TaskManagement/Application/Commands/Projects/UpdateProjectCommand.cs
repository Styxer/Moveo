using Application.DTOs.Projects;
using MediatR;

namespace Application.Commands.Projects
{
    // Command to update an existing project
    public class UpdateProjectCommand(Guid projectId, UpdateProjectRequestDto projectDto, string userId, bool isAdmin)
        : IRequest<Unit>
    {
        public Guid ProjectId { get; } = projectId;
        public UpdateProjectRequestDto ProjectDto { get; } = projectDto;
        public string UserId { get; } = userId;
        public bool IsAdmin { get; } = isAdmin;
    }
}