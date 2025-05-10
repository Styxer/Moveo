using Application.DTOs.Pagination;
using Application.DTOs.Projects;
using MediatR;

namespace Application.Queries.Projects
{
    public class GetAllProjectsQuery(string userId, bool isAdmin, ProjectQueryParameters queryParameters)
        : IRequest<PagedResultDto<ProjectDto>>
    {
        public string UserId { get; } = userId;
        public bool IsAdmin { get; } = isAdmin;
        public ProjectQueryParameters QueryParameters { get; } = queryParameters;
    }
}