using ATIP.Domain.Entities;

namespace ATIP.Application.Common.Interfaces;

/// <summary>Issues signed JWT access tokens for authenticated users.</summary>
public interface IJwtTokenService
{
    /// <summary>Creates a signed access token embedding the user's identity, tenant and role claims.</summary>
    AccessToken CreateAccessToken(User user);
}

/// <summary>A freshly minted access token and its expiry, returned to clients on login.</summary>
public sealed record AccessToken(string Token, DateTimeOffset ExpiresAtUtc);
