using ATIP.Application.Common.Security;
using ATIP.Application.Features.DataConnections.Dtos;
using ATIP.Domain.Enums;
using MediatR;

namespace ATIP.Application.Features.DataConnections.Commands.CreateDataConnection;

/// <summary>Registers a database the engine may query during runs in this environment.</summary>
public sealed record CreateDataConnectionCommand : IRequest<DataConnectionDto>, IProjectScopedRequest
{
    public Guid ProjectId { get; init; }

    public Guid EnvironmentId { get; init; }

    /// <summary>The name steps reference, e.g. <c>orders</c>.</summary>
    public required string Name { get; init; }

    /// <summary>PostgreSql, SqlServer or MySql.</summary>
    public string Provider { get; init; } = nameof(DataProviderKind.PostgreSql);

    /// <summary>Plaintext connection string; encrypted before it is stored and never returned.</summary>
    public required string ConnectionString { get; init; }

    public int CommandTimeoutSeconds { get; init; } = 15;

    /// <summary>Defaults to read-only so a validation connection cannot mutate the system under test.</summary>
    public bool ReadOnly { get; init; } = true;

    // Connection strings carry credentials to a real database, so registering one is an
    // administrative act rather than an authoring act — the same bar as managing credentials.
    public ProjectRole RequiredRole => ProjectRole.Owner;
}
