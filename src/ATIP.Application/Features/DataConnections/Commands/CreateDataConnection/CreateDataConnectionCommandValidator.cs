using ATIP.Domain.Enums;
using FluentValidation;

namespace ATIP.Application.Features.DataConnections.Commands.CreateDataConnection;

public sealed class CreateDataConnectionCommandValidator : AbstractValidator<CreateDataConnectionCommand>
{
    public CreateDataConnectionCommandValidator()
    {
        RuleFor(x => x.ProjectId).NotEmpty();
        RuleFor(x => x.EnvironmentId).NotEmpty();

        RuleFor(x => x.Provider)
            .NotEmpty()
            .Must(v => Enum.TryParse<DataProviderKind>(v, ignoreCase: true, out _))
            .WithMessage("Invalid provider. Use PostgreSql, SqlServer or MySql.");

        RuleFor(x => x.Name)
            .NotEmpty()
            .MaximumLength(120)
            .Matches("^[A-Za-z0-9._-]+$")
            .WithMessage("Use letters, digits, dot, underscore or hyphen — the name is referenced verbatim from steps.");

        RuleFor(x => x.ConnectionString)
            .NotEmpty()
            .MaximumLength(4000);

        RuleFor(x => x.CommandTimeoutSeconds)
            .InclusiveBetween(1, 300);
    }
}
