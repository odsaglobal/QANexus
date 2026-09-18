using ATIP.Domain.Common;
using ATIP.Domain.Enums;

namespace ATIP.Domain.Entities;

/// <summary>
/// A database the engine may query during a run, for seeding fixtures, tearing them down, or
/// validating that a UI/API action actually persisted.
/// </summary>
/// <remarks>
/// Scoped to an <see cref="Environment"/> rather than a project: staging and production point at
/// different databases, and a step that says "the order row exists" must follow the environment
/// the run was launched against without being rewritten.
/// </remarks>
public class DataConnection : AuditableEntity, ITenantScoped, ISoftDeletable
{
    public Guid TenantId { get; set; }

    public Guid ProjectId { get; set; }

    public Guid EnvironmentId { get; set; }

    /// <summary>Name steps reference, e.g. <c>orders</c>. Unique per environment.</summary>
    public required string Name { get; set; }

    public DataProviderKind Provider { get; set; } = DataProviderKind.PostgreSql;

    /// <summary>
    /// Ciphertext of the connection string. Uses the same envelope scheme as
    /// <see cref="Credential"/> so there is one key-rotation story rather than two, and is never
    /// returned by the API or written into run logs — a connection string usually carries a password.
    /// </summary>
    public required byte[] EncryptedConnectionString { get; set; }

    /// <summary>Per-secret nonce/IV used by the envelope encryption scheme.</summary>
    public required byte[] EncryptionNonce { get; set; }

    /// <summary>Identifier of the data-encryption key used, enabling key rotation.</summary>
    public required string KeyId { get; set; }

    /// <summary>
    /// Hard ceiling on how long a single query may run, so a bad predicate stalls one step rather
    /// than holding a run slot until the session watchdog fires.
    /// </summary>
    public int CommandTimeoutSeconds { get; set; } = 15;

    /// <summary>
    /// When true the driver refuses anything that is not a read. Default for a validation-only
    /// connection; turn it off deliberately for setup/teardown connections.
    /// </summary>
    public bool ReadOnly { get; set; } = true;

    public bool IsDeleted { get; set; }

    public DateTimeOffset? DeletedAtUtc { get; set; }
}
