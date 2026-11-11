using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace ATIP.Application.Common.Security;

/// <summary>
/// Masks known secret values in any text that leaves the run: AI prompts, live logs, persisted
/// step results. Populated per run with the plaintext of every secret the run can resolve.
/// </summary>
public sealed partial class SecretRedactor
{
    // Shorter values would mask ordinary words and numbers all over the page.
    private const int MinLength = 4;

    private readonly List<(string Value, string Mask)> _secrets = [];

    public bool IsEmpty => _secrets.Count == 0;

    public void Add(string key, string? value)
    {
        if (string.IsNullOrEmpty(value) || value.Length < MinLength)
        {
            return;
        }

        var mask = $"[secret:{key}]";
        AddVariant(value, mask);

        // Prompts embed page data as JSON, where a quote or backslash in the secret is escaped
        // either as \" (relaxed) or \u0022 (System.Text.Json's default).
        foreach (var escaped in new[]
                 {
                     JsonEncodedText.Encode(value).ToString(),
                     JsonEncodedText.Encode(value, JavaScriptEncoder.UnsafeRelaxedJsonEscaping).ToString(),
                 })
        {
            if (!string.Equals(escaped, value, StringComparison.Ordinal))
            {
                AddVariant(escaped, mask);
            }
        }

        // Longest first, so a secret that contains another is masked whole.
        _secrets.Sort((a, b) => b.Value.Length.CompareTo(a.Value.Length));
    }

    public string Redact(string text)
    {
        if (string.IsNullOrEmpty(text) || _secrets.Count == 0)
        {
            return text;
        }

        foreach (var (value, mask) in _secrets)
        {
            if (text.Contains(value, StringComparison.Ordinal))
            {
                text = text.Replace(value, mask, StringComparison.Ordinal);
            }
        }

        return text;
    }

    public string? RedactOrNull(string? text) => text is null ? null : Redact(text);

    /// <summary>
    /// Whether a variable name denotes a credential even when nobody marked it secret, e.g. a test
    /// data column called <c>password</c>. Over-matching is harmless: a masked value is still typed.
    /// </summary>
    public static bool LooksSecret(string key)
    {
        var lastSegment = key.Contains('.') ? key[(key.LastIndexOf('.') + 1)..] : key;
        return SecretNamePattern().IsMatch(lastSegment);
    }

    private void AddVariant(string value, string mask)
    {
        if (!_secrets.Exists(s => string.Equals(s.Value, value, StringComparison.Ordinal)))
        {
            _secrets.Add((value, mask));
        }
    }

    [GeneratedRegex(@"pass(word|wd|code|phrase)?$|^pass|pwd|secret|token|api[_-]?key|private[_-]?key|otp|totp|mfa", RegexOptions.IgnoreCase)]
    private static partial Regex SecretNamePattern();
}
