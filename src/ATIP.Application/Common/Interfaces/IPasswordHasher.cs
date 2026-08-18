namespace ATIP.Application.Common.Interfaces;

/// <summary>Hashes and verifies user passwords for local (non-federated) accounts.</summary>
public interface IPasswordHasher
{
    string Hash(string password);

    bool Verify(string password, string hash);
}
