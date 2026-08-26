using ATIP.Application.Features.Environments.Dtos;
using MediatR;

namespace ATIP.Application.Features.Environments.Queries.GetEnvironmentVariables;

/// <summary>Gets the key/value variables defined on an environment.</summary>
public sealed record GetEnvironmentVariablesQuery(Guid ProjectId, Guid EnvironmentId)
    : IRequest<IReadOnlyList<EnvironmentVariableDto>>;
