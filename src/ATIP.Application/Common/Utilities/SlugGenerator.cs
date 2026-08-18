using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace ATIP.Application.Common.Utilities;

/// <summary>Generates URL-safe, lowercase slugs/keys from arbitrary display names.</summary>
public static partial class SlugGenerator
{
    [GeneratedRegex("[^a-z0-9]+")]
    private static partial Regex NonAlphanumeric();

    [GeneratedRegex("-{2,}")]
    private static partial Regex RepeatedDashes();

    public static string Create(string input, int maxLength = 60)
    {
        if (string.IsNullOrWhiteSpace(input))
        {
            return string.Empty;
        }

        var normalized = input.Normalize(NormalizationForm.FormD);
        var stripped = new StringBuilder(normalized.Length);
        foreach (var ch in normalized)
        {
            if (CharUnicodeInfo.GetUnicodeCategory(ch) != UnicodeCategory.NonSpacingMark)
            {
                stripped.Append(ch);
            }
        }

        var slug = stripped.ToString().ToLowerInvariant();
        slug = NonAlphanumeric().Replace(slug, "-");
        slug = RepeatedDashes().Replace(slug, "-").Trim('-');

        return slug.Length > maxLength ? slug[..maxLength].Trim('-') : slug;
    }
}
