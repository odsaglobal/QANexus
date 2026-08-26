using ATIP.Application.Features.Requirements.Commands.AnalyzeRequirement;
using ATIP.Application.Features.Requirements.Commands.CreateManualFeature;
using ATIP.Application.Features.Requirements.Commands.DeleteRequirement;
using ATIP.Application.Features.Requirements.Commands.UpdateRequirementContent;
using ATIP.Application.Features.Requirements.Commands.UploadRequirement;
using ATIP.Application.Features.Requirements.Dtos;
using ATIP.Application.Features.Requirements.Queries.GetRequirement;
using ATIP.Application.Features.Requirements.Queries.ListRequirements;
using Microsoft.AspNetCore.Mvc;

namespace ATIP.Api.Controllers;

/// <summary>Requirement document upload and AI analysis, nested under a project.</summary>
[Route("api/v1/projects/{projectId:guid}/requirements")]
public sealed class RequirementsController : ApiControllerBase
{
    /// <summary>Lists requirement documents for the project.</summary>
    [HttpGet]
    [ProducesResponseType(typeof(IReadOnlyList<RequirementDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<RequirementDto>>> List(
        Guid projectId,
        CancellationToken cancellationToken)
    {
        var result = await Mediator.Send(new ListRequirementsQuery(projectId), cancellationToken);
        return Ok(result);
    }

    /// <summary>Returns a requirement with its extracted module → feature → story structure.</summary>
    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(RequirementDetailDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<RequirementDetailDto>> Get(
        Guid projectId,
        Guid id,
        CancellationToken cancellationToken)
    {
        var result = await Mediator.Send(new GetRequirementQuery(id), cancellationToken);
        return Ok(result);
    }

    /// <summary>Uploads a requirement document (PDF, DOCX, Markdown, text, Swagger).</summary>
    [HttpPost]
    [RequestSizeLimit(20 * 1024 * 1024)]
    [ProducesResponseType(typeof(RequirementDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<RequirementDto>> Upload(
        Guid projectId,
        [FromForm] string name,
        IFormFile file,
        CancellationToken cancellationToken)
    {
        using var stream = new MemoryStream();
        await file.CopyToAsync(stream, cancellationToken);

        var command = new UploadRequirementCommand
        {
            ProjectId = projectId,
            Name = string.IsNullOrWhiteSpace(name) ? file.FileName : name,
            FileName = file.FileName,
            Content = stream.ToArray(),
            ContentType = file.ContentType,
        };

        var result = await Mediator.Send(command, cancellationToken);
        return CreatedAtAction(nameof(Get), new { projectId, id = result.Id }, result);
    }

    /// <summary>Runs AI analysis to extract modules, features and user stories.</summary>
    [HttpPost("{id:guid}/analyze")]
    [ProducesResponseType(typeof(RequirementDetailDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<RequirementDetailDto>> Analyze(
        Guid projectId,
        Guid id,
        CancellationToken cancellationToken)
    {
        var result = await Mediator.Send(new AnalyzeRequirementCommand(id), cancellationToken);
        return Ok(result);
    }

    /// <summary>
    /// Creates a manual requirement → module → feature container (no document upload) so users can
    /// author scenarios by hand and drive scenario-based exploration.
    /// </summary>
    [HttpPost("manual-feature")]
    [ProducesResponseType(typeof(ManualFeatureDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ManualFeatureDto>> CreateManualFeature(
        Guid projectId,
        CreateManualFeatureCommand command,
        CancellationToken cancellationToken)
    {
        if (projectId != command.ProjectId)
        {
            return BadRequest("Route projectId and body projectId do not match.");
        }

        var result = await Mediator.Send(command, cancellationToken);
        return Ok(result);
    }

    /// <summary>Updates the extracted text (content) of a business-context document.</summary>
    [HttpPut("{id:guid}/content")]
    [ProducesResponseType(typeof(RequirementDetailDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<RequirementDetailDto>> UpdateContent(
        Guid projectId,
        Guid id,
        [FromBody] UpdateRequirementContentRequest body,
        CancellationToken cancellationToken)
    {
        var result = await Mediator.Send(new UpdateRequirementContentCommand(id, body.Content ?? string.Empty), cancellationToken);
        return Ok(result);
    }

    /// <summary>Soft-deletes a requirement and its extracted structure.</summary>
    [HttpDelete("{id:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Delete(Guid projectId, Guid id, CancellationToken cancellationToken)
    {
        await Mediator.Send(new DeleteRequirementCommand(id), cancellationToken);
        return NoContent();
    }
}

/// <summary>Request body for updating a document's content.</summary>
public sealed record UpdateRequirementContentRequest(string? Content);
