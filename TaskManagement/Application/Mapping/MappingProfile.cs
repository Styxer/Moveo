using Application.DTOs.Projects;
using Application.DTOs.Tasks;
using AutoMapper;


namespace Application.Mapping
{
    // AutoMapper Profile for mapping between Domain models and Application DTOs
    public class MappingProfile : Profile
    {
        public MappingProfile()
        {
            // Create maps for Project entities and DTOs
            CreateMap<CreateProjectRequestDto, Project>();
            CreateMap<UpdateProjectRequestDto, Project>();
            CreateMap<Project, ProjectDto>();

            // Create maps for Task entities and DTOs
            CreateMap<CreateTaskRequestDto, Task>();
            CreateMap<UpdateTaskRequestDto, Task>();
            CreateMap<Task, TaskDto>();
        }
    }
}