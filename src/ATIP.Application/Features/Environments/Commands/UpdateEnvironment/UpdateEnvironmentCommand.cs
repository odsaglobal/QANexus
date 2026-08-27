using ATIP.Application.Common.Security;
using ATIP.Application.Features.Environments.Dtos;
using ATIP.Domain.Enums;
using MediatR;

namespace ATIP.Application.Features.Environments.Commands.UpdateEnvironment;

/// <summary>Updates an existing environment's name, type, base URL and default flag.</summary>
public sealed record UpdateEnvironmentCommand : IRequest<EnvironmentDto>, IProjectScopedRequest
{
    public Guid Id { get; init; }

    public Guid ProjectId { get; init; }

    public required string Name { get; init; }

    /// <summary>One of the <c>EnvironmentType</c> names: Development, Test, Staging, Production, Sandbox.</summary>
    public required string Type { get; init; }

    public required string BaseUrl { get; init; }

    public bool IsDefault { get; init; }

    public ProjectRole RequiredRole => ProjectRole.Owner;
}
