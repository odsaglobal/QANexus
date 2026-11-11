using ATIP.Application.Common.Interfaces;
using ATIP.Application.Features.Environments.Dtos;
using FluentValidation;

namespace ATIP.Application.Features.Environments.Commands.UpdateEnvironmentVariables;

public sealed class UpdateEnvironmentVariablesCommandValidator : AbstractValidator<UpdateEnvironmentVariablesCommand>
{
    public UpdateEnvironmentVariablesCommandValidator()
    {
        RuleForEach(x => x.Variables)
            .Where(v => EnvironmentVariableSerialization.NormalizeType(v.Type) == EnvironmentVariableSerialization.VaultType)
            .Must(v => string.IsNullOrWhiteSpace(v.Value) || SecretReference.TryParse(v.Value, out _))
            .WithMessage((_, v) => $"Vault variable '{v.Key}' must reference a secret as path/to/secret#field.");
    }
}
