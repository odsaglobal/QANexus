using ATIP.Application.Common.Security;
using ATIP.Application.Features.Environments.Dtos;
using ATIP.Domain.Enums;
using MediatR;

namespace ATIP.Application.Features.Environments.Commands.UpdateEnvironmentVariables;

/// <summary>Replaces the full set of key/value variables on an environment.</summary>
public sealed record UpdateEnvironmentVariablesCommand : IRequest<IReadOnlyList<EnvironmentVariableDto>>, IProjectScopedRequest
{
    public Guid ProjectId { get; init; }

    public Guid EnvironmentId { get; init; }

    public List<EnvironmentVariableDto> Variables { get; init; } = new();

    public ProjectRole RequiredRole => ProjectRole.Editor;
}
