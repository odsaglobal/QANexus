namespace ATIP.Infrastructure.Exploration;

/// <summary>Element metadata extracted from the page by the JS discovery script.</summary>
public sealed class ElementDiscoveryInfo
{
    public string? TagName { get; set; }
    public string? Role { get; set; }
    public string? AriaLabel { get; set; }
    public string? Name { get; set; }
    public string? Placeholder { get; set; }
    public string? TextContent { get; set; }
    public string? DataTestId { get; set; }
    public string? Id { get; set; }
    public string? Href { get; set; }
    public string? InputType { get; set; }
    public string? CssSelector { get; set; }
    public string? XPath { get; set; }
    public ElementRect? Rect { get; set; }
    public bool IsInteractive { get; set; } = true;
}

public sealed class ElementRect
{
    public double X { get; set; }
    public double Y { get; set; }
    public double W { get; set; }
    public double H { get; set; }
}
