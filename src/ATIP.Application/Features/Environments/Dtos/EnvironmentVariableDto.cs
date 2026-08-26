namespace ATIP.Application.Features.Environments.Dtos;

/// <summary>A single Postman-style environment variable (key/value pair) with a field type.</summary>
public sealed record EnvironmentVariableDto
{
    public required string Key { get; init; }

    public string? Value { get; init; }

    /// <summary>Field type controlling how the value is displayed in the UI: <c>text</c> or <c>secret</c>.</summary>
    public string Type { get; init; } = "text";
}
