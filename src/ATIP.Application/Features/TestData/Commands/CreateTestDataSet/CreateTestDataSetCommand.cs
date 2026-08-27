using ATIP.Application.Common.Security;
using ATIP.Application.Features.TestData.Dtos;
using ATIP.Domain.Enums;
using MediatR;

namespace ATIP.Application.Features.TestData.Commands.CreateTestDataSet;

/// <summary>Creates a new environment-scoped test data set.</summary>
public sealed record CreateTestDataSetCommand : IRequest<TestDataSetDto>, IProjectScopedRequest
{
    public Guid ProjectId { get; init; }

    public Guid EnvironmentId { get; init; }

    public required string Name { get; init; }

    public string? Description { get; init; }

    public List<string> Columns { get; init; } = new();

    public List<Dictionary<string, string?>> Rows { get; init; } = new();

    public ProjectRole RequiredRole => ProjectRole.Editor;
}
