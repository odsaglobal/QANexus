using ATIP.Domain.Entities;
using ATIP.Domain.Enums;

namespace ATIP.Application.Features.DataConnections.Dtos;

/// <summary>Read model for an environment-scoped database connection.</summary>
/// <remarks>
/// The connection string is deliberately absent. It normally embeds a password, and once a secret
/// is readable over the API it ends up in browser history, screenshots and support tickets. Callers
/// that need to change it send a new one; nothing ever needs to read the old one back.
/// </remarks>
public sealed record DataConnectionDto
{
    public required Guid Id { get; init; }

    public required Guid ProjectId { get; init; }

    public required Guid EnvironmentId { get; init; }

    public required string Name { get; init; }

    /// <summary>PostgreSql, SqlServer or MySql.</summary>
    public required string Provider { get; init; }

    public required int CommandTimeoutSeconds { get; init; }
    public required bool ReadOnly { get; init; }

    public required DateTimeOffset CreatedAtUtc { get; init; }

    public DateTimeOffset? UpdatedAtUtc { get; init; }

    public static DataConnectionDto FromEntity(DataConnection e) => new()
    {
        Id = e.Id,
        ProjectId = e.ProjectId,
        EnvironmentId = e.EnvironmentId,
        Name = e.Name,
        Provider = e.Provider.ToString(),
        CommandTimeoutSeconds = e.CommandTimeoutSeconds,
        ReadOnly = e.ReadOnly,
        CreatedAtUtc = e.CreatedAtUtc,
        UpdatedAtUtc = e.UpdatedAtUtc
    };
}
