using FluentValidation;

namespace ATIP.Application.Features.Requirements.Commands.CreateManualFeature;

public sealed class CreateManualFeatureCommandValidator : AbstractValidator<CreateManualFeatureCommand>
{
    public CreateManualFeatureCommandValidator()
    {
        RuleFor(x => x.ProjectId).NotEmpty();
        RuleFor(x => x.FeatureName).NotEmpty().MaximumLength(200);
        RuleFor(x => x.ModuleName).MaximumLength(200);
    }
}
