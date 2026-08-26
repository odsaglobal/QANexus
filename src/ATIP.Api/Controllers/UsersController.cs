using ATIP.Application.Features.Users.Commands.SyncCurrentUserProfile;
using ATIP.Application.Features.Users.Dtos;
using ATIP.Application.Features.Users.Queries.ListUsers;
using Microsoft.AspNetCore.Mvc;

namespace ATIP.Api.Controllers;

/// <summary>Tenant user (workspace member) administration.</summary>
[Route("api/v1/users")]
public sealed class UsersController : ApiControllerBase
{
    /// <summary>Lists the current tenant's users.</summary>
    [HttpGet]
    [ProducesResponseType(typeof(IReadOnlyList<UserDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<UserDto>>> List(CancellationToken cancellationToken)
    {
        var result = await Mediator.Send(new ListUsersQuery(), cancellationToken);
        return Ok(result);
    }

    /// <summary>Syncs the signed-in user's email/display name from the identity provider profile.</summary>
    [HttpPost("me/sync")]
    [ProducesResponseType(typeof(UserDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<UserDto>> SyncMyProfile(
        [FromBody] SyncCurrentUserProfileCommand command,
        CancellationToken cancellationToken)
    {
        var result = await Mediator.Send(command, cancellationToken);
        return Ok(result);
    }
}
