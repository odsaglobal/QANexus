using FluentValidation;

namespace ATIP.Application.Features.Explorer.Commands.StartExploration;

public sealed class StartExplorationCommandValidator : AbstractValidator<StartExplorationCommand>
{
    public StartExplorationCommandValidator()
    {
        RuleFor(x => x.ProjectId).NotEmpty();
        RuleFor(x => x.EnvironmentId).NotEmpty();
        RuleFor(x => x.MaxPages).InclusiveBetween(1, 200);
        RuleFor(x => x.MaxDepth).InclusiveBetween(1, 10);
        RuleFor(x => x.SeedUrl)
            .Must(url => Uri.TryCreate(url, UriKind.Absolute, out var u) &&
                         (u.Scheme == Uri.UriSchemeHttp || u.Scheme == Uri.UriSchemeHttps))
            .WithMessage("SeedUrl must be a valid http(s) URL.")
            .When(x => !string.IsNullOrWhiteSpace(x.SeedUrl));
    }
}
