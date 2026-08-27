using ATIP.Application.Common.Security;
using ATIP.Application.Features.Environments.Dtos;
using ATIP.Domain.Enums;
using MediatR;

namespace ATIP.Application.Features.Environments.Commands.CreateEnvironment;

/// <summary>Adds a new environment (deployment target) to a project.</summary>
public sealed record CreateEnvironmentCommand : IRequest<EnvironmentDto>, IProjectScopedRequest
{
    public Guid ProjectId { get; init; }

    public required string Name { get; init; }

    /// <summary>One of the <c>EnvironmentType</c> names: Development, Test, Staging, Production, Sandbox.</summary>
    public required string Type { get; init; }

    public required string BaseUrl { get; init; }

    public bool IsDefault { get; init; }

    public ProjectRole RequiredRole => ProjectRole.Owner;
}
