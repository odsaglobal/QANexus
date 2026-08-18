using FluentValidation;

namespace ATIP.Application.Features.Auth.Commands.RegisterTenant;

public sealed class RegisterTenantCommandValidator : AbstractValidator<RegisterTenantCommand>
{
    public RegisterTenantCommandValidator()
    {
        RuleFor(x => x.OrganizationName)
            .NotEmpty()
            .MaximumLength(200);

        RuleFor(x => x.DisplayName)
            .NotEmpty()
            .MaximumLength(200);

        RuleFor(x => x.Email)
            .NotEmpty()
            .EmailAddress()
            .MaximumLength(320);

        RuleFor(x => x.Password)
            .NotEmpty()
            .MinimumLength(10)
            .MaximumLength(128)
            .Matches("[A-Z]").WithMessage("Password must contain an uppercase letter.")
            .Matches("[a-z]").WithMessage("Password must contain a lowercase letter.")
            .Matches("[0-9]").WithMessage("Password must contain a digit.");
    }
}
