using ATIP.Application.Features.Scenarios.Dtos;

namespace ATIP.Application.Features.Scenarios.Commands.ImportScenariosFromFile;

/// <summary>
/// Outcome of a file import: the scenarios that were created and, when the caller asked for
/// suite placement, the suite they landed in so the UI can link straight to it.
/// </summary>
public sealed record ImportScenariosResult
{
    public required IReadOnlyList<ScenarioDto> Scenarios { get; init; }

    /// <summary>The suite the scenarios were added to, or null when imported without a suite.</summary>
    public Guid? SuiteId { get; init; }

    public string? SuiteName { get; init; }

    /// <summary>True when the suite was created by this import rather than already existing.</summary>
    public bool SuiteCreated { get; init; }
}
