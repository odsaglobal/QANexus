namespace ATIP.Infrastructure.Configuration;

/// <summary>
/// Options for credential-secret envelope encryption, bound from section "Encryption".
/// <see cref="Keys"/> maps a key id to a base64-encoded 256-bit key, enabling rotation:
/// new secrets use <see cref="ActiveKeyId"/> while older ciphertexts remain decryptable.
/// </summary>
public sealed class EncryptionOptions
{
    public const string SectionName = "Encryption";

    public string ActiveKeyId { get; set; } = "default";

    public Dictionary<string, string> Keys { get; set; } = new();
}
