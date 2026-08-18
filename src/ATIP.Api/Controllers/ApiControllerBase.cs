using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ATIP.Api.Controllers;

/// <summary>
/// Base for all v1 API controllers. Requires authentication by default and exposes the
/// mediator so controllers stay thin — they only translate HTTP to/from application requests.
/// </summary>
[ApiController]
[Authorize]
[Route("api/v1/[controller]")]
[Produces("application/json")]
public abstract class ApiControllerBase : ControllerBase
{
    private ISender? _mediator;

    protected ISender Mediator =>
        _mediator ??= HttpContext.RequestServices.GetRequiredService<ISender>();
}
