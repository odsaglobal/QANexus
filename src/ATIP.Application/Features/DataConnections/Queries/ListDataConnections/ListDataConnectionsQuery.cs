using ATIP.Application.Features.DataConnections.Dtos;
using MediatR;

namespace ATIP.Application.Features.DataConnections.Queries.ListDataConnections;

/// <summary>Lists the data connections registered for an environment.</summary>
public sealed record ListDataConnectionsQuery(Guid ProjectId, Guid EnvironmentId)
    : IRequest<IReadOnlyList<DataConnectionDto>>;
