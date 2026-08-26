using ATIP.Domain.Enums;
using FluentValidation;

namespace ATIP.Application.Features.Environments.Commands.UpdateEnvironment;

public sealed class UpdateEnvironmentCommandValidator : AbstractValidator<UpdateEnvironmentCommand>
{
    public UpdateEnvironmentCommandValidator()
    {
        RuleFor(x => x.Id).NotEmpty();
        RuleFor(x => x.ProjectId).NotEmpty();

        RuleFor(x => x.Name)
            .NotEmpty()
            .MaximumLength(120);

        RuleFor(x => x.Type)
            .NotEmpty()
            .Must(t => Enum.TryParse<EnvironmentType>(t, ignoreCase: true, out _))
            .WithMessage("Type must be one of: Development, Test, Staging, Production, Sandbox.");

        RuleFor(x => x.BaseUrl)
            .NotEmpty()
            .MaximumLength(2000)
            .Must(BeAValidHttpUrl)
            .WithMessage("BaseUrl must be a valid absolute http(s) URL.");
    }

    private static bool BeAValidHttpUrl(string url) =>
        Uri.TryCreate(url, UriKind.Absolute, out var uri) &&
        (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps);
}
