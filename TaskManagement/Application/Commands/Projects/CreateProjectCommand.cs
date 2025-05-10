using Application.DTOs.Projects;
using MediatR;

namespace Application.Commands.Projects
{
    // Command to create a new project
    public class CreateProjectCommand(CreateProjectRequestDto projectDto, string ownerId) : IRequest<ProjectDto>
    {
        public CreateProjectRequestDto ProjectDto { get; } = projectDto;
        public string OwnerId { get; } = ownerId;
    }
}