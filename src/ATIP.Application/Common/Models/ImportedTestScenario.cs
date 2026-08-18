namespace ATIP.Application.Common.Models;

public sealed record ImportedTestScenario(
    string Title,
    string? Preconditions,
    string? ExpectedResult,
    IReadOnlyList<ImportedTestStep> Steps,
    string? ExternalReference = null);

public sealed record ImportedTestStep(
    int Order,
    string Action,
    string? ExpectedResult);
