using FluentValidation;

namespace ATIP.Application.Features.Projects.Commands.CreateProject;

public sealed class CreateProjectCommandValidator : AbstractValidator<CreateProjectCommand>
{
    public CreateProjectCommandValidator()
    {
        RuleFor(x => x.Name)
            .NotEmpty()
            .MaximumLength(200);

        RuleFor(x => x.Key)
            .MaximumLength(60)
            .Matches("^[a-z0-9-]+$")
            .WithMessage("Key may only contain lowercase letters, digits and hyphens.")
            .When(x => !string.IsNullOrWhiteSpace(x.Key));

        RuleFor(x => x.Description)
            .MaximumLength(2000);
    }
}
