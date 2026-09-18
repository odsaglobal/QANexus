using ATIP.Application.Features.Explorer.Commands.ExploreScenario;
using ATIP.Application.Features.Explorer.Dtos;
using ATIP.Application.Features.Explorer.Queries.GetLatestScenarioRun;
using ATIP.Application.Features.Scenarios.Commands.CreateManualScenario;
using ATIP.Application.Features.Scenarios.Commands.DeleteScenario;
using ATIP.Application.Features.Scenarios.Commands.DeleteScenarios;
using ATIP.Application.Features.Scenarios.Commands.GenerateScenarios;
using ATIP.Application.Features.Scenarios.Commands.GenerateScenariosFromStory;
using ATIP.Application.Features.Scenarios.Commands.ImportScenariosFromFile;
using ATIP.Application.Features.Scenarios.Commands.ProposedSteps;
using ATIP.Application.Features.Scenarios.Commands.SyncTestRailScenarios;
using ATIP.Application.Features.Scenarios.Commands.UpdateScenario;
using ATIP.Application.Features.Scenarios.Dtos;
using ATIP.Application.Features.Scenarios.Import;
using ATIP.Application.Features.Scenarios.Queries.GetJiraIssue;
using ATIP.Application.Features.Scenarios.Queries.ListScenarios;
using Microsoft.AspNetCore.Mvc;

namespace ATIP.Api.Controllers;

