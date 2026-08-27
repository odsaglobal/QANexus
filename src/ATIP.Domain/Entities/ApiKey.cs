using ATIP.Domain.Common;

namespace ATIP.Domain.Entities;

/// <summary>
/// A tenant-scoped API key that lets external systems (CI pipelines, scripts) authenticate to the
/// API via the <c>X-Api-Key</c> header. Only the SHA-256 hash of the key is stored.
/// </summary>
public class ApiKey : AuditableEntity, ITenantScoped
{
    public Guid TenantId { get; set; }

    /// <summary>Human-friendly label, e.g. "GitHub Actions".</summary>
    public required string Name { get; set; }

    /// <summary>SHA-256 hex hash of the plaintext key (the plaintext is never stored).</summary>
    public required string KeyHash { get; set; }

    /// <summary>First few characters of the key, shown so users can identify it.</summary>
    public required string Prefix { get; set; }

    /// <summary>The user who created the key; used as the acting identity for API-key requests.</summary>
    public Guid CreatedByUserId { get; set; }

    public DateTimeOffset? LastUsedAtUtc { get; set; }

    public DateTimeOffset? ExpiresAtUtc { get; set; }

    public DateTimeOffset? RevokedAtUtc { get; set; }

    public bool IsActive => RevokedAtUtc is null && (ExpiresAtUtc is null || ExpiresAtUtc > DateTimeOffset.UtcNow);
}
