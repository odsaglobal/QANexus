using ATIP.Application.Common.Interfaces;
using ATIP.Application.Features.SecretStore;
using Microsoft.AspNetCore.Mvc;

namespace ATIP.Api.Controllers;

/// <summary>External secret store (HashiCorp Vault). Never returns secret values.</summary>
[Route("api/v1/secret-store")]
public sealed class SecretStoreController : ApiControllerBase
{
    [HttpGet("status")]
    [ProducesResponseType(typeof(SecretStoreStatus), StatusCodes.Status200OK)]
    public async Task<ActionResult<SecretStoreStatus>> Status(CancellationToken cancellationToken) =>
        Ok(await Mediator.Send(new GetSecretStoreStatusQuery(), cancellationToken));

    [HttpPost("verify")]
    [ProducesResponseType(typeof(SecretReferenceCheck), StatusCodes.Status200OK)]
    public async Task<ActionResult<SecretReferenceCheck>> Verify(VerifySecretReferenceRequest request, CancellationToken cancellationToken) =>
        Ok(await Mediator.Send(new VerifySecretReferenceQuery(request.Reference), cancellationToken));
}

public sealed record VerifySecretReferenceRequest(string Reference);
