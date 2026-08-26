using ATIP.Application.Features.Environments.Dtos;
using MediatR;

namespace ATIP.Application.Features.Environments.Commands.UpdateEnvironmentVariables;

/// <summary>Replaces the full set of key/value variables on an environment.</summary>
public sealed record UpdateEnvironmentVariablesCommand : IRequest<IReadOnlyList<EnvironmentVariableDto>>
{
    public Guid ProjectId { get; init; }

    public Guid EnvironmentId { get; init; }

    public List<EnvironmentVariableDto> Variables { get; init; } = new();
}
