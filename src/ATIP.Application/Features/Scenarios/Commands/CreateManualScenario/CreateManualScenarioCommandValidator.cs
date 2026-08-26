using FluentValidation;

namespace ATIP.Application.Features.Scenarios.Commands.CreateManualScenario;

public sealed class CreateManualScenarioCommandValidator : AbstractValidator<CreateManualScenarioCommand>
{
    public CreateManualScenarioCommandValidator()
    {
        RuleFor(x => x.ProjectId).NotEmpty();
        RuleFor(x => x.ProjectId).NotEmpty();
        RuleFor(x => x.Title).NotEmpty().MaximumLength(300);
        RuleFor(x => x.Steps).NotEmpty().WithMessage("Add at least one step.");
        RuleForEach(x => x.Steps).ChildRules(step =>
        {
            step.RuleFor(s => s.Action).NotEmpty().MaximumLength(1000);
        });
    }
}
