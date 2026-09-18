using ATIP.Application.Common.Security;
using ATIP.Application.Features.DataConnections.Dtos;
using ATIP.Domain.Enums;
using MediatR;

namespace ATIP.Application.Features.DataConnections.Commands.UpdateDataConnection;

/// <summary>Updates a data connection. The connection string is only replaced when one is supplied.</summary>
public sealed record UpdateDataConnectionCommand : IRequest<DataConnectionDto>, IProjectScopedRequest
{
    public Guid Id { get; init; }

    public Guid ProjectId { get; init; }

    public Guid EnvironmentId { get; init; }

    public required string Name { get; init; }

    /// <summary>PostgreSql, SqlServer or MySql.</summary>
    public string Provider { get; init; } = nameof(DataProviderKind.PostgreSql);

    /// <summary>
    /// Leave null to keep the stored secret. This is what lets the edit form round-trip without
    /// ever having to display, or ask the user to retype, a password it cannot show them.
    /// </summary>
    public string? ConnectionString { get; init; }

    public int CommandTimeoutSeconds { get; init; } = 15;

    public bool ReadOnly { get; init; } = true;

    public ProjectRole RequiredRole => ProjectRole.Owner;
}
