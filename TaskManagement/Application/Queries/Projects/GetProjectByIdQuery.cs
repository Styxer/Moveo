using Application.DTOs.Projects;
using MediatR;

namespace Application.Queries.Projects
{
    public class GetProjectByIdQuery(Guid projectId, string userId, bool isAdmin) : IRequest<ProjectDto>
    {
        public Guid ProjectId { get; } = projectId;
        public string UserId { get; } = userId;
        public bool IsAdmin { get; } = isAdmin;
    }
}