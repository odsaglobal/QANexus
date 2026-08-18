namespace ATIP.Infrastructure.Configuration;

/// <summary>
/// Configuration for driving scenario execution through the official Playwright MCP server
/// (<c>@playwright/mcp</c>). Bound from the "PlaywrightMcp" section. When <see cref="Enabled"/> is
/// false the in-process Playwright engine is used instead.
/// </summary>
public sealed class PlaywrightMcpOptions
{
    public const string SectionName = "PlaywrightMcp";

    /// <summary>When true, scenario/suite runs drive the browser via the Playwright MCP server.</summary>
    public bool Enabled { get; set; }

    /// <summary>Executable that launches the MCP server (default: npx).</summary>
    public string Command { get; set; } = "npx";

    /// <summary>
    /// Arguments passed to <see cref="Command"/> to start the stdio MCP server. Left empty by
    /// default: the config binder appends to (does not replace) a pre-populated list, so defaults
    /// are supplied via configuration instead.
    /// </summary>
    public List<string> Arguments { get; set; } = new();
}
