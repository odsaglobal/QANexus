using ATIP.Domain.Common;
using ATIP.Domain.Enums;

namespace ATIP.Domain.Entities;

/// <summary>
/// A reusable Playwright browser configuration (engine, viewport, locale, device emulation)
/// bound to an environment. The explorer and execution engines launch contexts from these profiles.
/// </summary>
public class BrowserProfile : AuditableEntity, ITenantScoped
{
    public Guid TenantId { get; set; }

    public Guid EnvironmentId { get; set; }

    public Environment Environment { get; set; } = null!;

    public required string Name { get; set; }

    public BrowserType Browser { get; set; } = BrowserType.Chromium;

    public bool Headless { get; set; } = true;

    public int ViewportWidth { get; set; } = 1280;

    public int ViewportHeight { get; set; } = 720;

    public string? Locale { get; set; }

    public string? TimezoneId { get; set; }

    /// <summary>Optional Playwright device descriptor name for mobile emulation (e.g. "iPhone 13").</summary>
    public string? DeviceName { get; set; }

    public string? UserAgent { get; set; }
}
