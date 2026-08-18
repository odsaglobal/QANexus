using FluentValidation;

namespace ATIP.Application.Features.Scenarios.Commands.SyncTestRailScenarios;

public sealed class SyncTestRailScenariosCommandValidator : AbstractValidator<SyncTestRailScenariosCommand>
{
    public SyncTestRailScenariosCommandValidator()
    {
        RuleFor(x => x.ProjectId).NotEmpty();
        RuleFor(x => x.FeatureId).NotEmpty();
        RuleFor(x => x.TestRailProjectId).GreaterThan(0);
        RuleFor(x => x.SuiteId).GreaterThan(0).When(x => x.SuiteId.HasValue);
        RuleFor(x => x.SectionId).GreaterThan(0).When(x => x.SectionId.HasValue);
    }
}
