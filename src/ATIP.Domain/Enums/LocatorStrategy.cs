namespace ATIP.Domain.Enums;

/// <summary>Locator strategy stored for a discovered UI element.</summary>
public enum LocatorStrategy
{
    CSS = 0,
    XPath = 1,
    ARIA = 2,
    Role = 3,
    Text = 4,
    Placeholder = 5,
    DataAttribute = 6,
    NearbyLabel = 7,
    Visual = 8
}
