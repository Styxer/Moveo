using Application.DTOs.Tasks;
using MediatR;

namespace Application.Queries.Tasks
{
    
    public class GetTaskByIdQuery(Guid taskId, string userId, bool isAdmin) : IRequest<TaskDto>
    {
        public Guid TaskId { get; } = taskId;
        public string UserId { get; } = userId; 
        public bool IsAdmin { get; } = isAdmin; /
    }
}