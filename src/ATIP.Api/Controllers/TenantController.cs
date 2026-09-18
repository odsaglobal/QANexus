using ATIP.Application.Features.Tenants.Commands.UpdateTenant;
using ATIP.Application.Features.Tenants.Dtos;
using Microsoft.AspNetCore.Mvc;

namespace ATIP.Api.Controllers;

/// <summary>The caller's own tenant (client workspace).</summary>
[Route("api/v1/tenant")]
public sealed class TenantController : ApiControllerBase
{
    /// <summary>Renames the caller's workspace (and marks it onboarded). TenantAdmin only.</summary>
    [HttpPut]
    [ProducesResponseType(typeof(TenantDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<TenantDto>> Update(
        [FromBody] UpdateTenantCommand command,
        CancellationToken cancellationToken)
    {
        var result = await Mediator.Send(command, cancellationToken);
        return Ok(result);
    }
}
