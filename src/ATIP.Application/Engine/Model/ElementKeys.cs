namespace ATIP.Application.Engine.Model;

/// <summary>
/// Derives the object-repository key for an element from its human descriptor.
/// </summary>
/// <remarks>
/// Exploration and replay must agree on this exactly. Exploration writes a locator under the key while
/// replay looks one up under it, so two slightly different slug rules would silently sever the two: the
/// store would fill up with entries nothing ever reads, and every run would behave as though it had
/// never explored. Keeping the single rule here is what makes that class of bug impossible.
/// </remarks>
public static class ElementKeys
{
    /// <summary>Long enough to stay readable and distinct, short enough to sit in an index.</summary>
    private const int MaxLength = 120;

    /// <summary>
    /// A lowercase, hyphen-separated slug of <paramref name="descriptor"/>, or <c>null</c> when the
    /// descriptor carries no usable identity.
    /// </summary>
    public static string? From(string? descriptor)
    {
        if (string.IsNullOrWhiteSpace(descriptor))
        {
            return null;
        }

        var slug = new string(descriptor
            .Trim()
            .ToLowerInvariant()
            .Select(c => char.IsLetterOrDigit(c) ? c : '-')
            .ToArray());

        while (slug.Contains("--", StringComparison.Ordinal))
        {
            slug = slug.Replace("--", "-", StringComparison.Ordinal);
        }

        slug = slug.Trim('-');

        return slug.Length switch
        {
            0 => null,
            > MaxLength => slug[..MaxLength].TrimEnd('-'),
            _ => slug,
        };
    }
}
