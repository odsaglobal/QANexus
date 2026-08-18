namespace ATIP.Infrastructure.Configuration;

/// <summary>File-storage configuration, bound from section "Storage".</summary>
public sealed class StorageOptions
{
    public const string SectionName = "Storage";

    /// <summary>Root directory for the local file store (development). Relative paths are allowed.</summary>
    public string RootPath { get; set; } = "storage";
}
