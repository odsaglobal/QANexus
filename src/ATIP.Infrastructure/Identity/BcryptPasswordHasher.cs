using ATIP.Application.Common.Interfaces;
using BCryptNet = BCrypt.Net.BCrypt;

namespace ATIP.Infrastructure.Identity;

/// <summary>BCrypt-based password hasher. Work factor 12 balances security and login latency.</summary>
public sealed class BcryptPasswordHasher : IPasswordHasher
{
    private const int WorkFactor = 12;

    public string Hash(string password) => BCryptNet.HashPassword(password, WorkFactor);

    public bool Verify(string password, string hash)
    {
        try
        {
            return BCryptNet.Verify(password, hash);
        }
        catch (BCrypt.Net.SaltParseException)
        {
            // Malformed or legacy hash: treat as a failed verification rather than throwing.
            return false;
        }
    }
}