/// <summary>AI scenario generation and retrieval, nested under a project.</summary>
[Route("api/v1/projects/{projectId:guid}/scenarios")]
public sealed class ScenariosController : ApiControllerBase
{
    /// <summary>Lists scenarios for the project, optionally filtered to a feature.</summary>
    [HttpGet]
    [ProducesResponseType(typeof(IReadOnlyList<ScenarioDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<ScenarioDto>>> List(
        Guid projectId,
        [FromQuery] Guid? featureId,
        CancellationToken cancellationToken)
    {
        var result = await Mediator.Send(
            new ListScenariosQuery { ProjectId = projectId, FeatureId = featureId },
            cancellationToken);
        return Ok(result);
    }

    /// <summary>Generates AI scenarios for a feature, replacing prior AI-generated ones.</summary>
    [HttpPost("generate")]
    [ProducesResponseType(typeof(IReadOnlyList<ScenarioDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<IReadOnlyList<ScenarioDto>>> Generate(
        Guid projectId,
        [FromQuery] Guid featureId,
        CancellationToken cancellationToken)
    {
        var result = await Mediator.Send(new GenerateScenariosCommand(featureId), cancellationToken);
        return Ok(result);
    }

    /// <summary>Generates manual test-case scenarios from a free-text story grounded in the project's business context.</summary>
    [HttpPost("generate-from-story")]
    [ProducesResponseType(typeof(IReadOnlyList<ScenarioDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<IReadOnlyList<ScenarioDto>>> GenerateFromStory(
        Guid projectId,
        GenerateScenariosFromStoryCommand command,
        CancellationToken cancellationToken)
    {
        if (projectId != command.ProjectId)
        {
            return BadRequest("Route projectId and body projectId do not match.");
        }

        var result = await Mediator.Send(command, cancellationToken);
        return Ok(result);
    }

    /// <summary>Fetches a Jira issue's details by key (e.g. PROJ-123) to reference/prefill a scenario.</summary>
    [HttpGet("jira/{issueKey}")]
    [ProducesResponseType(typeof(ATIP.Application.Common.Models.JiraIssue), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ATIP.Application.Common.Models.JiraIssue>> GetJiraIssue(
        Guid projectId,
        string issueKey,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(issueKey))
        {
            return BadRequest("A Jira issue key is required.");
        }

        var result = await Mediator.Send(new GetJiraIssueQuery(issueKey), cancellationToken);
        return Ok(result);
    }
    [HttpPost("manual")]
    [ProducesResponseType(typeof(ScenarioDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ScenarioDto>> CreateManual(
        Guid projectId,
        CreateManualScenarioCommand command,
        CancellationToken cancellationToken)
    {
        if (projectId != command.ProjectId)
        {
            return BadRequest("Route projectId and body projectId do not match.");
        }

        var result = await Mediator.Send(command, cancellationToken);
        return Ok(result);
    }

    /// <summary>
    /// Downloads the .xlsx import template: a pre-filled sample sheet plus a "How to use" sheet
    /// describing every supported column.
    /// </summary>
    [HttpGet("import-template")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public IActionResult ImportTemplate()
        => File(ScenarioImportTemplate.Build(), ScenarioImportTemplate.ContentType, ScenarioImportTemplate.FileName);

    /// <summary>
    /// Imports manual test cases from CSV/XLSX and creates Manual scenarios with ordered steps.
    /// The file should include columns such as title, step/action, step_expected and optional case_id.
    /// Optionally adds every imported scenario to a new or existing suite.
    /// </summary>
    [HttpPost("import")]
    [ProducesResponseType(typeof(ImportScenariosResult), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ImportScenariosResult>> Import(
        Guid projectId,
        [FromForm] Guid featureId,
        [FromForm] IFormFile file,
        CancellationToken cancellationToken,
        [FromForm] ImportSuiteMode suiteMode = ImportSuiteMode.None,
        [FromForm] Guid? suiteId = null,
        [FromForm] string? newSuiteName = null)
    {
        if (file is null || file.Length == 0)
        {
            return BadRequest("Upload a non-empty .csv or .xlsx file.");
        }

        await using var stream = file.OpenReadStream();
        using var ms = new MemoryStream();
        await stream.CopyToAsync(ms, cancellationToken);

        var result = await Mediator.Send(new ImportScenariosFromFileCommand
        {
            ProjectId = projectId,
            FeatureId = featureId,
            FileName = file.FileName,
            Content = ms.ToArray(),
            SuiteMode = suiteMode,
            SuiteId = suiteId,
            NewSuiteName = newSuiteName,
        }, cancellationToken);

        return Ok(result);
    }

    /// <summary>Syncs test cases from TestRail and stores them as TestRail-backed scenarios.</summary>
    [HttpPost("sync/testrail")]
    [ProducesResponseType(typeof(IReadOnlyList<ScenarioDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<IReadOnlyList<ScenarioDto>>> SyncTestRail(
        Guid projectId,
        SyncTestRailScenariosCommand command,
        CancellationToken cancellationToken)
    {
        if (projectId != command.ProjectId)
        {
            return BadRequest("Route projectId and body projectId do not match.");
        }

        var result = await Mediator.Send(command, cancellationToken);
        return Ok(result);
    }

    /// <summary>Updates a scenario's metadata and replaces its entire step list.</summary>
    [HttpPut("{id:guid}")]
    [ProducesResponseType(typeof(ScenarioDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ScenarioDto>> Update(
        Guid projectId,
        Guid id,
        UpdateScenarioCommand command,
        CancellationToken cancellationToken)
    {
        if (id != command.Id)
        {
            return BadRequest("Route id and body id do not match.");
        }

        var result = await Mediator.Send(command, cancellationToken);
        return Ok(result);
    }

    /// <summary>Soft-deletes a scenario.</summary>
    [HttpDelete("{id:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Delete(
        Guid projectId, Guid id, CancellationToken cancellationToken)
    {
        await Mediator.Send(new DeleteScenarioCommand(id), cancellationToken);
        return NoContent();
    }

    /// <summary>Soft-deletes several scenarios at once. POST rather than DELETE because the ids travel in the body.</summary>
    [HttpPost("bulk-delete")]
    [ProducesResponseType(typeof(DeleteScenariosResult), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<DeleteScenariosResult>> BulkDelete(
        Guid projectId,
        DeleteScenariosCommand command,
        CancellationToken cancellationToken)
    {
        if (projectId != command.ProjectId)
        {
            return BadRequest("Route projectId and body projectId do not match.");
        }

        var result = await Mediator.Send(command, cancellationToken);
        return Ok(result);
    }

    /// <summary>Runs a scenario-scoped exploration that walks this scenario's steps and records per-step results.</summary>
    [HttpPost("{id:guid}/explore")]
    [ProducesResponseType(typeof(ExplorationSessionDto), StatusCodes.Status202Accepted)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ExplorationSessionDto>> Explore(
        Guid projectId,
        Guid id,
        [FromBody] ExploreScenarioRequest? body,
        CancellationToken cancellationToken)
    {
        var result = await Mediator.Send(new ExploreScenarioCommand
        {
            ProjectId = projectId,
            ScenarioId = id,
            EnvironmentId = body?.EnvironmentId,
        }, cancellationToken);
        return Accepted(result);
    }

    /// <summary>Runs the scenario's saved steps in order as a test, recording Passed/Healed/Failed per step.</summary>
    [HttpPost("{id:guid}/run")]
    [ProducesResponseType(typeof(ExplorationSessionDto), StatusCodes.Status202Accepted)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ExplorationSessionDto>> Run(
        Guid projectId,
        Guid id,
        [FromBody] ExploreScenarioRequest? body,
        CancellationToken cancellationToken)
    {
        var result = await Mediator.Send(new ExploreScenarioCommand
        {
            ProjectId = projectId,
            ScenarioId = id,
            EnvironmentId = body?.EnvironmentId,
            ExecuteSavedSteps = true,
        }, cancellationToken);
        return Accepted(result);
    }
    [HttpGet("{id:guid}/run")]
    [ProducesResponseType(typeof(ScenarioRunDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<ActionResult<ScenarioRunDto>> GetLatestRun(
        Guid projectId, Guid id, CancellationToken cancellationToken)
    {
        var result = await Mediator.Send(new GetLatestScenarioRunQuery(projectId, id), cancellationToken);
        return result is null ? NoContent() : Ok(result);
    }

    /// <summary>Applies the AI-discovered proposed steps to the scenario (backing up the current steps).</summary>
    [HttpPost("{id:guid}/proposed-steps/apply")]
    [ProducesResponseType(typeof(ScenarioDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<ScenarioDto>> ApplyProposedSteps(Guid projectId, Guid id, CancellationToken cancellationToken)
    {
        var result = await Mediator.Send(new ApplyProposedStepsCommand(projectId, id), cancellationToken);
        return Ok(result);
    }

    /// <summary>Discards the AI-discovered proposed steps without changing the scenario's steps.</summary>
    [HttpPost("{id:guid}/proposed-steps/discard")]
    [ProducesResponseType(typeof(ScenarioDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<ScenarioDto>> DiscardProposedSteps(Guid projectId, Guid id, CancellationToken cancellationToken)
    {
        var result = await Mediator.Send(new DiscardProposedStepsCommand(projectId, id), cancellationToken);
        return Ok(result);
    }

    /// <summary>Reverts the scenario's steps to the backup taken when proposed steps were applied.</summary>
    [HttpPost("{id:guid}/steps/revert")]
    [ProducesResponseType(typeof(ScenarioDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<ScenarioDto>> RevertSteps(Guid projectId, Guid id, CancellationToken cancellationToken)
    {
        var result = await Mediator.Send(new RevertStepsCommand(projectId, id), cancellationToken);
        return Ok(result);
    }
}

/// <summary>Optional body for the scenario explore endpoint.</summary>
public sealed record ExploreScenarioRequest
{
    public Guid? EnvironmentId { get; init; }
}
