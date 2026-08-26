using ATIP.Application.Features.Environments.Commands.CreateEnvironment;
using ATIP.Application.Features.Environments.Commands.DeleteEnvironment;
using ATIP.Application.Features.Environments.Commands.UpdateEnvironment;
using ATIP.Application.Features.Environments.Commands.UpdateEnvironmentVariables;
using ATIP.Application.Features.Environments.Dtos;
using ATIP.Application.Features.Environments.Queries.GetEnvironmentVariables;
using ATIP.Application.Features.Environments.Queries.ListEnvironments;
using Microsoft.AspNetCore.Mvc;

namespace ATIP.Api.Controllers;

/// <summary>
/// Environment endpoints, nested under a project. Environments are the deployment targets
/// explorations and executions run against.
/// </summary>
[Route("api/v1/projects/{projectId:guid}/environments")]
public sealed class EnvironmentsController : ApiControllerBase
{
    /// <summary>Lists all environments belonging to the project.</summary>
    [HttpGet]
    [ProducesResponseType(typeof(IReadOnlyList<EnvironmentDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<EnvironmentDto>>> List(
        Guid projectId,
        CancellationToken cancellationToken)
    {
        var result = await Mediator.Send(new ListEnvironmentsQuery(projectId), cancellationToken);
        return Ok(result);
    }

    /// <summary>Creates a new environment within the project.</summary>
    [HttpPost]
    [ProducesResponseType(typeof(EnvironmentDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<EnvironmentDto>> Create(
        Guid projectId,
        CreateEnvironmentCommand command,
        CancellationToken cancellationToken)
    {
        if (projectId != command.ProjectId)
        {
            return BadRequest("Route projectId and body projectId do not match.");
        }

        var result = await Mediator.Send(command, cancellationToken);
        return CreatedAtAction(nameof(List), new { projectId }, result);
    }

    /// <summary>Deletes an environment.</summary>
    [HttpDelete("{id:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Delete(Guid projectId, Guid id, CancellationToken cancellationToken)
    {
        await Mediator.Send(new DeleteEnvironmentCommand(id), cancellationToken);
        return NoContent();
    }

    /// <summary>Updates an existing environment.</summary>
    [HttpPut("{id:guid}")]
    [ProducesResponseType(typeof(EnvironmentDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<EnvironmentDto>> Update(
        Guid projectId,
        Guid id,
        UpdateEnvironmentCommand command,
        CancellationToken cancellationToken)
    {
        if (projectId != command.ProjectId || id != command.Id)
        {
            return BadRequest("Route projectId/id and body values do not match.");
        }

        var result = await Mediator.Send(command, cancellationToken);
        return Ok(result);
    }

    /// <summary>Gets the Postman-style key/value variables for an environment.</summary>
    [HttpGet("{id:guid}/variables")]
    [ProducesResponseType(typeof(IReadOnlyList<EnvironmentVariableDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<IReadOnlyList<EnvironmentVariableDto>>> GetVariables(
        Guid projectId,
        Guid id,
        CancellationToken cancellationToken)
    {
        var result = await Mediator.Send(new GetEnvironmentVariablesQuery(projectId, id), cancellationToken);
        return Ok(result);
    }

    /// <summary>Replaces the full set of key/value variables for an environment.</summary>
    [HttpPut("{id:guid}/variables")]
    [ProducesResponseType(typeof(IReadOnlyList<EnvironmentVariableDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<IReadOnlyList<EnvironmentVariableDto>>> UpdateVariables(
        Guid projectId,
        Guid id,
        UpdateEnvironmentVariablesCommand command,
        CancellationToken cancellationToken)
    {
        if (projectId != command.ProjectId || id != command.EnvironmentId)
        {
            return BadRequest("Route projectId/environmentId and body values do not match.");
        }

        var result = await Mediator.Send(command, cancellationToken);
        return Ok(result);
    }
}
