using System.Security.Claims;
using ATIP.Application.Common.Interfaces;
using ATIP.Domain.Enums;

namespace ATIP.Api.Services;

/// <summary>
/// Resolves the current caller from the validated JWT on the ambient HTTP context.
/// Registered as scoped so each request sees its own identity.
/// </summary>
public sealed class CurrentUser : ICurrentUser
{
    private readonly IHttpContextAccessor _accessor;

    public CurrentUser(IHttpContextAccessor accessor) => _accessor = accessor;

    private ClaimsPrincipal? Principal => _accessor.HttpContext?.User;

    public Guid? UserId =>
        TryParseGuid(Principal?.FindFirstValue(Auth0ClaimsTransformer.AppUserIdClaim)
                     ?? Principal?.FindFirstValue(ClaimTypes.NameIdentifier)
                     ?? Principal?.FindFirstValue("sub"));

    public Guid? TenantId =>
        TryParseGuid(Principal?.FindFirstValue(Auth0ClaimsTransformer.TenantIdClaim));

    public string? Email => Principal?.FindFirstValue(ClaimTypes.Email);

    public SystemRole? SystemRole =>
        Enum.TryParse<SystemRole>(Principal?.FindFirstValue(ClaimTypes.Role), out var role) ? role : null;

    public string? IpAddress => _accessor.HttpContext?.Connection.RemoteIpAddress?.ToString();

    public bool IsAuthenticated => Principal?.Identity?.IsAuthenticated ?? false;

    private static Guid? TryParseGuid(string? value) =>
        Guid.TryParse(value, out var guid) ? guid : null;
}
