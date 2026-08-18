using ATIP.Domain.Common;
using ATIP.Domain.Enums;

namespace ATIP.Domain.Entities;

/// <summary>
/// An uploaded product document (SRS, BRD, Swagger, etc.). The raw extracted text is stored
/// so AI analysis is reproducible, and the derived <see cref="RequirementModule"/> graph hangs off it.
/// </summary>
public class Requirement : AuditableEntity, ITenantScoped, ISoftDeletable
{
    public Guid TenantId { get; set; }

    public Guid ProjectId { get; set; }

    public Project Project { get; set; } = null!;

    public required string Name { get; set; }

    public RequirementSourceType SourceType { get; set; }

    public RequirementStatus Status { get; set; } = RequirementStatus.Uploaded;

    /// <summary>Relative path in the file store where the original upload is retained.</summary>
    public string? StoragePath { get; set; }

    /// <summary>Plain text extracted from the document, used as the AI analysis input.</summary>
    public string? ExtractedText { get; set; }

    /// <summary>Populated when <see cref="Status"/> is <see cref="RequirementStatus.Failed"/>.</summary>
    public string? ErrorMessage { get; set; }

    public DateTimeOffset? AnalyzedAtUtc { get; set; }

    public bool IsDeleted { get; set; }

    public DateTimeOffset? DeletedAtUtc { get; set; }

    public ICollection<RequirementModule> Modules { get; set; } = new List<RequirementModule>();
}
