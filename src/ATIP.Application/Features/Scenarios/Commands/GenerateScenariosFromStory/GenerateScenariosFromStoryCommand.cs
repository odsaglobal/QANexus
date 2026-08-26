using ATIP.Application.Features.Scenarios.Dtos;
using MediatR;

namespace ATIP.Application.Features.Scenarios.Commands.GenerateScenariosFromStory;

/// <summary>
/// Generates manual test-case scenarios from a free-text story/description grounded in the project's
/// business-context documents. Scenarios are created under a hidden per-project "General" bucket.
/// </summary>
public sealed record GenerateScenariosFromStoryCommand : IRequest<IReadOnlyList<ScenarioDto>>
{
    public Guid ProjectId { get; init; }

    public required string Story { get; init; }
}
