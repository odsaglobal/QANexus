using ATIP.Application.Common.Interfaces;
using ATIP.Application.Features.Users.Dtos;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ATIP.Application.Features.Users.Queries.ListUsers;

/// <summary>Lists all users in the current tenant (tenant scoping is applied by global query filters).</summary>
public sealed record ListUsersQuery : IRequest<IReadOnlyList<UserDto>>;

public sealed class ListUsersQueryHandler
    : IRequestHandler<ListUsersQuery, IReadOnlyList<UserDto>>
{
    private readonly IApplicationDbContext _db;

    public ListUsersQueryHandler(IApplicationDbContext db) => _db = db;

    public async Task<IReadOnlyList<UserDto>> Handle(
        ListUsersQuery request,
        CancellationToken cancellationToken)
    {
        var users = await _db.Users
            .AsNoTracking()
            .OrderByDescending(u => u.LastLoginAtUtc)
            .ThenBy(u => u.DisplayName)
            .ToListAsync(cancellationToken);

        return users.Select(UserDto.FromEntity).ToList();
    }
}
