using System.Security.Cryptography;

namespace ATIP.Application.Common.Security;

/// <summary>
/// Generates and hashes API keys. The plaintext key is shown to the user exactly once at creation;
/// only a SHA-256 hash is persisted, so a database leak never exposes usable keys.
/// </summary>
public static class ApiKeyHashing
{
    public const string Prefix = "qk_";

    /// <summary>Creates a new key. Returns the one-time plaintext, its hash, and a display prefix.</summary>
    public static (string PlainText, string Hash, string DisplayPrefix) Generate()
    {
        var bytes = RandomNumberGenerator.GetBytes(30);
        var secret = Convert.ToBase64String(bytes)
            .Replace("+", "").Replace("/", "").Replace("=", "");
        var plainText = Prefix + secret;
        return (plainText, Hash(plainText), plainText[..Math.Min(10, plainText.Length)]);
    }

    /// <summary>Computes the stable SHA-256 hex hash used to look a key up on authentication.</summary>
    public static string Hash(string plainText)
    {
        var hash = SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(plainText));
        return Convert.ToHexString(hash).ToLowerInvariant();
    }
}
