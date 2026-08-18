using ATIP.Domain.Common;

namespace ATIP.Domain.Entities;

/// <summary>
/// An encrypted secret (e.g. login username/password) the explorer and execution engines
/// use to authenticate against the application under test. The secret value is stored
/// encrypted at rest; only a reference to it is ever surfaced through the API.
/// </summary>
public class Credential : AuditableEntity, ITenantScoped
{
    public Guid TenantId { get; set; }

    public Guid ProjectId { get; set; }

    public Project Project { get; set; } = null!;

    /// <summary>Friendly label, e.g. "Standard user" or "Admin account".</summary>
    public required string Name { get; set; }

    /// <summary>Non-secret identifier, typically the username or client id.</summary>
    public required string Username { get; set; }

    /// <summary>Ciphertext of the secret (password/token). Never returned by the API.</summary>
    public required byte[] EncryptedSecret { get; set; }

    /// <summary>Per-secret nonce/IV used by the envelope encryption scheme.</summary>
    public required byte[] EncryptionNonce { get; set; }

    /// <summary>Identifier of the data-encryption key used, enabling key rotation.</summary>
    public required string KeyId { get; set; }
}
