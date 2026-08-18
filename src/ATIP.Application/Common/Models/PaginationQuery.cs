namespace ATIP.Application.Common.Models;

/// <summary>
/// Standard parameters accepted by list endpoints. Values are clamped to safe bounds
/// in the handlers to protect the database from unbounded page sizes.
/// </summary>
public record PaginationQuery
{
    private const int MaxPageSize = 200;

    private int _page = 1;
    private int _pageSize = 25;

    /// <summary>1-based page number.</summary>
    public int Page
    {
        get => _page;
        init => _page = value < 1 ? 1 : value;
    }

    public int PageSize
    {
        get => _pageSize;
        init => _pageSize = value is < 1 or > MaxPageSize ? Math.Clamp(value, 1, MaxPageSize) : value;
    }

    /// <summary>Optional free-text search term applied to name-like columns.</summary>
    public string? Search { get; init; }

    /// <summary>Column to sort by; interpreted per-handler with a safe allow-list.</summary>
    public string? SortBy { get; init; }

    public bool SortDescending { get; init; }
}
