namespace ATIP.Domain.Enums;

/// <summary>Where a stored locator came from. Origin drives the initial ranking: a locator a
/// human wrote outranks one the recorder derived, which outranks one the AI invented while
/// healing.</summary>
public enum LocatorOrigin
{
    /// <summary>Authored by a user in the object repository.</summary>
    Manual = 0,

    /// <summary>Derived from the page while recording a successful step.</summary>
    Recorded = 1,

    /// <summary>Produced by the AI when the previous locators stopped resolving.</summary>
    Healed = 2,

    /// <summary>Captured by the crawler during application exploration.</summary>
    Discovered = 3
}
