using ATIP.Domain.Entities;

namespace ATIP.Application.Features.Projects.Dtos;

/// <summary>A member of a project with their project-scoped role.</summary>
public sealed record ProjectMemberDto
{
    public required Guid UserId { get; init; }
    public required string Email { get; init; }
    public required string DisplayName { get; init; }
    public required string Role { get; init; }

    public static ProjectMemberDto FromEntity(ProjectMember m) => new()
    {
        UserId = m.UserId,
        Email = m.User.Email,
        DisplayName = m.User.DisplayName,
        Role = m.Role.ToString(),
    };
}
