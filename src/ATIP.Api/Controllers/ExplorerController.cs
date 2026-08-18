using ATIP.Application.Common.Models;
using ATIP.Application.Features.Explorer.Commands.CancelExploration;
using ATIP.Application.Features.Explorer.Commands.StartExploration;
using ATIP.Application.Features.Explorer.Dtos;
using ATIP.Application.Features.Explorer.Queries.GetDiscoveredPage;
using ATIP.Application.Features.Explorer.Queries.GetExplorationSession;
using ATIP.Application.Features.Explorer.Queries.ListDiscoveredElements;
using ATIP.Application.Features.Explorer.Queries.ListDiscoveredPages;
using ATIP.Application.Features.Explorer.Queries.ListExplorationSessions;
using Microsoft.AspNetCore.Mvc;

namespace ATIP.Api.Controllers;

/// <summary>Application Explorer — sessions, discovered pages and element locators.</summary>
[Route("api/v1/projects/{projectId:guid}/explorer")]
public sealed class ExplorerController : ApiControllerBase
{
    // ── Sessions ──────────────────────────────────────────────────────────────

    /// <summary>Lists exploration sessions for the project.</summary>
    [HttpGet("sessions")]
    [ProducesResponseType(typeof(IReadOnlyList<ExplorationSessionDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<ExplorationSessionDto>>> ListSessions(
        Guid projectId, CancellationToken ct)
    {
        var result = await Mediator.Send(new ListExplorationSessionsQuery(projectId), ct);
        return Ok(result);
    }

    /// <summary>Returns a single exploration session with live progress counters.</summary>
    [HttpGet("sessions/{sessionId:guid}")]
    [ProducesResponseType(typeof(ExplorationSessionDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ExplorationSessionDto>> GetSession(
        Guid projectId, Guid sessionId, CancellationToken ct)
    {
        var result = await Mediator.Send(new GetExplorationSessionQuery(sessionId), ct);
        return Ok(result);
    }

    /// <summary>Starts a new exploration session and queues it for background browser crawl.</summary>
    [HttpPost("sessions")]
    [ProducesResponseType(typeof(ExplorationSessionDto), StatusCodes.Status202Accepted)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ExplorationSessionDto>> StartSession(
        Guid projectId,
        StartExplorationCommand command,
        CancellationToken ct)
    {
        if (projectId != command.ProjectId)
        {
            return BadRequest("Route projectId and body projectId do not match.");
        }

        var result = await Mediator.Send(command, ct);
        return AcceptedAtAction(nameof(GetSession), new { projectId, sessionId = result.Id }, result);
    }

    /// <summary>Requests cancellation of a running exploration session.</summary>
    [HttpPost("sessions/{sessionId:guid}/cancel")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> CancelSession(Guid projectId, Guid sessionId, CancellationToken ct)
    {
        await Mediator.Send(new CancelExplorationCommand(sessionId), ct);
        return NoContent();
    }

    // ── Discovered Pages ───────────────────────────────────────────────────────

    /// <summary>Lists all pages discovered in a session.</summary>
    [HttpGet("sessions/{sessionId:guid}/pages")]
    [ProducesResponseType(typeof(IReadOnlyList<DiscoveredPageDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<DiscoveredPageDto>>> ListPages(
        Guid projectId, Guid sessionId, CancellationToken ct)
    {
        var result = await Mediator.Send(new ListDiscoveredPagesQuery(sessionId), ct);
        return Ok(result);
    }

    /// <summary>Returns a page with its accessibility tree and full element list.</summary>
    [HttpGet("pages/{pageId:guid}")]
    [ProducesResponseType(typeof(DiscoveredPageDetailDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<DiscoveredPageDetailDto>> GetPage(
        Guid projectId, Guid pageId, CancellationToken ct)
    {
        var result = await Mediator.Send(new GetDiscoveredPageQuery(pageId), ct);
        return Ok(result);
    }

    // ── Element Locator Repository ─────────────────────────────────────────────

    /// <summary>Paged, searchable list of discovered elements across the project or a single page.</summary>
    [HttpGet("elements")]
    [ProducesResponseType(typeof(PagedResult<DiscoveredElementDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<PagedResult<DiscoveredElementDto>>> ListElements(
        Guid projectId,
        [FromQuery] ListDiscoveredElementsQuery query,
        CancellationToken ct)
    {
        var q = query with { ProjectId = projectId };
        var result = await Mediator.Send(q, ct);
        return Ok(result);
    }
}
