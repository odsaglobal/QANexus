using ATIP.Application.Common.Models;
using ATIP.Application.Features.Projects.Commands.CreateProject;
using ATIP.Application.Features.Projects.Commands.DeleteProject;
using ATIP.Application.Features.Projects.Commands.UpdateProject;
using ATIP.Application.Features.Projects.Dtos;
using ATIP.Application.Features.Projects.Queries.GetProject;
using ATIP.Application.Features.Projects.Queries.ListProjects;
using Microsoft.AspNetCore.Mvc;

namespace ATIP.Api.Controllers;

/// <summary>CRUD endpoints for projects within the caller's tenant.</summary>
public sealed class ProjectsController : ApiControllerBase
{
    /// <summary>Returns a paged list of projects, with optional search, status filter and sorting.</summary>
    [HttpGet]
    [ProducesResponseType(typeof(PagedResult<ProjectDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<PagedResult<ProjectDto>>> List(
        [FromQuery] ListProjectsQuery query,
        CancellationToken cancellationToken)
    {
        var result = await Mediator.Send(query, cancellationToken);
        return Ok(result);
    }

    /// <summary>Returns a single project by id.</summary>
    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(ProjectDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ProjectDto>> Get(Guid id, CancellationToken cancellationToken)
    {
        var result = await Mediator.Send(new GetProjectQuery(id), cancellationToken);
        return Ok(result);
    }

    /// <summary>Creates a new project and enrolls the caller as its owner.</summary>
    [HttpPost]
    [ProducesResponseType(typeof(ProjectDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<ProjectDto>> Create(
        CreateProjectCommand command,
        CancellationToken cancellationToken)
    {
        var result = await Mediator.Send(command, cancellationToken);
        return CreatedAtAction(nameof(Get), new { id = result.Id }, result);
    }

    /// <summary>Updates a project's name, description and status.</summary>
    [HttpPut("{id:guid}")]
    [ProducesResponseType(typeof(ProjectDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ProjectDto>> Update(
        Guid id,
        UpdateProjectCommand command,
        CancellationToken cancellationToken)
    {
        if (id != command.Id)
        {
            return BadRequest("Route id and body id do not match.");
        }

        var result = await Mediator.Send(command, cancellationToken);
        return Ok(result);
    }

    /// <summary>Soft-deletes a project.</summary>
    [HttpDelete("{id:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Delete(Guid id, CancellationToken cancellationToken)
    {
        await Mediator.Send(new DeleteProjectCommand(id), cancellationToken);
        return NoContent();
    }
}
