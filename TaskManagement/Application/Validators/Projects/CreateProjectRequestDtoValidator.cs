using Application.DTOs.Projects;
using FluentValidation;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Application.Validators.Projects
{
    public class CreateProjectRequestDtoValidator : AbstractValidator<CreateProjectRequestDto>
    {
        public CreateProjectRequestDtoValidator()
        {
            RuleFor(x => x.Name)
                .NotEmpty().WithMessage("Project name is required.")
                .MaximumLength(100).WithMessage("Project name cannot exceed 100 characters.");

            RuleFor(x => x.Description)
                .MaximumLength(500).WithMessage("Project description cannot exceed 500 characters.");
        }
    }
}
