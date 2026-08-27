using ATIP.Application.Common.Security;
using ATIP.Application.Features.Explorer.Dtos;
using ATIP.Domain.Enums;
using MediatR;

namespace ATIP.Application.Features.Explorer.Commands.StartExploration;

/// <summary>
/// Creates an exploration session and enqueues it for background processing. Returns immediately
/// with a Pending session; callers poll the session endpoint to observe progress.
/// </summary>
public sealed record StartExplorationCommand : IRequest<ExplorationSessionDto>, IProjectScopedRequest
{
    public Guid ProjectId { get; init; }
    public Guid EnvironmentId { get; init; }

    /// <summary>Optional: drive the crawl only from this feature's manual/imported scenarios.</summary>
    public Guid? FeatureId { get; init; }

    /// <summary>Optional: run and record results for just this one scenario.</summary>
    public Guid? ScenarioId { get; init; }

    /// <summary>Optional: a free-text mission. When set, the agent autonomously performs the described
    /// flow and records it as a new reusable scenario (found under the "AI Explorations" feature).</summary>
    public string? Prompt { get; init; }

    /// <summary>Override the environment's base URL. Leave null to use the environment default.</summary>
    public string? SeedUrl { get; init; }

    public int MaxPages { get; init; } = 30;
    public int MaxDepth { get; init; } = 4;

    public ProjectRole RequiredRole => ProjectRole.Editor;
}
