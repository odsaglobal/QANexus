using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using ATIP.Application.Common.Interfaces;
using ATIP.Domain.Entities;
using ATIP.Infrastructure.Configuration;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace ATIP.Infrastructure.Identity;

/// <summary>Issues HMAC-SHA256 signed JWTs carrying the user's identity, tenant and role claims.</summary>
public sealed class JwtTokenService : IJwtTokenService
{
    /// <summary>Custom claim type carrying the tenant id used for multi-tenant isolation.</summary>
    public const string TenantIdClaim = "tenant_id";

    private readonly JwtOptions _options;
    private readonly IDateTimeProvider _clock;

    public JwtTokenService(IOptions<JwtOptions> options, IDateTimeProvider clock)
    {
        _options = options.Value;
        _clock = clock;
    }

    public AccessToken CreateAccessToken(User user)
    {
        var now = _clock.UtcNow;
        var expires = now.AddMinutes(_options.AccessTokenMinutes);

        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, user.Id.ToString()),
            new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
            new(JwtRegisteredClaimNames.Email, user.Email),
            new(ClaimTypes.Name, user.DisplayName),
            new(ClaimTypes.Role, user.SystemRole.ToString()),
            new(TenantIdClaim, user.TenantId.ToString())
        };

        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_options.SigningKey));
        var credentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        var token = new JwtSecurityToken(
            issuer: _options.Issuer,
            audience: _options.Audience,
            claims: claims,
            notBefore: now.UtcDateTime,
            expires: expires.UtcDateTime,
            signingCredentials: credentials);

        var encoded = new JwtSecurityTokenHandler().WriteToken(token);
        return new AccessToken(encoded, expires);
    }
}
