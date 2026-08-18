using ATIP.Domain.Enums;

namespace ATIP.Application.Common.Interfaces;

/// <summary>
/// Ambient information about the caller, resolved from the JWT on each request.
/// Implemented in the API layer over <c>IHttpContextAccessor</c>.
/// </summary>
public interface ICurrentUser
{
    Guid? UserId { get; }

    Guid? TenantId { get; }

    string? Email { get; }

    SystemRole? SystemRole { get; }

    bool IsAuthenticated { get; }
}
