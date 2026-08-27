using ATIP.Application.Features.ApiKeys.Commands.CreateApiKey;
using ATIP.Application.Features.ApiKeys.Commands.RevokeApiKey;
using ATIP.Application.Features.ApiKeys.Dtos;
using ATIP.Application.Features.ApiKeys.Queries.ListApiKeys;
using Microsoft.AspNetCore.Mvc;

namespace ATIP.Api.Controllers;

/// <summary>Manage tenant API keys used by CI systems (X-Api-Key header).</summary>
[Route("api/v1/api-keys")]
public sealed class ApiKeysController : ApiControllerBase
{
    /// <summary>Lists the tenant's API keys (never returns secrets).</summary>
    [HttpGet]
    [ProducesResponseType(typeof(IReadOnlyList<ApiKeyDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<ApiKeyDto>>> List(CancellationToken cancellationToken)
    {
        var result = await Mediator.Send(new ListApiKeysQuery(), cancellationToken);
        return Ok(result);
    }

    /// <summary>Creates a key and returns the plaintext once.</summary>
    [HttpPost]
    [ProducesResponseType(typeof(CreatedApiKeyDto), StatusCodes.Status201Created)]
    public async Task<ActionResult<CreatedApiKeyDto>> Create(
        [FromBody] CreateApiKeyCommand command,
        CancellationToken cancellationToken)
    {
        var result = await Mediator.Send(command, cancellationToken);
        return StatusCode(StatusCodes.Status201Created, result);
    }

    /// <summary>Revokes an API key.</summary>
    [HttpDelete("{id:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> Revoke(Guid id, CancellationToken cancellationToken)
    {
        await Mediator.Send(new RevokeApiKeyCommand(id), cancellationToken);
        return NoContent();
    }
}
