using ATIP.Application.Common.Security;
using ATIP.Application.Features.Scenarios.Dtos;
using ATIP.Domain.Enums;
using MediatR;

namespace ATIP.Application.Features.Scenarios.Commands.ImportScenariosFromFile;

/// <summary>How the imported scenarios should be attached to a test suite.</summary>
public enum ImportSuiteMode
{
    /// <summary>Import the scenarios only; they join no suite.</summary>
    None = 0,

    /// <summary>Append the scenarios to the end of an existing suite.</summary>
    Existing = 1,

    /// <summary>Create a new suite containing exactly the imported scenarios.</summary>
    New = 2,
}

public sealed record ImportScenariosFromFileCommand : IRequest<ImportScenariosResult>, IProjectScopedRequest
{
    public Guid ProjectId { get; init; }
    public Guid FeatureId { get; init; }
    public required string FileName { get; init; }
    public required byte[] Content { get; init; }

    /// <summary>Suite handling. Defaults to <see cref="ImportSuiteMode.None"/>.</summary>
    public ImportSuiteMode SuiteMode { get; init; } = ImportSuiteMode.None;

    /// <summary>Target suite when <see cref="SuiteMode"/> is <see cref="ImportSuiteMode.Existing"/>.</summary>
    public Guid? SuiteId { get; init; }

    /// <summary>Name of the suite to create when <see cref="SuiteMode"/> is <see cref="ImportSuiteMode.New"/>.</summary>
    public string? NewSuiteName { get; init; }

    public ProjectRole RequiredRole => ProjectRole.Editor;
}
