using System.Text.RegularExpressions;

namespace ATIP.Application.Engine.Model;

/// <summary>
/// Everything a driver needs to know about the run it is part of: which tenant/project/environment
/// it belongs to, and the variable bag that carries values from one step to the next.
/// </summary>
/// <remarks>
/// The variable bag is the seam between platforms. A web step captures the order number off the
/// confirmation page into <c>orderId</c>; an API step calls <c>/orders/{{orderId}}</c>; a database
/// step asserts the row exists. None of them know about each other — they only share this context.
/// </remarks>
public sealed partial class RunContext
{
    private readonly Dictionary<string, string?> _variables = new(StringComparer.OrdinalIgnoreCase);

    public required Guid TenantId { get; init; }

    public required Guid ProjectId { get; init; }

    public Guid? EnvironmentId { get; init; }

    public Guid? SessionId { get; init; }

    public Guid? ScenarioId { get; init; }

    /// <summary>Base URL of the environment under test, used to resolve relative navigations.</summary>
    public string? BaseUrl { get; init; }

    /// <summary>Default per-action timeout; individual actions may override it.</summary>
    public int DefaultTimeoutMs { get; init; } = 15000;

    /// <summary>
    /// When false, drivers must not call the AI. Set for deterministic replay so a "passing" run
    /// cannot be one the model quietly rescued.
    /// </summary>
    public bool AllowHealing { get; init; } = true;

    public IReadOnlyDictionary<string, string?> Variables => _variables;

    public void SetVariable(string name, string? value)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            return;
        }

        _variables[name.Trim()] = value;
    }

    public void SetVariables(IEnumerable<KeyValuePair<string, string?>> values)
    {
        foreach (var (key, value) in values)
        {
            SetVariable(key, value);
        }
    }

    public string? GetVariable(string name) =>
        _variables.TryGetValue(name.Trim(), out var value) ? value : null;

    /// <summary>
    /// Replaces <c>{{name}}</c> tokens with run variables. Unknown tokens are left untouched on
    /// purpose: emitting an empty string would turn a wiring mistake into a confusing product
    /// failure ("expected '', got 'ORD-1001'") instead of an obvious unresolved placeholder.
    /// </summary>
    public string? Resolve(string? template)
    {
        if (string.IsNullOrEmpty(template) || !template.Contains("{{", StringComparison.Ordinal))
        {
            return template;
        }

        return VariableToken().Replace(template, match =>
        {
            var key = match.Groups[1].Value.Trim();
            return _variables.TryGetValue(key, out var value) ? value ?? string.Empty : match.Value;
        });
    }

    [GeneratedRegex(@"\{\{\s*([^}]+?)\s*\}\}", RegexOptions.Compiled)]
    private static partial Regex VariableToken();
}
