using ATIP.Domain.Common;

namespace ATIP.Domain.Entities;

/// <summary>
/// A named collection of test data rows (e.g. sample users, product SKUs) that scenarios
/// can be parameterized with. Stored as JSON to accommodate arbitrary tabular shapes.
/// </summary>
public class TestDataSet : AuditableEntity, ITenantScoped
{
    public Guid TenantId { get; set; }

    public Guid ProjectId { get; set; }

    public Project Project { get; set; } = null!;

    public required string Name { get; set; }

    public string? Description { get; set; }

    /// <summary>Ordered list of column/field names, serialized as a JSON array.</summary>
    public required string ColumnsJson { get; set; }

    /// <summary>Array of row objects, serialized as JSON.</summary>
    public required string RowsJson { get; set; }
}
