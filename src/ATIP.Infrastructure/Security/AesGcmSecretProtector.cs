using System.Security.Cryptography;
using ATIP.Application.Common.Interfaces;
using ATIP.Infrastructure.Configuration;
using Microsoft.Extensions.Options;

namespace ATIP.Infrastructure.Security;

/// <summary>
/// AES-256-GCM authenticated encryption for credential secrets. Each secret gets a fresh
/// 96-bit nonce; the 128-bit authentication tag is appended to the ciphertext so tampering
/// is detected on decryption. Keys are resolved by id from configuration to support rotation.
/// </summary>
public sealed class AesGcmSecretProtector : ISecretProtector
{
    private const int NonceSize = 12; // 96 bits, recommended for GCM
    private const int TagSize = 16;   // 128 bits

    private readonly EncryptionOptions _options;

    public AesGcmSecretProtector(IOptions<EncryptionOptions> options)
    {
        _options = options.Value;
        if (!_options.Keys.ContainsKey(_options.ActiveKeyId))
        {
            throw new InvalidOperationException(
                $"Encryption active key '{_options.ActiveKeyId}' is not present in configuration.");
        }
    }

    public ProtectedSecret Protect(string plaintext)
    {
        var key = ResolveKey(_options.ActiveKeyId);
        var plainBytes = System.Text.Encoding.UTF8.GetBytes(plaintext);

        var nonce = RandomNumberGenerator.GetBytes(NonceSize);
        var cipher = new byte[plainBytes.Length];
        var tag = new byte[TagSize];

        using var aes = new AesGcm(key, TagSize);
        aes.Encrypt(nonce, plainBytes, cipher, tag);

        // Store ciphertext || tag so a single blob round-trips through the database.
        var payload = new byte[cipher.Length + tag.Length];
        Buffer.BlockCopy(cipher, 0, payload, 0, cipher.Length);
        Buffer.BlockCopy(tag, 0, payload, cipher.Length, tag.Length);

        return new ProtectedSecret(payload, nonce, _options.ActiveKeyId);
    }

    public string Unprotect(ProtectedSecret secret)
    {
        var key = ResolveKey(secret.KeyId);

        var cipherLength = secret.Ciphertext.Length - TagSize;
        if (cipherLength < 0)
        {
            throw new CryptographicException("Ciphertext is too short to contain an authentication tag.");
        }

        var cipher = new byte[cipherLength];
        var tag = new byte[TagSize];
        Buffer.BlockCopy(secret.Ciphertext, 0, cipher, 0, cipherLength);
        Buffer.BlockCopy(secret.Ciphertext, cipherLength, tag, 0, TagSize);

        var plain = new byte[cipherLength];
        using var aes = new AesGcm(key, TagSize);
        aes.Decrypt(secret.Nonce, cipher, tag, plain);

        return System.Text.Encoding.UTF8.GetString(plain);
    }

    private byte[] ResolveKey(string keyId)
    {
        if (!_options.Keys.TryGetValue(keyId, out var base64Key))
        {
            throw new InvalidOperationException($"Encryption key '{keyId}' was not found in configuration.");
        }

        var key = Convert.FromBase64String(base64Key);
        if (key.Length != 32)
        {
            throw new InvalidOperationException($"Encryption key '{keyId}' must be a base64-encoded 256-bit key.");
        }

        return key;
    }
}
