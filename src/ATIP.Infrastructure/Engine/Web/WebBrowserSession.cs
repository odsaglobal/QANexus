using ATIP.Infrastructure.Exploration;

namespace ATIP.Infrastructure.Engine.Web;

/// <summary>
/// Lets a caller that already has a live browser hand it to the engine instead of having a second
/// one launched.
/// </summary>
/// <remarks>
/// The explorer opens a browser, logs in, and walks the application; when it then asks the engine
/// to run a step, that step must land in the <em>same</em> page — a fresh browser would have no
/// session, no cart, and no scenario state. Registered scoped, so the handoff lasts exactly as long
/// as the run does.
/// </remarks>
public sealed class WebBrowserSession
{
    /// <summary>The browser to reuse, or null to let the engine own its own.</summary>
    public PlaywrightBrowserService? Browser { get; set; }
}
