using System.Text.Json;
using System.Text.Json.Serialization;
using ATIP.Domain.Entities;
using ATIP.Domain.Enums;

namespace ATIP.Application.Common.Utilities;

/// <summary>
/// Narrows a suite run to a subset of its scenarios. Three independent facets, because the labels a
/// user sees on a test case come from three different places: <see cref="ScenarioType"/> (Positive,
/// Negative, Boundary…), <see cref="Priority"/> (Critical, High…) and the free-form
/// <c>TagsJson</c> list (smoke, login, import:manual…).
///
/// Matching is OR within a facet and AND across facets — the standard faceted behaviour. Picking
/// "Negative + Boundary" and "Critical" runs the critical negative and boundary cases; OR-ing
/// everything together would widen the selection each time a facet is touched, which is the opposite
/// of what picking a filter is supposed to do.
///
/// <see cref="ScenarioIds"/> is a further explicit allow-list on top of the facets, for when the user
/// ticks individual test cases off the run. It is AND-ed with them: a scenario runs only if it both
/// matches the facets and was left checked.
///
/// Serialized onto the session because the run happens later, on a background worker, from the
/// session alone.
/// </summary>
public sealed record ScenarioRunFilter
{
    public IReadOnlyList<string> Tags { get; init; } = [];

    public IReadOnlyList<string> Types { get; init; } = [];

    public IReadOnlyList<string> Priorities { get; init; } = [];

    /// <summary>Explicitly checked scenarios. Empty means "whatever the facets match", not "nothing".</summary>
    public IReadOnlyList<Guid> ScenarioIds { get; init; } = [];

    [JsonIgnore]
    public bool IsEmpty =>
        Tags.Count == 0 && Types.Count == 0 && Priorities.Count == 0 && ScenarioIds.Count == 0;

    /// <summary>Trims, de-duplicates and drops blanks so a sloppy filter can't silently match nothing.</summary>
    public static ScenarioRunFilter Create(
        IEnumerable<string>? tags,
        IEnumerable<string>? types,
        IEnumerable<string>? priorities,
        IEnumerable<Guid>? scenarioIds = null) =>
        new()
        {
            Tags = Clean(tags),
            Types = Clean(types),
            Priorities = Clean(priorities),
            ScenarioIds = scenarioIds?.Where(id => id != Guid.Empty).Distinct().ToList() ?? [],
        };

    public static ScenarioRunFilter Deserialize(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return new ScenarioRunFilter();
        }

        try
        {
            return JsonSerializer.Deserialize<ScenarioRunFilter>(json) ?? new ScenarioRunFilter();
        }
        catch (JsonException)
        {
            return new ScenarioRunFilter();
        }
    }

    /// <summary>Null for an empty filter, so "no filter" is stored as an absent value rather than noise.</summary>
    public string? Serialize() => IsEmpty ? null : JsonSerializer.Serialize(this);

    /// <summary>True when the scenario satisfies every facet that was actually specified.</summary>
    public bool Matches(Scenario scenario) =>
        Matches(scenario.Id, scenario.TagsJson, scenario.Type, scenario.Priority);

    /// <summary>
    /// Overload for the projection used when validating a run, which reads the four columns without
    /// materializing whole scenarios.
    /// </summary>
    public bool Matches(Guid scenarioId, string? tagsJson, ScenarioType type, Priority priority) =>
        (ScenarioIds.Count == 0 || ScenarioIds.Contains(scenarioId))
        && MatchesTags(tagsJson)
        && (Types.Count == 0 || Types.Contains(type.ToString(), StringComparer.OrdinalIgnoreCase))
        && (Priorities.Count == 0 || Priorities.Contains(priority.ToString(), StringComparer.OrdinalIgnoreCase));

    /// <summary>Reads a scenario's <c>TagsJson</c> column, tolerating null, blank and malformed values.</summary>
    public static IReadOnlyList<string> ParseTags(string? tagsJson)
    {
        if (string.IsNullOrWhiteSpace(tagsJson))
        {
            return [];
        }

        try
        {
            return JsonSerializer.Deserialize<List<string>>(tagsJson) ?? [];
        }
        catch (JsonException)
        {
            return [];
        }
    }

    /// <summary>Human-readable summary for audit entries and run logs.</summary>
    public string Describe()
    {
        var parts = new List<string>();
        if (Types.Count > 0) parts.Add($"type {string.Join("/", Types)}");
        if (Priorities.Count > 0) parts.Add($"priority {string.Join("/", Priorities)}");
        if (Tags.Count > 0) parts.Add($"tagged {string.Join("/", Tags)}");
        if (ScenarioIds.Count > 0) parts.Add($"{ScenarioIds.Count} hand-picked test(s)");
        return parts.Count > 0 ? string.Join(", ", parts) : "no filter";
    }

    private bool MatchesTags(string? tagsJson) =>
        Tags.Count == 0 || ParseTags(tagsJson).Any(t => Tags.Contains(t, StringComparer.OrdinalIgnoreCase));

    private static IReadOnlyList<string> Clean(IEnumerable<string>? values) =>
        values?
            .Where(v => !string.IsNullOrWhiteSpace(v))
            .Select(v => v.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList()
        ?? [];
}
