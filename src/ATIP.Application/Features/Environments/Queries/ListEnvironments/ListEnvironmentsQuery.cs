using ATIP.Application.Features.Environments.Dtos;
using MediatR;

namespace ATIP.Application.Features.Environments.Queries.ListEnvironments;

/// <summary>Lists all environments belonging to a project (small, unpaged set).</summary>
public sealed record ListEnvironmentsQuery(Guid ProjectId) : IRequest<IReadOnlyList<EnvironmentDto>>;
