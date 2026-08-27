using ATIP.Application.Common.Security;
using ATIP.Application.Features.Scenarios.Dtos;
using ATIP.Domain.Enums;
using MediatR;

namespace ATIP.Application.Features.Scenarios.Commands.GenerateScenariosFromStory;

/// <summary>
/// Generates manual test-case scenarios from a free-text story/description grounded in the project's
/// business-context documents. Scenarios are created under a hidden per-project "General" bucket.
/// </summary>
public sealed record GenerateScenariosFromStoryCommand : IRequest<IReadOnlyList<ScenarioDto>>, IProjectScopedRequest
{
    public Guid ProjectId { get; init; }

    public required string Story { get; init; }

    public ProjectRole RequiredRole => ProjectRole.Editor;
}
