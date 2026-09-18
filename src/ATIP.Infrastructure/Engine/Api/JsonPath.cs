using System.Text.Json;

namespace ATIP.Infrastructure.Engine.Api;

/// <summary>
/// Reads values out of a JSON document with a dotted path such as <c>data.items[0].id</c>, plus
/// <c>[*]</c> to fan out across an array.
/// </summary>
/// <remarks>
/// Deliberately a small subset rather than a JSONPath library: assertions need "reach into the
/// response and pull out this field", and the wildcard form exists so a check like "every order is
/// SHIPPED" can be expressed without a loop. Anything more expressive would be a query language
/// that test authors then have to learn and debug.
/// </remarks>
internal static class JsonPath
{
    /// <summary>
    /// Returns the values at <paramref name="path"/>, or null when the document cannot be parsed
    /// or the path does not exist. Null is distinct from an empty list: "unreadable" is a defect in
    /// the test, while "no matches" is a legitimate result an assertion may be checking for.
    /// </summary>
    public static IReadOnlyList<string>? Read(string? json, string? path)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return null;
        }

        JsonDocument document;
        try
        {
            document = JsonDocument.Parse(json);
        }
        catch (JsonException)
        {
            return null;
        }

        using (document)
        {
            // An empty path means the whole document — useful for "the response body contains X".
            if (string.IsNullOrWhiteSpace(path) || path.Trim() is "$" or ".")
            {
                return [Stringify(document.RootElement)];
            }

            var current = new List<JsonElement> { document.RootElement };

            foreach (var segment in Tokenize(path!))
            {
                var next = new List<JsonElement>();

                foreach (var element in current)
                {
                    Descend(element, segment, next);
                }

                if (next.Count == 0)
                {
                    return null;
                }

                current = next;
            }

            return current.Select(Stringify).ToList();
        }
    }

    private static void Descend(JsonElement element, string segment, List<JsonElement> into)
    {
        if (segment == "*")
        {
            switch (element.ValueKind)
            {
                case JsonValueKind.Array:
                    into.AddRange(element.EnumerateArray());
                    break;
                case JsonValueKind.Object:
                    into.AddRange(element.EnumerateObject().Select(p => p.Value));
                    break;
            }

            return;
        }

        if (int.TryParse(segment, out var index))
        {
            if (element.ValueKind == JsonValueKind.Array && index >= 0 && index < element.GetArrayLength())
            {
                into.Add(element[index]);
            }

            return;
        }

        if (element.ValueKind == JsonValueKind.Object && element.TryGetProperty(segment, out var property))
        {
            into.Add(property);
        }
    }

    /// <summary>Splits <c>a.b[0].c</c> and <c>a.b[*].c</c> into the segments a, b, 0/*, c.</summary>
    private static IEnumerable<string> Tokenize(string path)
    {
        foreach (var part in path.Split('.', StringSplitOptions.RemoveEmptyEntries))
        {
            var name = part;

            var bracket = name.IndexOf('[');
            if (bracket < 0)
            {
                yield return name.Trim();
                continue;
            }

            if (bracket > 0)
            {
                yield return name[..bracket].Trim();
            }

            var rest = name[bracket..];
            while (rest.StartsWith('[') && rest.Contains(']'))
            {
                var close = rest.IndexOf(']');
                yield return rest[1..close].Trim().Trim('\'', '"');
                rest = rest[(close + 1)..];
            }
        }
    }

    private static string Stringify(JsonElement element) =>
        element.ValueKind switch
        {
            JsonValueKind.String => element.GetString() ?? string.Empty,
            JsonValueKind.Null or JsonValueKind.Undefined => string.Empty,
            _ => element.ToString()
        };
}
