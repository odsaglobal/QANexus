namespace ATIP.Application.Common.Interfaces;

/// <summary>
/// Envelope-encryption abstraction for credential secrets. Implementations encrypt with a
/// data-encryption key identified by <see cref="ProtectedSecret.KeyId"/> to support key rotation.
/// </summary>
public interface ISecretProtector
{
    ProtectedSecret Protect(string plaintext);

    string Unprotect(ProtectedSecret secret);
}

/// <summary>Ciphertext plus the nonce and key id required to decrypt it later.</summary>
public sealed record ProtectedSecret(byte[] Ciphertext, byte[] Nonce, string KeyId);
