using ATIP.Domain.Enums;
using FluentValidation;

namespace ATIP.Application.Features.Scenarios.Commands.UpdateScenario;

public sealed class UpdateScenarioCommandValidator : AbstractValidator<UpdateScenarioCommand>
{
    public UpdateScenarioCommandValidator()
    {
        RuleFor(x => x.Id).NotEmpty();

        RuleFor(x => x.Title).NotEmpty().MaximumLength(300);

        RuleFor(x => x.Type)
            .NotEmpty()
            .Must(v => Enum.TryParse<ScenarioType>(v, ignoreCase: true, out _))
            .WithMessage("Invalid scenario type.");

        RuleFor(x => x.Priority)
            .NotEmpty()
            .Must(v => Enum.TryParse<Priority>(v, ignoreCase: true, out _))
            .WithMessage("Invalid priority.");

        RuleFor(x => x.Risk)
            .NotEmpty()
            .Must(v => Enum.TryParse<RiskLevel>(v, ignoreCase: true, out _))
            .WithMessage("Invalid risk level.");

        RuleFor(x => x.Steps)
            .NotNull()
            .Must(s => s.Count <= 50)
            .WithMessage("A scenario may not have more than 50 steps.");

        RuleForEach(x => x.Steps)
            .ChildRules(step =>
            {
                step.RuleFor(s => s.Action).NotEmpty().MaximumLength(1000);
                step.RuleFor(s => s.ExpectedResult).MaximumLength(1000);
            });
    }
}
