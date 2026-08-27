using ATIP.Domain.Enums;

namespace ATIP.Application.Common.Security;

/// <summary>
/// Marks a request as operating within a single project and declares the minimum project role the
/// caller must hold. The <c>ProjectAuthorizationBehavior</c> enforces this before the handler runs.
/// </summary>
public interface IProjectScopedRequest
{
    /// <summary>The project the request targets.</summary>
    Guid ProjectId { get; }

    /// <summary>Minimum project role required to perform the action.</summary>
    ProjectRole RequiredRole { get; }
}
