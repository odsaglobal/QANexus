using ATIP.Application.Features.TestData.Dtos;
using MediatR;

namespace ATIP.Application.Features.TestData.Commands.UpdateTestDataSet;

/// <summary>Updates an existing test data set's name, description, columns and rows.</summary>
public sealed record UpdateTestDataSetCommand : IRequest<TestDataSetDto>
{
    public Guid Id { get; init; }

    public Guid ProjectId { get; init; }

    public Guid EnvironmentId { get; init; }

    public required string Name { get; init; }

    public string? Description { get; init; }

    public List<string> Columns { get; init; } = new();

    public List<Dictionary<string, string?>> Rows { get; init; } = new();
}
