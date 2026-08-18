using ATIP.Application.Features.Auth.Commands.Login;
using ATIP.Application.Features.Auth.Commands.RegisterTenant;
using ATIP.Application.Features.Auth.Dtos;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ATIP.Api.Controllers;

/// <summary>Registration and login endpoints. These are the only anonymous endpoints in the API.</summary>
[AllowAnonymous]
public sealed class AuthController : ApiControllerBase
{
    /// <summary>Creates a new organization (tenant) and its first admin user, returning an access token.</summary>
    [HttpPost("register")]
    [ProducesResponseType(typeof(AuthResultDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<AuthResultDto>> Register(
        RegisterTenantCommand command,
        CancellationToken cancellationToken)
    {
        var result = await Mediator.Send(command, cancellationToken);
        return Ok(result);
    }

    /// <summary>Authenticates an existing local account and returns an access token.</summary>
    [HttpPost("login")]
    [ProducesResponseType(typeof(AuthResultDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<AuthResultDto>> Login(
        LoginCommand command,
        CancellationToken cancellationToken)
    {
        var result = await Mediator.Send(command, cancellationToken);
        return Ok(result);
    }
}
