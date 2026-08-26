using System.Text.Json;

namespace ATIP.Application.Features.Environments.Dtos;

/// <summary>
/// Reads/writes an environment's key/value variables stored as JSON. The stored shape is
/// <c>{ "key": { "value": "...", "type": "text|secret" } }</c>. A legacy shape where each value is
/// a plain string (<c>{ "key": "value" }</c>) is still parsed (treated as type <c>text</c>).
/// </summary>
public static class EnvironmentVariableSerialization
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public static List<EnvironmentVariableDto> Parse(string? variablesJson)
    {
        if (string.IsNullOrWhiteSpace(variablesJson))
        {
            return [];
        }

        try
        {
            using var doc = JsonDocument.Parse(variablesJson);
            if (doc.RootElement.ValueKind != JsonValueKind.Object)
            {
                return [];
            }

            var list = new List<EnvironmentVariableDto>();
            foreach (var prop in doc.RootElement.EnumerateObject())
            {
                string? value;
                var type = "text";

                if (prop.Value.ValueKind == JsonValueKind.String)
                {
                    value = prop.Value.GetString();
                }
                else if (prop.Value.ValueKind == JsonValueKind.Object)
                {
                    value = prop.Value.TryGetProperty("value", out var v) && v.ValueKind == JsonValueKind.String
                        ? v.GetString()
                        : null;
                    type = prop.Value.TryGetProperty("type", out var t) && t.ValueKind == JsonValueKind.String
                        ? NormalizeType(t.GetString())
                        : "text";
                }
                else
                {
                    value = null;
                }

                list.Add(new EnvironmentVariableDto { Key = prop.Name, Value = value, Type = type });
            }

            return list;
        }
        catch (JsonException)
        {
            return [];
        }
    }

    public static string? Serialize(IEnumerable<EnvironmentVariableDto> variables)
    {
        var dict = new Dictionary<string, object>(StringComparer.Ordinal);
        foreach (var variable in variables)
        {
            if (string.IsNullOrEmpty(variable.Key))
            {
                continue;
            }
            dict[variable.Key] = new { value = variable.Value, type = NormalizeType(variable.Type) };
        }

        return dict.Count == 0 ? null : JsonSerializer.Serialize(dict, JsonOptions);
    }

    /// <summary>Flattens variables into a key → value map for {{token}} binding.</summary>
    public static Dictionary<string, string?> ParseValues(string? variablesJson)
    {
        var result = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);
        foreach (var variable in Parse(variablesJson))
        {
            result[variable.Key] = variable.Value;
        }
        return result;
    }

    public static string NormalizeType(string? type) =>
        string.Equals(type, "secret", StringComparison.OrdinalIgnoreCase) ? "secret" : "text";
}
