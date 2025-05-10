using Application.DTOs.Pagination;
using Application.DTOs.Tasks;
using MediatR;

namespace Application.Queries.Tasks {

    public class GetTasksByProjectIdQuery(
        Guid projectId,
        string userId,
        bool isAdmin,
        TaskQueryParameters queryParameters)
        : IRequest<PagedResultDto<TaskDto>>
    {
        public Guid ProjectId { get; } = projectId;
        public string UserId { get; } = userId; 
        public bool IsAdmin { get; } = isAdmin; 
        public TaskQueryParameters QueryParameters { get; } = queryParameters;
    }
}