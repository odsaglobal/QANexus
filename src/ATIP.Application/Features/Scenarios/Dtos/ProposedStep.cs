namespace ATIP.Application.Features.Scenarios.Dtos;

/// <summary>
/// A single step proposed by a goal-driven exploration (or a snapshot of prior steps kept for revert).
/// Stored on the scenario as JSON so the user can review, apply, or revert without losing the originals.
/// </summary>
public sealed record ProposedStep
{
    public int Order { get; init; }
    public required string Action { get; init; }
    public string? ExpectedResult { get; init; }
}
