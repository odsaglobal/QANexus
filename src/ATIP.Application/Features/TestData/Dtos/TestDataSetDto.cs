using System.Text.Json;
using ATIP.Domain.Entities;

namespace ATIP.Application.Features.TestData.Dtos;

/// <summary>Read model for an environment-scoped test data set.</summary>
public sealed record TestDataSetDto
{
    public required Guid Id { get; init; }

    public required Guid ProjectId { get; init; }

    public required Guid EnvironmentId { get; init; }

    public required string Name { get; init; }

    public string? Description { get; init; }

    public required IReadOnlyList<string> Columns { get; init; }

    public required IReadOnlyList<Dictionary<string, string?>> Rows { get; init; }

    public int RowCount { get; init; }

    public required DateTimeOffset CreatedAtUtc { get; init; }

    public DateTimeOffset? UpdatedAtUtc { get; init; }

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public static TestDataSetDto FromEntity(TestDataSet e)
    {
        var columns = string.IsNullOrWhiteSpace(e.ColumnsJson)
            ? new List<string>()
            : JsonSerializer.Deserialize<List<string>>(e.ColumnsJson, JsonOptions) ?? new List<string>();

        var rows = string.IsNullOrWhiteSpace(e.RowsJson)
            ? new List<Dictionary<string, string?>>()
            : JsonSerializer.Deserialize<List<Dictionary<string, string?>>>(e.RowsJson, JsonOptions)
              ?? new List<Dictionary<string, string?>>();

        return new TestDataSetDto
        {
            Id = e.Id,
            ProjectId = e.ProjectId,
            EnvironmentId = e.EnvironmentId,
            Name = e.Name,
            Description = e.Description,
            Columns = columns,
            Rows = rows,
            RowCount = rows.Count,
            CreatedAtUtc = e.CreatedAtUtc,
            UpdatedAtUtc = e.UpdatedAtUtc
        };
    }

    public static string SerializeColumns(IEnumerable<string> columns) =>
        JsonSerializer.Serialize(columns, JsonOptions);

    public static string SerializeRows(IEnumerable<Dictionary<string, string?>> rows) =>
        JsonSerializer.Serialize(rows, JsonOptions);
}
