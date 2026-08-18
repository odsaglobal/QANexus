using ATIP.Domain.Enums;
using FluentValidation;

namespace ATIP.Application.Features.Projects.Commands.UpdateProject;

public sealed class UpdateProjectCommandValidator : AbstractValidator<UpdateProjectCommand>
{
    public UpdateProjectCommandValidator()
    {
        RuleFor(x => x.Id).NotEmpty();

        RuleFor(x => x.Name)
            .NotEmpty()
            .MaximumLength(200);

        RuleFor(x => x.Description)
            .MaximumLength(2000);

        RuleFor(x => x.Status)
            .NotEmpty()
            .Must(s => Enum.TryParse<ProjectStatus>(s, ignoreCase: true, out _))
            .WithMessage("Status must be one of: Active, Archived, Suspended.");
    }
}
