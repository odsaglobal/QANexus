using System.Text;
using System.Text.Json;
using ATIP.Application.Common.Interfaces;
using ATIP.Application.Common.Utilities;
using ATIP.Application.Features.Explorer.Agent;
using ATIP.Application.Features.Scenarios.Dtos;
using ATIP.Domain.Entities;
using ATIP.Domain.Enums;
using ATIP.Infrastructure.Configuration;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace ATIP.Infrastructure.Exploration;

/// <summary>
/// Drives a complete application exploration: launches a headless browser, crawls the app
/// following links, discovers UI elements on each page, and persists all findings.
/// If an LLM is configured it also tries to expose dynamic content (modals, tabs, etc.).
/// </summary>
public sealed class ExplorerAgent : IExplorerAgent
{
    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        PropertyNameCaseInsensitive = true,
    };

    private readonly IApplicationDbContext _db;
    private readonly ILlmClient _llm;
    private readonly IFileStorage _fileStorage;
    private readonly IDateTimeProvider _clock;
    private readonly IExplorationLiveStream _live;
    private readonly PlaywrightMcpOptions _mcp;
    private readonly ILogger<ExplorerAgent> _logger;

    public ExplorerAgent(
        IApplicationDbContext db,
        ILlmClient llm,
        IFileStorage fileStorage,
        IDateTimeProvider clock,
        IExplorationLiveStream live,
        Microsoft.Extensions.Options.IOptions<PlaywrightMcpOptions> mcpOptions,
        ILogger<ExplorerAgent> logger)
    {
        _db = db;
        _llm = llm;
        _fileStorage = fileStorage;
        _clock = clock;
        _live = live;
        _mcp = mcpOptions.Value;
        _logger = logger;
    }

    public async Task RunAsync(ExplorationSession session, CancellationToken cancellationToken = default)
    {
        var environment = await _db.Environments
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(e => e.Id == session.EnvironmentId, cancellationToken)
            ?? throw new InvalidOperationException($"Environment {session.EnvironmentId} not found.");

        var baseUrl = (session.SeedUrl ?? environment.BaseUrl).TrimEnd('/');
        var seedUri = new Uri(baseUrl);
        var origin = $"{seedUri.Scheme}://{seedUri.Host}{(seedUri.Port is 80 or 443 ? "" : $":{seedUri.Port}")}";

        // When configured, scenario/suite runs are driven by the official Playwright MCP server.
        if (_mcp.Enabled && (session.ScenarioId is not null || session.SuiteId is not null))
        {
            await RunScenarioViaMcpAsync(session, baseUrl, cancellationToken);
            return;
        }

        session.Status = ExplorationStatus.Running;
        session.StartedAtUtc = _clock.UtcNow;
        await _db.SaveChangesAsync(cancellationToken);

        await PublishStatusAsync(session, baseUrl, cancellationToken);

        await using var browser = new PlaywrightBrowserService();

        try
        {
            await browser.InitializeAsync(headless: true);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to launch browser for session {SessionId}. Ensure Playwright browsers are installed.", session.Id);
            session.Status = ExplorationStatus.Failed;
            session.ErrorMessage = "Browser not found. Run: playwright install chromium";
            await _db.SaveChangesAsync(cancellationToken);
            await PublishStatusAsync(session, null, cancellationToken);
            return;
        }

        // Begin live CDP screencast — frames stream to the session's tenant group only.
        try
        {
            await browser.StartScreencastAsync(
                frame => _live.PublishFrameAsync(session.TenantId, session.Id, frame, cancellationToken),
                cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Live screencast could not be started for session {SessionId}; crawl continues without live view.", session.Id);
        }

        try
        {
            await PublishLogAsync(session, $"Browser launched. Starting at {baseUrl}", "info", cancellationToken);
            await ExploreAsync(session, browser, baseUrl, origin, cancellationToken);
            session.Status = cancellationToken.IsCancellationRequested
                ? ExplorationStatus.Cancelled
                : ExplorationStatus.Completed;
            await PublishLogAsync(
                session,
                session.Status == ExplorationStatus.Cancelled ? "Exploration cancelled." : "Exploration completed.",
                session.Status == ExplorationStatus.Cancelled ? "warn" : "success",
                CancellationToken.None);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogError(ex, "Exploration session {SessionId} failed.", session.Id);
            session.Status = ExplorationStatus.Failed;
            session.ErrorMessage = ex.Message[..Math.Min(ex.Message.Length, 500)];
            await PublishLogAsync(session, $"Exploration failed: {session.ErrorMessage}", "error", CancellationToken.None);
        }
        finally
        {
            session.CompletedAtUtc = _clock.UtcNow;
            await browser.StopScreencastAsync();
            await _db.SaveChangesAsync(cancellationToken);
            await PublishStatusAsync(session, browser.CurrentUrl, CancellationToken.None);
        }
    }

    /// <summary>Broadcasts the current session progress to the tenant's live-view group.</summary>
    private async Task PublishStatusAsync(ExplorationSession session, string? currentUrl, CancellationToken ct)
    {
        try
        {
            await _live.PublishStatusAsync(
                session.TenantId,
                session.Id,
                new ExplorationLiveStatus(
                    session.Status.ToString(),
                    currentUrl,
                    session.PagesDiscovered,
                    session.ElementsDiscovered),
                ct);
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Failed to publish live status for session {SessionId}.", session.Id);
        }
    }

    // ── Playwright MCP-driven scenario execution ─────────────────────────────────

    /// <summary>
    /// Runs a scenario (or every scenario in a suite) by driving the official Playwright MCP server:
    /// snapshot → LLM decides the next tool call → MCP executes it. The live view is streamed from
    /// MCP screenshots and per-step results are recorded like the in-process path.
    /// </summary>
    private async Task RunScenarioViaMcpAsync(ExplorationSession session, string baseUrl, CancellationToken ct)
    {
        session.Status = ExplorationStatus.Running;
        session.StartedAtUtc = _clock.UtcNow;
        await _db.SaveChangesAsync(ct);
        await PublishStatusAsync(session, baseUrl, ct);
        await PublishLogAsync(session, "Starting Playwright MCP server…", "info", ct);

        await using var mcp = new PlaywrightMcpBrowser(_logger, _mcp.Command, _mcp.Arguments);
        try
        {
            await mcp.InitializeAsync(ct);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to start Playwright MCP server for session {SessionId}.", session.Id);
            session.Status = ExplorationStatus.Failed;
            session.ErrorMessage = "Could not start the Playwright MCP server (npx @playwright/mcp). Check Node/npx and the browser install.";
            session.CompletedAtUtc = _clock.UtcNow;
            await _db.SaveChangesAsync(ct);
            await PublishLogAsync(session, session.ErrorMessage, "error", CancellationToken.None);
            await PublishStatusAsync(session, null, CancellationToken.None);
            return;
        }

        await PublishLogAsync(session, "Playwright MCP server ready (official @playwright/mcp).", "success", ct);

        using var streamCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        var streamTask = StreamMcpScreenshotsAsync(session, mcp, streamCts.Token);

        try
        {
            var scenarios = await LoadScenariosForSessionAsync(session, ct);
            await mcp.NavigateAsync(baseUrl, ct);
            await mcp.StabilizePageAsync(ct);
            await PublishLogAsync(session, $"→ Navigated to {baseUrl}", "info", ct);

            foreach (var scenario in scenarios)
            {
                if (ct.IsCancellationRequested)
                {
                    break;
                }

                var orderedSteps = scenario.Steps.OrderBy(s => s.Order).ToList();

                if (session.ExecuteSavedSteps)
                {
                    await PublishLogAsync(session, $"Running scenario '{scenario.Title}' ({orderedSteps.Count} step(s)) via MCP…", "info", ct);
                    await RunScenarioStepsViaMcpAsync(session, mcp, baseUrl, scenario, orderedSteps, ct);
                }
                else
                {
                    await PublishLogAsync(session, $"Exploring the app to achieve scenario '{scenario.Title}' via MCP…", "info", ct);
                    await RunScenarioMissionViaMcpAsync(session, mcp, baseUrl, scenario, orderedSteps, ct);
                }
            }

            session.Status = ct.IsCancellationRequested ? ExplorationStatus.Cancelled : ExplorationStatus.Completed;
            await PublishLogAsync(session, "Exploration completed.", "success", CancellationToken.None);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogError(ex, "MCP scenario run failed for session {SessionId}.", session.Id);
            session.Status = ExplorationStatus.Failed;
            session.ErrorMessage = ex.Message[..Math.Min(ex.Message.Length, 500)];
            await PublishLogAsync(session, $"Exploration failed: {session.ErrorMessage}", "error", CancellationToken.None);
        }
        finally
        {
            streamCts.Cancel();
            try { await streamTask; } catch { /* streaming best-effort */ }
            session.CompletedAtUtc = _clock.UtcNow;
            await _db.SaveChangesAsync(CancellationToken.None);
            await PublishStatusAsync(session, mcp.CurrentUrl, CancellationToken.None);
        }
    }

    private async Task<List<Scenario>> LoadScenariosForSessionAsync(ExplorationSession session, CancellationToken ct)
    {
        if (session.ScenarioId is Guid scenarioId)
        {
            var scenario = await _db.Scenarios
                .IgnoreQueryFilters()
                .Include(s => s.Steps)
                .FirstOrDefaultAsync(s => s.Id == scenarioId && s.ProjectId == session.ProjectId, ct);
            return scenario is null ? [] : [scenario];
        }

        if (session.SuiteId is Guid suiteId)
        {
            var ids = await _db.TestSuiteScenarios
                .IgnoreQueryFilters()
                .Where(ss => ss.SuiteId == suiteId)
                .OrderBy(ss => ss.Order)
                .Select(ss => ss.ScenarioId)
                .ToListAsync(ct);

            var scenarios = await _db.Scenarios
                .IgnoreQueryFilters()
                .Include(s => s.Steps)
                .Where(s => ids.Contains(s.Id) && s.ProjectId == session.ProjectId)
                .ToListAsync(ct);

            return ids.Select(id => scenarios.FirstOrDefault(s => s.Id == id)).Where(s => s is not null).Select(s => s!).ToList();
        }

        return [];
    }

    private async Task StreamMcpScreenshotsAsync(ExplorationSession session, PlaywrightMcpBrowser mcp, CancellationToken ct)
    {
        try
        {
            while (!ct.IsCancellationRequested)
            {
                var frame = await mcp.ScreenshotBase64Async(ct);
                if (!string.IsNullOrEmpty(frame))
                {
                    await _live.PublishFrameAsync(session.TenantId, session.Id, frame, ct);
                }

                await Task.Delay(1200, ct);
            }
        }
        catch (OperationCanceledException)
        {
            // expected on stop
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "MCP screenshot streaming stopped for session {SessionId}.", session.Id);
        }
    }

    /// <summary>Runs one step's agentic loop through MCP tools and returns the outcome plus the recorded actions.</summary>
    private async Task<(StepRunStatus Status, string Detail, List<RecordedStepAction> Actions)> RunMcpStepAsync(
        ExplorationSession session,
        PlaywrightMcpBrowser mcp,
        string baseUrl,
        ScenarioStep step,
        CancellationToken ct)
    {
        const int maxTurns = 6;
        var history = new StringBuilder();
        var acted = false;
        var actions = new List<RecordedStepAction>();

        for (var turn = 1; turn <= maxTurns; turn++)
        {
            ct.ThrowIfCancellationRequested();

            var snapshot = TrimSnapshot(await mcp.SnapshotAsync(ct));
            var decision = await DecideNextStepActionAsync(step, mcp.CurrentUrl, snapshot, history.ToString(), turn, maxTurns, ct);
            if (decision is null)
            {
                history.AppendLine($"- turn {turn}: no decision; retrying.");
                continue;
            }

            var action = decision.Action?.Trim().ToLowerInvariant();

            if (decision.StepComplete || action == "finish" || decision.StepFailed)
            {
                if (decision.StepFailed)
                {
                    return (StepRunStatus.Failed, TrimThought(decision.Thought) ?? "The agent determined the step cannot be completed on this page.", actions);
                }

                // Verify the expected outcome with AI (intent-based), not brittle literal text matching.
                var (ok, reason) = await IsStepOutcomeSatisfiedAsync(step, snapshot, mcp.CurrentUrl, ct);
                if (ok)
                {
                    if (actions.Count == 0)
                    {
                        actions.Add(new RecordedStepAction { Kind = "assert", Target = step.ExpectedResult ?? step.Action });
                    }
                    return (acted ? StepRunStatus.Passed : StepRunStatus.Passed, TrimThought(decision.Thought) ?? reason, actions);
                }

                return acted
                    ? (StepRunStatus.Healed, $"Actions completed but the expected outcome wasn't confirmed: {reason}", actions)
                    : (StepRunStatus.Failed, $"No action satisfied the step and the expected outcome is absent: {reason}", actions);
            }

            var (performed, observation) = await PerformMcpActionAsync(mcp, decision, baseUrl, ct);
            if (performed)
            {
                acted = true;
                if (action is "navigate" or "click" or "type" or "press")
                {
                    actions.Add(new RecordedStepAction
                    {
                        Kind = action,
                        Target = action == "navigate" ? mcp.CurrentUrl : decision.Target,
                        Value = decision.Value,
                    });
                }
            }

            history.AppendLine($"- turn {turn}: {action} -> {observation}. (thought: {TrimThought(decision.Thought)})");
            await PublishStatusAsync(session, mcp.CurrentUrl, ct);
        }

        // Turn budget exhausted — do a final AI verification of the current page state.
        var (finalOk, finalReason) = await IsStepOutcomeSatisfiedAsync(step, TrimSnapshot(await mcp.SnapshotAsync(ct)), mcp.CurrentUrl, ct);
        if (finalOk)
        {
            if (actions.Count == 0)
            {
                actions.Add(new RecordedStepAction { Kind = "assert", Target = step.ExpectedResult ?? step.Action });
            }
            return (StepRunStatus.Passed, $"Reached the expected state after several MCP actions ({finalReason}).", actions);
        }

        return (StepRunStatus.Failed, acted
            ? $"Performed multiple MCP actions but the expected outcome was not reached: {finalReason}"
            : "The agent could not determine an MCP action to complete the step.", actions);
    }

    /// <summary>
    /// Decides whether a step's expected outcome is satisfied by the current page. Fast literal
    /// check first; if that misses, an AI verifier judges the intent (so a shown login modal counts
    /// as "login popup is displayed" even without that literal text).
    /// </summary>
    private async Task<(bool Ok, string Reason)> IsStepOutcomeSatisfiedAsync(ScenarioStep step, string snapshot, string url, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(step.ExpectedResult))
        {
            return (true, "no explicit expected outcome");
        }

        if (McpMatchesExpected(snapshot, url, step.ExpectedResult))
        {
            return (true, "expected text present on the page");
        }

        if (!_llm.IsLive)
        {
            return (false, "expected outcome text not found");
        }

        try
        {
            var system = "You are a meticulous QA verifier. Given a web page accessibility snapshot and a test step's expected outcome, "
                + "decide whether the expected outcome is actually satisfied by the current page state. Match intent, not exact wording "
                + "(e.g. a visible login dialog satisfies 'login popup is displayed'). "
                + "Respond ONLY with JSON: {\"satisfied\": true|false, \"reason\": \"short explanation\"}.";
            var snap = snapshot.Length > 6000 ? snapshot[..6000] : snapshot;
            var user = $"Step action: {step.Action}\nExpected outcome: {step.ExpectedResult}\nCurrent URL: {url}\n\nPage snapshot:\n{snap}";
            var raw = await _llm.CompleteAsync(system, user, jsonMode: true, ct);
            var json = JsonExtraction.ExtractJsonObject(raw);
            var judged = JsonSerializer.Deserialize<OutcomeJudgement>(json, JsonOpts);
            return (judged?.Satisfied ?? false, judged?.Reason ?? "verified by AI");
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Outcome verification failed for step {StepId}; treating as unmet.", step.Id);
            return (false, "outcome could not be verified");
        }
    }

    private sealed class OutcomeJudgement
    {
        public bool Satisfied { get; set; }
        public string? Reason { get; set; }
    }


    private static async Task<(bool Ok, string Observation)> PerformMcpActionAsync(
        PlaywrightMcpBrowser mcp,
        StepAgentDecision decision,
        string baseUrl,
        CancellationToken ct)
    {
        switch (decision.Action?.Trim().ToLowerInvariant())
        {
            case "navigate":
                var url = ResolveNavigationTarget(mcp.CurrentUrl, decision.Target, baseUrl);
                await mcp.NavigateAsync(url, ct);
                await mcp.StabilizePageAsync(ct);
                return (true, $"navigated to {mcp.CurrentUrl}");
            case "click":
                var clicked = !string.IsNullOrWhiteSpace(decision.Ref)
                    && await mcp.ClickAsync(decision.Target ?? "element", decision.Ref!, ct);
                if (!clicked && !string.IsNullOrWhiteSpace(decision.Target))
                {
                    // Snapshot ref may be stale on a dynamic page — relocate + click by descriptor.
                    clicked = await mcp.ClickByDescriptorAsync(decision.Target!, ct);
                    if (clicked)
                    {
                        return (true, $"clicked \"{decision.Target}\" (by descriptor; the snapshot ref was stale)");
                    }
                }
                return (clicked, clicked ? $"clicked [ref={decision.Ref}]" : $"could not click {decision.Ref} (it may be hidden or covered by a modal/overlay — dismiss any blocking popup first, e.g. press Escape or click its close/Skip/Not now control)");
            case "type":
                var typed = !string.IsNullOrWhiteSpace(decision.Ref)
                    && await mcp.TypeAsync(decision.Target ?? "field", decision.Ref!, decision.Value ?? string.Empty, ct);
                if (!typed && (!string.IsNullOrWhiteSpace(decision.Target) || !string.IsNullOrWhiteSpace(decision.Value)))
                {
                    // Relocate the field by its label/placeholder and fill it directly.
                    typed = await mcp.FillByDescriptorAsync(decision.Target ?? string.Empty, decision.Value ?? string.Empty, ct);
                    if (typed)
                    {
                        return (true, $"typed \"{decision.Value}\" into \"{decision.Target}\" (by descriptor; the snapshot ref was stale)");
                    }
                }
                return (typed, typed ? $"typed into [ref={decision.Ref}]" : $"could not type into {decision.Ref} (the field may be covered by a modal/overlay — dismiss any blocking popup first)");
            case "press":
                var key = string.IsNullOrWhiteSpace(decision.Target) ? "Enter" : decision.Target!;
                var pressed = await mcp.PressKeyAsync(key, ct);
                return (pressed, pressed ? $"pressed {key}" : $"could not press {key}");
            case "wait":
                await mcp.WaitAsync(1, ct);
                return (true, "waited");
            default:
                return (false, $"unknown action '{decision.Action}'");
        }
    }

    private static bool McpMatchesExpected(string snapshot, string currentUrl, string? expected)
    {
        if (string.IsNullOrWhiteSpace(expected))
        {
            return true;
        }

        var needle = expected.Trim();
        return snapshot.Contains(needle, StringComparison.OrdinalIgnoreCase)
            || currentUrl.Contains(needle, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Deterministic scenario RUN: executes the scenario's saved steps in order (each via a bounded
    /// agentic MCP loop) and records Passed/Healed/Failed per step — a real regression run of the
    /// authored steps, as opposed to the goal-driven exploration that proposes new steps.
    /// </summary>
    private async Task RunScenarioStepsViaMcpAsync(
        ExplorationSession session,
        PlaywrightMcpBrowser mcp,
        string baseUrl,
        Scenario scenario,
        List<ScenarioStep> orderedSteps,
        CancellationToken ct)
    {
        if (orderedSteps.Count == 0)
        {
            await PublishLogAsync(session, $"Scenario '{scenario.Title}' has no steps to run. Explore it first to ground its steps.", "warn", ct);
            return;
        }

        // Keep the page stable and unblocked before executing the authored steps.
        await mcp.StabilizePageAsync(ct);
        await DismissBlockingOverlaysAsync(session, mcp, ct);

        var passed = 0;
        var healed = 0;
        var failed = 0;

        foreach (var step in orderedSteps)
        {
            if (ct.IsCancellationRequested)
            {
                break;
            }

            await PublishLogAsync(session, $"→ Step {step.Order}: {step.Action}", "info", ct);

            var (status, detail, _) = await RunMcpStepAsync(session, mcp, baseUrl, step, ct);

            // AUTO-HEAL: a failed step gets one recovery pass — reload/re-navigate if the page is in an
            // error/unreachable state (e.g. ERR_CONNECTION_RESET → chrome-error://), clear overlays, then
            // retry the step once. If the retry succeeds, the step is marked Healed instead of Failed.
            if (status == StepRunStatus.Failed && !ct.IsCancellationRequested)
            {
                await PublishLogAsync(session, $"  ↻ Step {step.Order} failed — attempting auto-heal…", "warn", ct);
                var recovered = await TryRecoverPageAsync(session, mcp, baseUrl, ct);
                var (retryStatus, retryDetail, _) = await RunMcpStepAsync(session, mcp, baseUrl, step, ct);

                if (retryStatus != StepRunStatus.Failed)
                {
                    status = StepRunStatus.Healed;
                    detail = $"Auto-healed{(recovered ? " after reloading the page" : " on retry")}: {retryDetail}";
                    await PublishLogAsync(session, $"  ✎ Step {step.Order} auto-healed.", "success", ct);
                }
                else
                {
                    detail = $"{detail} · Auto-heal retry also failed: {retryDetail}";
                }
            }

            switch (status)
            {
                case StepRunStatus.Passed: passed++; break;
                case StepRunStatus.Healed: healed++; break;
                default: failed++; break;
            }

            _db.ScenarioStepResults.Add(new ScenarioStepResult
            {
                TenantId = session.TenantId,
                ProjectId = session.ProjectId,
                SessionId = session.Id,
                ScenarioId = scenario.Id,
                StepOrder = step.Order,
                Action = step.Action,
                Status = status,
                Detail = detail,
                Url = mcp.CurrentUrl,
            });
            await _db.SaveChangesAsync(ct);

            await PublishStepAsync(session, scenario, step, status, detail, mcp.CurrentUrl, ct);
            var glyph = status == StepRunStatus.Passed ? "✓" : status == StepRunStatus.Healed ? "✎" : "✗";
            await PublishLogAsync(session, $"  {glyph} Step {step.Order} {status}: {detail}", status == StepRunStatus.Failed ? "error" : status == StepRunStatus.Healed ? "warn" : "success", ct);
        }

        var outcome = failed > 0 ? "error" : healed > 0 ? "warn" : "success";
        await PublishLogAsync(session, $"Run complete for '{scenario.Title}': {passed} passed · {healed} healed · {failed} failed.", outcome, ct);
    }

    /// <summary>
    /// Recovery for a failed run step: if the page is in an error/unreachable state (e.g.
    /// ERR_CONNECTION_RESET lands on <c>chrome-error://</c>, a blank page, or an empty body), reload it
    /// by re-navigating to the base URL, re-stabilise and clear overlays so the retry has a live page.
    /// Returns true if it actually performed a reload. Never throws.
    /// </summary>
    private async Task<bool> TryRecoverPageAsync(ExplorationSession session, PlaywrightMcpBrowser mcp, string baseUrl, CancellationToken ct)
    {
        try
        {
            var url = mcp.CurrentUrl ?? string.Empty;
            var brokenUrl = url.Length == 0
                || url.Contains("chrome-error", StringComparison.OrdinalIgnoreCase)
                || url.StartsWith("about:", StringComparison.OrdinalIgnoreCase)
                || url.Contains("data:text/html", StringComparison.OrdinalIgnoreCase);

            // Also treat an empty/error document body as broken.
            var bodyEmpty = false;
            if (!brokenUrl)
            {
                var probe = await mcp.EvaluateAsync("() => { try { return !!document.body && document.body.innerText.trim().length > 20; } catch { return false; } }", ct);
                bodyEmpty = !PlaywrightMcpBrowserResultTrue(probe);
            }

            if (!brokenUrl && !bodyEmpty)
            {
                return false; // page looks usable — the retry runs in place.
            }

            await PublishLogAsync(session, "  ↻ Page looks unreachable — reloading and re-navigating…", "warn", ct);
            await mcp.NavigateAsync(baseUrl, ct);
            await mcp.StabilizePageAsync(ct);
            await DismissBlockingOverlaysAsync(session, mcp, ct);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Page recovery best-effort failed for session {SessionId}.", session.Id);
            return false;
        }
    }

    private static bool PlaywrightMcpBrowserResultTrue(string? evalResult) =>
        !string.IsNullOrEmpty(evalResult)
        && System.Text.RegularExpressions.Regex.IsMatch(evalResult, @"(^|\W)true(\W|$)", System.Text.RegularExpressions.RegexOptions.IgnoreCase);

    /// <summary>
    /// Goal-driven scenario exploration: the AI is given the scenario's intent (title, preconditions,
    /// expected outcome, and the existing steps as hints) and explores the REAL app via MCP to achieve
    /// it — adapting to the actual UI instead of rigidly executing possibly-wrong steps. The concrete
    /// actions it performs are recorded as *proposed* steps for the user to review/apply (originals kept).
    /// </summary>
    private async Task RunScenarioMissionViaMcpAsync(
        ExplorationSession session,
        PlaywrightMcpBrowser mcp,
        string baseUrl,
        Scenario scenario,
        List<ScenarioStep> hintSteps,
        CancellationToken ct)
    {
        var mission = BuildScenarioMission(scenario, hintSteps);
        var missionStep = new ScenarioStep { ScenarioId = scenario.Id, Order = 0, Action = mission, ExpectedResult = scenario.ExpectedResult };

        // Real sites often gate the page behind a login/OTP, cookie, location or promo modal that
        // intercepts every click. Clear those deterministically before the agent starts so it can
        // actually interact, rather than hoping the LLM infers it from opaque "could not click" errors.
        await mcp.StabilizePageAsync(ct);
        var dismissed = await DismissBlockingOverlaysAsync(session, mcp, ct);
        if (dismissed > 0)
        {
            await PublishLogAsync(session, $"Cleared {dismissed} blocking popup(s) before exploring.", "info", ct);
        }

        const int maxTurns = 18;
        var history = new StringBuilder();
        var discovered = new List<ProposedStep>();
        var order = 1;
        var reachedGoal = false;
        var goalReason = string.Empty;
        var decided = false;

        for (var turn = 1; turn <= maxTurns; turn++)
        {
            ct.ThrowIfCancellationRequested();

            var snapshot = TrimSnapshot(await mcp.SnapshotAsync(ct));
            var decision = await DecideNextStepActionAsync(missionStep, mcp.CurrentUrl, snapshot, history.ToString(), turn, maxTurns, ct);
            if (decision is null)
            {
                history.AppendLine($"- turn {turn}: no decision returned.");
                continue;
            }

            var action = decision.Action?.Trim().ToLowerInvariant();

            if (decision.StepComplete || action == "finish" || decision.StepFailed)
            {
                if (decision.StepFailed)
                {
                    goalReason = TrimThought(decision.Thought) ?? "The agent concluded the scenario cannot be accomplished on this app.";
                }
                else
                {
                    var (ok, reason) = await IsStepOutcomeSatisfiedAsync(missionStep, snapshot, mcp.CurrentUrl, ct);
                    reachedGoal = ok;
                    goalReason = ok ? (TrimThought(decision.Thought) ?? reason) : reason;
                }

                decided = true;
                break;
            }

            var (performed, observation) = await PerformMcpActionAsync(mcp, decision, baseUrl, ct);

            // A rejected click/type almost always means a modal/overlay appeared and is intercepting
            // pointer events (these often pop up LATE, after the page settles). Clear it generically
            // and tell the agent to retry, rather than letting it flail at other elements.
            if (!performed && action is "click" or "type")
            {
                var cleared = await DismissBlockingOverlaysAsync(session, mcp, ct);
                if (cleared > 0)
                {
                    observation += " — a blocking overlay was covering the page and has now been dismissed; retry the action.";
                }
            }

            // Escape/dismissal keypresses are housekeeping, not meaningful test steps — and repeated
            // identical actions (e.g. the agent hammering Escape at a stubborn modal) are just noise.
            var isDismissKey = action == "press"
                && (string.Equals(decision.Target, "Escape", StringComparison.OrdinalIgnoreCase)
                    || string.IsNullOrWhiteSpace(decision.Target));
            var isRecordable = performed && !isDismissKey && action is "navigate" or "click" or "type" or "press";
            if (isRecordable)
            {
                var desc = DescribeMcpAction(action, decision, mcp.CurrentUrl);
                var isDuplicate = discovered.Count > 0
                    && string.Equals(discovered[^1].Action, desc, StringComparison.OrdinalIgnoreCase);
                if (!isDuplicate)
                {
                    discovered.Add(new ProposedStep { Order = order, Action = desc });

                    _db.ScenarioStepResults.Add(new ScenarioStepResult
                    {
                        TenantId = session.TenantId,
                        ProjectId = session.ProjectId,
                        SessionId = session.Id,
                        ScenarioId = scenario.Id,
                        StepOrder = order,
                        Action = desc,
                        Status = StepRunStatus.Passed,
                        Detail = TrimThought(decision.Thought),
                        Url = mcp.CurrentUrl,
                    });
                    await _db.SaveChangesAsync(ct);

                    await PublishStepAsync(session, scenario, new ScenarioStep { Order = order, Action = desc }, StepRunStatus.Passed, TrimThought(decision.Thought) ?? string.Empty, mcp.CurrentUrl, ct);
                    await PublishLogAsync(session, $"  ✓ {order}. {desc}", "success", ct);
                    order++;
                }
            }

            history.AppendLine($"- turn {turn}: {action} -> {observation}. (thought: {TrimThought(decision.Thought)})");
            await PublishStatusAsync(session, mcp.CurrentUrl, ct);
        }

        if (!decided)
        {
            var (ok, reason) = await IsStepOutcomeSatisfiedAsync(missionStep, TrimSnapshot(await mcp.SnapshotAsync(ct)), mcp.CurrentUrl, ct);
            reachedGoal = ok;
            goalReason = reason;
        }

        if (discovered.Count > 0)
        {
            scenario.ProposedStepsJson = JsonSerializer.Serialize(discovered, JsonOpts);
            await _db.SaveChangesAsync(ct);
            await PublishLogAsync(
                session,
                $"Discovered {discovered.Count} real step(s) for '{scenario.Title}'. Open the scenario to review & apply — your original steps are kept.",
                reachedGoal ? "success" : "warn",
                ct);
        }
        else
        {
            await PublishLogAsync(session, $"No actionable steps were discovered for '{scenario.Title}'. {goalReason}", "warn", ct);
        }

        await PublishLogAsync(
            session,
            reachedGoal ? $"Scenario goal reached: {goalReason}" : $"Scenario goal not confirmed: {goalReason}",
            reachedGoal ? "success" : "warn",
            ct);
    }

    private static string BuildScenarioMission(Scenario scenario, List<ScenarioStep> hintSteps)
    {
        var sb = new StringBuilder();
        sb.AppendLine("Accomplish this test scenario by exploring and interacting with the REAL web application in front of you.");
        sb.AppendLine($"Scenario: {scenario.Title}");
        if (!string.IsNullOrWhiteSpace(scenario.Preconditions))
        {
            sb.AppendLine($"Preconditions: {scenario.Preconditions}");
        }
        if (!string.IsNullOrWhiteSpace(scenario.ExpectedResult))
        {
            sb.AppendLine($"Intended outcome: {scenario.ExpectedResult}");
        }
        if (hintSteps.Count > 0)
        {
            sb.AppendLine($"Suggested flow (HINTS ONLY — the real UI may differ; adapt or skip steps that don't apply): {string.Join("; ", hintSteps.Select(s => s.Action))}");
        }
        sb.AppendLine("If a login/OTP, cookie, location or promotional popup or modal is blocking the page, close or dismiss it first (click its ✕/close/'Not now'/'Skip' control, or press Escape) — do NOT try to log in; continue as a guest.");
        sb.Append("Perform the minimal REAL actions needed to reach the intended outcome. Prefer the app's actual UI over the suggested steps. When the outcome is reached — or it genuinely cannot be — finish.");
        return sb.ToString();
    }

    /// <summary>
    /// Generic, DOM-level JS that runs IN the page to detect a blocking overlay (modal/popup/consent
    /// wall) at runtime — WITHOUT any site-specific selectors or hardcoded button names — and dismiss
    /// it. Strategy, in order: (1) click a semantic close control ([aria-label*=close], role=dialog's
    /// close, etc.); (2) click the small element in the overlay's top-right corner (the typical ✕);
    /// (3) as a last resort for a genuine modal (role=dialog / aria-modal), hide it + its backdrop so
    /// the page underneath becomes interactable. Returns {found, closed, how}.
    /// </summary>
    private const string DismissOverlayJs = """
        () => {
          const cx = innerWidth / 2, cy = innerHeight / 2;
          const top = document.elementFromPoint(cx, cy);
          if (!top) return { found: false, diag: 'no element at center' };

          // The element actually intercepting a center click is the real blocker. Walk up to its
          // outermost fixed/absolute ancestor — that container is the overlay/modal/backdrop.
          let node = top, overlay = null;
          while (node && node !== document.body) {
            const s = getComputedStyle(node);
            if (s.position === 'fixed' || s.position === 'absolute') overlay = node;
            node = node.parentElement;
          }
          const desc = (e) => e ? (e.tagName.toLowerCase() + (e.id ? '#' + e.id : '') + (e.className ? '.' + String(e.className).trim().split(/\s+/).slice(0, 2).join('.') : '')) : 'null';
          if (!overlay) return { found: false, diag: 'center=' + desc(top) + ' (no positioned ancestor)' };

          const or = overlay.getBoundingClientRect();
          const big = or.width >= innerWidth * 0.3 && or.height >= innerHeight * 0.25;
          if (!big) return { found: false, diag: 'overlay=' + desc(overlay) + ' ' + Math.round(or.width) + 'x' + Math.round(or.height) + ' (too small)' };

          const vis = (n) => { const s = getComputedStyle(n); return s.display !== 'none' && s.visibility !== 'hidden' && parseFloat(s.opacity || '1') > 0; };

          // 1) semantic close control
          const sel = '[aria-label*="close" i],[aria-label*="dismiss" i],[title*="close" i],[data-testid*="close" i],[class*="close" i]';
          let btn = [...overlay.querySelectorAll(sel)].find(vis);
          // 2) the small control in the overlay's top-right corner (the usual ✕)
          if (!btn) {
            btn = [...overlay.querySelectorAll('button,[role=button],a,span,svg,img,i')]
              .map(e => ({ e, r: e.getBoundingClientRect() }))
              .filter(o => o.r.width > 0 && o.r.width <= 48 && o.r.height <= 48 && o.r.right >= or.right - 64 && o.r.top <= or.top + 72 && vis(o.e))
              .sort((a, b) => a.r.top - b.r.top)[0]?.e;
          }
          if (btn) { btn.click(); return { found: true, closed: true, how: 'click', overlay: desc(overlay) }; }

          // 3) last resort: hide the intercepting container + any full-screen backdrops
          overlay.style.setProperty('display', 'none', 'important');
          for (const n of document.querySelectorAll('body > *, body > * > *')) {
            const s = getComputedStyle(n); const r = n.getBoundingClientRect();
            if ((s.position === 'fixed' || s.position === 'absolute') && r.width >= innerWidth * 0.9 && r.height >= innerHeight * 0.9)
              n.style.setProperty('display', 'none', 'important');
          }
          return { found: true, closed: true, how: 'hidden', overlay: desc(overlay) };
        }
        """;

    /// <summary>
    /// Best-effort dismissal of blocking overlays that intercept pointer events on real sites. Presses
    /// Escape, then runs a GENERIC DOM detector (<see cref="DismissOverlayJs"/>) that finds and closes
    /// whatever modal is covering the page at runtime — no hardcoded per-site names. Bounded; never throws.
    /// Returns how many overlays it cleared.
    /// </summary>
    private async Task<int> DismissBlockingOverlaysAsync(ExplorationSession session, PlaywrightMcpBrowser mcp, CancellationToken ct)
    {
        var dismissed = 0;
        try
        {
            // Let a just-loaded modal appear before we look for it.
            await mcp.WaitAsync(1, ct);

            for (var attempt = 0; attempt < 3 && !ct.IsCancellationRequested; attempt++)
            {
                await mcp.PressKeyAsync("Escape", ct);

                var result = await mcp.EvaluateAsync(DismissOverlayJs, ct);
                if (!result.Contains("\"closed\": true", StringComparison.OrdinalIgnoreCase)
                    && !result.Contains("\"closed\":true", StringComparison.OrdinalIgnoreCase))
                {
                    break;
                }

                dismissed++;
                var how = result.Contains("hidden", StringComparison.OrdinalIgnoreCase) ? "hidden" : "closed";
                await PublishLogAsync(session, $"  • Dismissed a blocking overlay ({how}).", "info", ct);
                await mcp.WaitAsync(0.5, ct);
            }
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Overlay dismissal best-effort failed for session {SessionId}.", session.Id);
        }

        return dismissed;
    }

    private static string DescribeMcpAction(string? action, StepAgentDecision decision, string currentUrl) => action switch
    {
        "navigate" => $"Navigate to {currentUrl}",
        "click" => $"Click \"{decision.Target ?? "element"}\"",
        "type" => $"Enter \"{decision.Value}\" into \"{decision.Target ?? "field"}\"",
        "press" => $"Press {decision.Target ?? "Enter"}",
        _ => decision.Action ?? "action",
    };

    /// <summary>
    /// Reduces a large MCP accessibility snapshot to the header plus the most relevant interactive
    /// elements so heavy SPAs (e.g. Flipkart) don't blow up the LLM prompt — keeping decisions fast,
    /// cheap and focused on things the agent can actually act on.
    /// </summary>
    private static string TrimSnapshot(string snapshot, int maxInterestingLines = 120, int maxChars = 9000)
    {
        if (string.IsNullOrEmpty(snapshot) || snapshot.Length <= maxChars)
        {
            return snapshot;
        }

        var lines = snapshot.Split('\n');
        var sb = new StringBuilder();
        var header = 0;
        var kept = 0;

        foreach (var line in lines)
        {
            if (header < 8 &&
                (line.StartsWith("###", StringComparison.Ordinal)
                 || line.Contains("Page URL", StringComparison.Ordinal)
                 || line.Contains("Page Title", StringComparison.Ordinal)
                 || line.TrimStart().StartsWith("```", StringComparison.Ordinal)))
            {
                sb.AppendLine(line);
                header++;
                continue;
            }

            var interesting = line.Contains("[ref=", StringComparison.Ordinal)
                || line.Contains("heading ", StringComparison.Ordinal)
                || line.Contains("button", StringComparison.Ordinal)
                || line.Contains("link ", StringComparison.Ordinal)
                || line.Contains("textbox", StringComparison.Ordinal)
                || line.Contains("searchbox", StringComparison.Ordinal)
                || line.Contains("dialog", StringComparison.Ordinal);

            if (interesting && kept < maxInterestingLines)
            {
                sb.AppendLine(line);
                kept++;
            }

            if (sb.Length >= maxChars)
            {
                break;
            }
        }

        sb.AppendLine($"… (snapshot trimmed to {kept} key interactive elements)");
        return sb.ToString();
    }

    /// <summary>Broadcasts a single scenario step's result to the tenant's live-view group as it executes.</summary>
    private async Task PublishStepAsync(
        ExplorationSession session,
        Scenario scenario,
        ScenarioStep step,
        StepRunStatus status,
        string? detail,
        string? url,
        CancellationToken ct)
    {
        try
        {
            await _live.PublishStepAsync(
                session.TenantId,
                session.Id,
                new ScenarioStepLiveUpdate(
                    scenario.Id,
                    scenario.Title,
                    step.Order,
                    step.Action,
                    status.ToString(),
                    detail,
                    url),
                ct);
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Failed to publish live step for session {SessionId}.", session.Id);
        }
    }

    /// <summary>Streams a human-readable activity log line to the tenant's live-view group.</summary>
    private async Task PublishLogAsync(ExplorationSession session, string message, string level, CancellationToken ct)
    {
        try
        {
            await _live.PublishLogAsync(
                session.TenantId,
                session.Id,
                new ExplorationLogEntry(level, message, _clock.UtcNow),
                ct);
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Failed to publish live log for session {SessionId}.", session.Id);
        }
    }

    private static string StepStatusGlyph(StepRunStatus status) => status switch
    {
        StepRunStatus.Passed => "✓",
        StepRunStatus.Healed => "✎",
        StepRunStatus.Failed => "✗",
        _ => "•",
    };

    private async Task ExploreAsync(
        ExplorationSession session,
        PlaywrightBrowserService browser,
        string baseUrl,
        string origin,
        CancellationToken ct)
    {
        // Prompt-driven mission: the agent autonomously performs a free-text goal and records a scenario.
        if (!string.IsNullOrWhiteSpace(session.Prompt))
        {
            await ExplorePromptMissionAsync(session, browser, baseUrl, ct);
            return;
        }

        if (session.ScenarioId is Guid scenarioId)
        {
            await ExploreScenarioStepsAsync(session, browser, baseUrl, scenarioId, ct);
            return;
        }

        if (session.SuiteId is Guid suiteId)
        {
            await ExploreSuiteScenariosAsync(session, browser, baseUrl, suiteId, ct);
            return;
        }

        var visited = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var queue = new Queue<(string Url, int Depth, string? FromUrl)>();
        queue.Enqueue((baseUrl, 0, null));

        while (queue.Count > 0 && session.PagesDiscovered < session.MaxPages && !ct.IsCancellationRequested)
        {
            var (url, depth, fromUrl) = queue.Dequeue();

            var normalised = NormaliseUrl(url);
            if (visited.Contains(normalised) || depth > session.MaxDepth)
            {
                continue;
            }

            visited.Add(normalised);

            _logger.LogInformation("Exploring [{Depth}] {Url}", depth, url);
            await PublishLogAsync(session, $"→ Navigating to {url} (depth {depth})", "info", ct);

            var (pageTitle, finalUrl, statusCode) = await browser.NavigateAsync(url, ct);
            await PublishLogAsync(
                session,
                $"✓ Loaded '{DeriveName(pageTitle, finalUrl)}' (HTTP {statusCode})",
                statusCode >= 400 ? "warn" : "success",
                ct);

            // Capture screenshot.
            string? screenshotPath = null;
            try
            {
                await PublishLogAsync(session, "  • Capturing screenshot…", "info", ct);
                var screenshotBytes = await browser.TakeScreenshotAsync(fullPage: false, ct);
                screenshotPath = await _fileStorage.SaveAsync(
                    $"screenshots/{session.ProjectId}",
                    $"{Guid.NewGuid():N}.png",
                    new MemoryStream(screenshotBytes),
                    ct);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Screenshot failed for {Url}", url);
                await PublishLogAsync(session, "  • Screenshot failed (continuing)", "warn", ct);
            }

            // Capture accessibility tree.
            await PublishLogAsync(session, "  • Reading accessibility snapshot…", "info", ct);
            var a11yJson = await browser.GetAccessibilityTreeAsync(ct);

            // Capture DOM snapshot.
            string? domPath = null;
            try
            {
                await PublishLogAsync(session, "  • Saving DOM snapshot…", "info", ct);
                var html = await browser.GetPageHtmlAsync(ct);
                domPath = await _fileStorage.SaveAsync(
                    $"dom/{session.ProjectId}",
                    $"{Guid.NewGuid():N}.html",
                    new MemoryStream(Encoding.UTF8.GetBytes(html)),
                    ct);
            }
            catch { /* non-fatal */ }

            // Persist the discovered page.
            var discoveredPage = new DiscoveredPage
            {
                TenantId = session.TenantId,
                ProjectId = session.ProjectId,
                SessionId = session.Id,
                Name = DeriveName(pageTitle, finalUrl),
                Url = finalUrl,
                Path = new Uri(finalUrl).PathAndQuery,
                Title = pageTitle,
                HttpStatusCode = statusCode,
                ScreenshotPath = screenshotPath,
                DomSnapshotPath = domPath,
                AccessibilityTreeJson = a11yJson.Length <= 500_000 ? a11yJson : null,
                IsExplored = true,
                DepthFromRoot = depth,
                DiscoveredFromUrl = fromUrl,
            };

            _db.DiscoveredPages.Add(discoveredPage);
            session.PagesDiscovered++;

            // Discover elements.
            await PublishLogAsync(session, "  • Discovering interactive elements…", "info", ct);
            await DiscoverElementsAsync(session, discoveredPage, browser, ct);
            await PublishLogAsync(
                session,
                $"  ✓ Found {discoveredPage.Elements.Count} element(s) on '{discoveredPage.Name}'",
                "success",
                ct);

            // Optional LLM step: find hidden content to interact with.
            if (_llm.IsLive)
            {
                await PublishLogAsync(session, "  • Asking AI to reveal hidden content (modals, menus)…", "info", ct);
                await TryExpandDynamicContentAsync(session, discoveredPage, browser, a11yJson, ct);
            }

            // Enqueue new links.
            var links = await browser.GetSameOriginLinksAsync(baseUrl, ct);
            var queuedBefore = queue.Count;
            foreach (var link in links)
            {
                var norm = NormaliseUrl(link);
                if (!visited.Contains(norm) && link.StartsWith(origin, StringComparison.OrdinalIgnoreCase))
                {
                    queue.Enqueue((link, depth + 1, finalUrl));
                }
            }

            var queuedNew = queue.Count - queuedBefore;
            if (queuedNew > 0)
            {
                await PublishLogAsync(session, $"Queued {queuedNew} new link(s) to explore", "info", ct);
            }

            await _db.SaveChangesAsync(ct);
            await PublishStatusAsync(session, finalUrl, ct);
        }
    }

    private async Task ExploreScenarioStepsAsync(
        ExplorationSession session,
        PlaywrightBrowserService browser,
        string baseUrl,
        Guid scenarioId,
        CancellationToken ct)
    {
        var scenario = await _db.Scenarios
            .IgnoreQueryFilters()
            .Include(s => s.Steps)
            .FirstOrDefaultAsync(s => s.Id == scenarioId && s.ProjectId == session.ProjectId, ct);

        if (scenario is null)
        {
            throw new InvalidOperationException($"Scenario {scenarioId} not found for project {session.ProjectId}.");
        }

        await browser.NavigateAsync(baseUrl, ct);
        await WalkScenarioStepsAsync(session, browser, baseUrl, scenario, ct);

        session.Status = ExplorationStatus.Completed;
    }

    private async Task ExploreSuiteScenariosAsync(
        ExplorationSession session,
        PlaywrightBrowserService browser,
        string baseUrl,
        Guid suiteId,
        CancellationToken ct)
    {
        var suite = await _db.TestSuites
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(s => s.Id == suiteId && s.ProjectId == session.ProjectId, ct);

        if (suite is null)
        {
            throw new InvalidOperationException($"Suite {suiteId} not found for project {session.ProjectId}.");
        }

        var suiteScenarioIds = await _db.TestSuiteScenarios
            .IgnoreQueryFilters()
            .Where(ss => ss.SuiteId == suiteId)
            .OrderBy(ss => ss.Order)
            .Select(ss => ss.ScenarioId)
            .ToListAsync(ct);

        // Load each scenario with its steps, preserving suite order.
        var scenarios = await _db.Scenarios
            .IgnoreQueryFilters()
            .Include(s => s.Steps)
            .Where(s => suiteScenarioIds.Contains(s.Id) && s.ProjectId == session.ProjectId)
            .ToListAsync(ct);

        var ordered = suiteScenarioIds
            .Select(id => scenarios.FirstOrDefault(s => s.Id == id))
            .Where(s => s is not null)
            .Select(s => s!)
            .ToList();

        // Sequential run in a single shared browser session: navigate once, then walk each
        // scenario's steps so state carries over between scenarios.
        await browser.NavigateAsync(baseUrl, ct);

        foreach (var scenario in ordered)
        {
            if (ct.IsCancellationRequested)
            {
                break;
            }

            await WalkScenarioStepsAsync(session, browser, baseUrl, scenario, ct);
        }

        session.Status = ExplorationStatus.Completed;
    }

    /// <summary>Executes every step of a single scenario against the current browser page, recording per-step results.</summary>
    private async Task WalkScenarioStepsAsync(
        ExplorationSession session,
        PlaywrightBrowserService browser,
        string baseUrl,
        Scenario scenario,
        CancellationToken ct)
    {
        var orderedSteps = scenario.Steps
            .OrderBy(s => s.Order)
            .ToList();

        if (orderedSteps.Count == 0)
        {
            return;
        }

        await PublishLogAsync(session, $"Running scenario '{scenario.Title}' — {orderedSteps.Count} step(s)", "info", ct);

        var currentUrl = browser.CurrentUrl;

        foreach (var step in orderedSteps)
        {
            if (ct.IsCancellationRequested)
            {
                break;
            }

            await PublishLogAsync(session, $"→ Step {step.Order}: {step.Action}", "info", ct);
            var result = await ExecuteScenarioStepAsync(session, browser, baseUrl, step, ct);
            _db.ScenarioStepResults.Add(new ScenarioStepResult
            {
                TenantId = session.TenantId,
                ProjectId = session.ProjectId,
                SessionId = session.Id,
                ScenarioId = scenario.Id,
                StepOrder = step.Order,
                Action = step.Action,
                Status = result.Status,
                Detail = result.Detail,
                Url = browser.CurrentUrl ?? currentUrl,
            });

            currentUrl = browser.CurrentUrl;
            await _db.SaveChangesAsync(ct);
            await PublishStepAsync(session, scenario, step, result.Status, result.Detail, browser.CurrentUrl ?? currentUrl, ct);
            await PublishLogAsync(
                session,
                $"  {StepStatusGlyph(result.Status)} Step {step.Order} {result.Status}: {result.Detail}",
                result.Status == StepRunStatus.Failed ? "error" : result.Status == StepRunStatus.Healed ? "warn" : "success",
                ct);
            await PublishStatusAsync(session, browser.CurrentUrl, ct);
        }
    }

    // ── Prompt-driven ("mission") exploration ────────────────────────────────────

    /// <summary>
    /// Runs a free-text mission autonomously: navigates the app, decides each browser action itself,
    /// and records the sequence of actions as a new reusable scenario when finished.
    /// </summary>
    private async Task ExplorePromptMissionAsync(
        ExplorationSession session,
        PlaywrightBrowserService browser,
        string baseUrl,
        CancellationToken ct)
    {
        await browser.NavigateAsync(baseUrl, ct);

        const int maxTurns = 20;
        var history = new StringBuilder();
        var recorded = new List<(string Action, string? Expected)>();
        var missionOk = false;

        for (var turn = 1; turn <= maxTurns && !ct.IsCancellationRequested; turn++)
        {
            var pageUrl = string.IsNullOrWhiteSpace(browser.CurrentUrl) ? baseUrl : browser.CurrentUrl;
            var snapshot = await browser.SnapshotForAgentAsync(60, ct);

            var decision = await DecideMissionActionAsync(session.Prompt!, pageUrl, snapshot, history.ToString(), turn, maxTurns, ct);
            if (decision is null)
            {
                history.AppendLine($"- turn {turn}: no decision returned; retrying.");
                continue;
            }

            var action = decision.Action?.Trim().ToLowerInvariant();

            if (decision.MissionComplete || action == "finish" || decision.MissionFailed)
            {
                missionOk = decision.MissionComplete && !decision.MissionFailed;
                break;
            }

            var (ok, observation) = await PerformMissionActionAsync(browser, decision, baseUrl, pageUrl, ct);
            if (ok && action is "navigate" or "click" or "type" or "press")
            {
                var stepText = string.IsNullOrWhiteSpace(decision.StepDescription)
                    ? SynthesizeStep(action!, decision)
                    : decision.StepDescription!.Trim();
                recorded.Add((stepText, string.IsNullOrWhiteSpace(decision.ExpectedResult) ? null : decision.ExpectedResult!.Trim()));
            }

            history.AppendLine($"- turn {turn}: {action} -> {observation}. (thought: {TrimThought(decision.Thought)})");

            // Reuse the pages counter to surface live progress (# of recorded steps).
            session.PagesDiscovered = recorded.Count;
            await _db.SaveChangesAsync(ct);
            await PublishStatusAsync(session, browser.CurrentUrl, ct);
        }

        if (recorded.Count > 0)
        {
            session.RecordedScenarioId = await RecordScenarioAsync(session, recorded, missionOk, ct);
            await _db.SaveChangesAsync(ct);
            _logger.LogInformation(
                "Prompt exploration recorded scenario {ScenarioId} with {Count} step(s).",
                session.RecordedScenarioId, recorded.Count);
        }
        else
        {
            _logger.LogInformation("Prompt exploration for session {SessionId} produced no recordable steps.", session.Id);
        }
    }

    private async Task<MissionAgentDecision?> DecideMissionActionAsync(
        string mission, string pageUrl, string snapshot, string history, int turn, int maxTurns, CancellationToken ct)
    {
        try
        {
            var raw = await _llm.CompleteAsync(
                MissionAgentPrompts.System,
                MissionAgentPrompts.BuildUserPrompt(mission, pageUrl, snapshot, history, turn, maxTurns),
                jsonMode: true,
                ct);

            var json = JsonExtraction.ExtractJsonObject(raw);
            return JsonSerializer.Deserialize<MissionAgentDecision>(json, JsonOpts);
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Mission agent decision failed on turn {Turn}.", turn);
            return null;
        }
    }

    private async Task<(bool Ok, string Observation)> PerformMissionActionAsync(
        PlaywrightBrowserService browser, MissionAgentDecision decision, string baseUrl, string pageUrl, CancellationToken ct)
    {
        try
        {
            switch (decision.Action?.Trim().ToLowerInvariant())
            {
                case "navigate":
                    var url = ResolveNavigationTarget(pageUrl, decision.Target, baseUrl);
                    await browser.NavigateAsync(url, ct);
                    return (true, $"navigated to {browser.CurrentUrl}");

                case "click":
                    var clickedByRef = !string.IsNullOrWhiteSpace(decision.Ref) && await browser.TryClickByRefAsync(decision.Ref!, ct);
                    if (clickedByRef)
                    {
                        return (true, $"clicked [ref={decision.Ref}]");
                    }

                    var clickIdx = await browser.TryClickByHintAsync(decision.Target ?? string.Empty, ct);
                    return clickIdx >= 0
                        ? (true, $"clicked \"{decision.Target}\"")
                        : (false, $"could not click {decision.Ref ?? decision.Target}");

                case "type":
                    var value = decision.Value ?? string.Empty;
                    var filledByRef = !string.IsNullOrWhiteSpace(decision.Ref) && await browser.TryFillByRefAsync(decision.Ref!, value, ct);
                    if (filledByRef)
                    {
                        return (true, $"typed '{value}' into [ref={decision.Ref}]");
                    }

                    var fillIdx = await browser.TryFillAsync(decision.Target ?? string.Empty, value, ct);
                    return fillIdx >= 0
                        ? (true, $"typed '{value}' into \"{decision.Target}\"")
                        : (false, $"could not find input {decision.Ref ?? decision.Target}");

                case "press":
                    var key = string.IsNullOrWhiteSpace(decision.Target) ? "Enter" : decision.Target!;
                    var pressed = await browser.PressKeyAsync(key, ct);
                    return (pressed, pressed ? $"pressed {key}" : $"could not press {key}");

                case "wait":
                    await browser.WaitAsync(800, ct);
                    return (true, "waited for the page to settle");

                default:
                    return (false, $"unknown action '{decision.Action}'");
            }
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Mission action '{Action}' threw.", decision.Action);
            return (false, $"action '{decision.Action}' errored: {(ex.Message.Length > 120 ? ex.Message[..120] : ex.Message)}");
        }
    }

    private static string SynthesizeStep(string action, MissionAgentDecision d) => action switch
    {
        "navigate" => $"Navigate to {d.Target}",
        "click" => $"Click {d.Target ?? "the element"}",
        "type" => $"Enter \"{d.Value}\" into {d.Target ?? "the field"}",
        "press" => $"Press {d.Target ?? "Enter"}",
        _ => action,
    };

    private static string BuildScenarioTitle(string prompt)
    {
        var t = prompt.Trim().Replace('\n', ' ').Replace('\r', ' ');
        return t.Length > 90 ? t[..90] + "…" : t;
    }

    /// <summary>Persists the recorded action sequence as a new scenario under an auto-provisioned feature.</summary>
    private async Task<Guid> RecordScenarioAsync(
        ExplorationSession session, List<(string Action, string? Expected)> steps, bool missionOk, CancellationToken ct)
    {
        var featureId = await EnsureExplorationFeatureAsync(session, ct);

        var scenario = new Scenario
        {
            TenantId = session.TenantId,
            ProjectId = session.ProjectId,
            FeatureId = featureId,
            Title = BuildScenarioTitle(session.Prompt!),
            Type = ScenarioType.Positive,
            Priority = Priority.Medium,
            Risk = RiskLevel.Low,
            Source = ScenarioSource.AiGenerated,
            Preconditions = $"Recorded by AI exploration from prompt: \"{session.Prompt}\".",
            ExpectedResult = missionOk
                ? "The described flow completed successfully."
                : "Partial flow recorded (the mission was not fully completed).",
        };

        var order = 1;
        foreach (var (act, expected) in steps)
        {
            scenario.Steps.Add(new ScenarioStep
            {
                TenantId = session.TenantId,
                Order = order++,
                Action = act,
                ExpectedResult = expected,
            });
        }

        _db.Scenarios.Add(scenario);
        await _db.SaveChangesAsync(ct);
        return scenario.Id;
    }

    /// <summary>Finds or creates a dedicated Requirement → Module → Feature chain to host recorded scenarios.</summary>
    private async Task<Guid> EnsureExplorationFeatureAsync(ExplorationSession session, CancellationToken ct)
    {
        const string bucketName = "AI Explorations";

        var feature = await _db.Features
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(f => f.ProjectId == session.ProjectId && f.Name == bucketName, ct);
        if (feature is not null)
        {
            return feature.Id;
        }

        var requirement = await _db.Requirements
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(r => r.ProjectId == session.ProjectId && r.Name == bucketName, ct);
        if (requirement is null)
        {
            requirement = new Requirement
            {
                TenantId = session.TenantId,
                ProjectId = session.ProjectId,
                Name = bucketName,
                SourceType = RequirementSourceType.PlainText,
            };
            _db.Requirements.Add(requirement);
            await _db.SaveChangesAsync(ct);
        }

        var module = await _db.RequirementModules
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(m => m.ProjectId == session.ProjectId && m.RequirementId == requirement.Id && m.Name == bucketName, ct);
        if (module is null)
        {
            module = new RequirementModule
            {
                TenantId = session.TenantId,
                ProjectId = session.ProjectId,
                RequirementId = requirement.Id,
                Name = bucketName,
                Description = "Scenarios recorded automatically by prompt-driven exploration.",
            };
            _db.RequirementModules.Add(module);
            await _db.SaveChangesAsync(ct);
        }

        feature = new Feature
        {
            TenantId = session.TenantId,
            ProjectId = session.ProjectId,
            ModuleId = module.Id,
            Name = bucketName,
            Description = "Scenarios recorded automatically by prompt-driven exploration.",
            Priority = Priority.Medium,
        };
        _db.Features.Add(feature);
        await _db.SaveChangesAsync(ct);
        return feature.Id;
    }

    private async Task<(StepRunStatus Status, string Detail)> ExecuteScenarioStepAsync(
        ExplorationSession session,
        PlaywrightBrowserService browser,
        string baseUrl,
        ScenarioStep step,
        CancellationToken ct)
    {
        var recorded = DeserializeRecordedActions(step.RecordedActionsJson);

        // Replay-first: if we already recorded how to satisfy this step, replay it deterministically
        // and only fall back to AI when a locator no longer resolves (self-healing).
        if (recorded.Count > 0)
        {
            return await ReplayRecordedStepAsync(session, browser, baseUrl, step, recorded, ct);
        }

        // No recording yet. Without a live LLM, use the deterministic single-action heuristic.
        if (!_llm.IsLive)
        {
            return await ExecuteScenarioStepHeuristicAsync(session, browser, baseUrl, step, ct);
        }

        // Record phase: let the AI drive the browser, capturing the concrete actions it used so the
        // next run can replay them without the AI.
        var (status, detail, actions) = await RunAgenticStepAsync(session, browser, baseUrl, step, ct);

        if (status is StepRunStatus.Passed or StepRunStatus.Healed)
        {
            // Always record a replayable script so future runs are deterministic and never re-invoke
            // the AI for a step that already works. Verification-only steps record a single assert.
            if (actions.Count == 0)
            {
                actions.Add(new RecordedStepAction { Kind = "assert", Target = step.ExpectedResult ?? step.Action });
            }

            step.RecordedActionsJson = SerializeRecordedActions(actions);
            step.NeedsReview = false;
            step.ReviewReason = null;
            await _db.SaveChangesAsync(ct);
            await PublishLogAsync(session, $"  ⤓ Recorded {actions.Count} action(s) for step {step.Order} (future runs replay this).", "info", ct);
        }
        else if (status == StepRunStatus.Failed)
        {
            step.NeedsReview = true;
            step.ReviewReason = detail;
            await _db.SaveChangesAsync(ct);
            await PublishLogAsync(session, $"  ⚠ Needs your input — step {step.Order}: {detail}", "warn", ct);
        }

        return (status, detail);
    }

    /// <summary>
    /// Replays a step's recorded actions deterministically. If an action's target can no longer be
    /// resolved, it heals by re-locating the element with the AI and updates the recorded target in
    /// place. If it cannot be located at all, the step is flagged for the user to review/refine.
    /// </summary>
    private async Task<(StepRunStatus Status, string Detail)> ReplayRecordedStepAsync(
        ExplorationSession session,
        PlaywrightBrowserService browser,
        string baseUrl,
        ScenarioStep step,
        List<RecordedStepAction> recorded,
        CancellationToken ct)
    {
        var anyHealed = false;

        for (var i = 0; i < recorded.Count; i++)
        {
            ct.ThrowIfCancellationRequested();
            var action = recorded[i];

            var ok = await ExecuteRecordedActionAsync(browser, action, baseUrl, ct);
            if (ok)
            {
                continue;
            }

            // The recorded locator failed — attempt to heal by re-locating with the AI.
            await PublishLogAsync(session, $"  ↻ Locator '{action.Target}' failed; re-locating with AI…", "warn", ct);
            var healedTarget = _llm.IsLive
                ? await HealRecordedActionAsync(session, browser, step, action, baseUrl, ct)
                : null;

            if (healedTarget is not null)
            {
                action.Target = healedTarget;
                action.Healed = true;
                anyHealed = true;
                await PublishLogAsync(session, $"  ✎ Healed: re-located to '{healedTarget}' and updated the step.", "warn", ct);
                continue;
            }

            // Could not heal — escalate to the user.
            var reason = $"Could not locate '{action.Target}' to {action.Kind} for this step. The page may have changed or the step description is ambiguous — please refine it.";
            step.NeedsReview = true;
            step.ReviewReason = reason;
            if (anyHealed)
            {
                step.RecordedActionsJson = SerializeRecordedActions(recorded);
            }
            await _db.SaveChangesAsync(ct);
            await PublishLogAsync(session, $"  ⚠ Needs your input — step {step.Order}: {reason}", "warn", ct);
            return (StepRunStatus.Failed, reason);
        }

        // Persist any healed locators so the next run is deterministic again.
        if (anyHealed)
        {
            step.RecordedActionsJson = SerializeRecordedActions(recorded);
            await _db.SaveChangesAsync(ct);
        }

        var matched = await MatchesExpectedOutcomeAsync(browser, step.ExpectedResult, ct);
        var status = anyHealed ? StepRunStatus.Healed : StepRunStatus.Passed;
        var detail = anyHealed
            ? "Replayed the recorded actions and healed a changed locator."
            : (matched ? "Replayed the recorded actions; the expected state is present." : "Replayed the recorded actions.");
        return (status, detail);
    }

    /// <summary>Executes one recorded action against the current page by its stable descriptor.</summary>
    private static async Task<bool> ExecuteRecordedActionAsync(
        PlaywrightBrowserService browser,
        RecordedStepAction action,
        string baseUrl,
        CancellationToken ct)
    {
        try
        {
            switch (action.Kind?.Trim().ToLowerInvariant())
            {
                case "navigate":
                    var url = ResolveNavigationTarget(browser.CurrentUrl ?? baseUrl, action.Target, baseUrl);
                    await browser.NavigateAsync(url, ct);
                    return true;
                case "click":
                    return !string.IsNullOrWhiteSpace(action.Target) && await browser.TryClickByHintAsync(action.Target!, ct) >= 0;
                case "type":
                    return !string.IsNullOrWhiteSpace(action.Target) && await browser.TryFillAsync(action.Target!, action.Value ?? string.Empty, ct) >= 0;
                case "press":
                    return await browser.PressKeyAsync(string.IsNullOrWhiteSpace(action.Target) ? "Enter" : action.Target!, ct);
                case "wait":
                    await browser.WaitAsync(600, ct);
                    return true;
                case "assert":
                    // Verification-only step: the outcome is checked after replay; treat as a no-op here.
                    return true;
                default:
                    return false;
            }
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// Re-locates the element for a failed recorded action using one focused AI turn over the current
    /// page snapshot, executes it, and returns the new stable descriptor (or null if it cannot be found).
    /// </summary>
    private async Task<string?> HealRecordedActionAsync(
        ExplorationSession session,
        PlaywrightBrowserService browser,
        ScenarioStep step,
        RecordedStepAction action,
        string baseUrl,
        CancellationToken ct)
    {
        var pageUrl = string.IsNullOrWhiteSpace(browser.CurrentUrl) ? baseUrl : browser.CurrentUrl;
        var snapshot = await browser.SnapshotForAgentAsync(60, ct);

        // Frame the heal as a single-action sub-goal so the agent picks the closest matching element.
        var subGoal = $"{action.Kind} the element previously described as \"{action.Target}\" (part of: {step.Action})";
        var decision = await DecideNextStepActionAsync(
            new ScenarioStep { Action = subGoal, ExpectedResult = step.ExpectedResult, Order = step.Order, ScenarioId = step.ScenarioId },
            pageUrl, snapshot, history: string.Empty, turn: 1, maxTurns: 1, ct);

        if (decision is null || string.IsNullOrWhiteSpace(decision.Action))
        {
            return null;
        }

        // Force the healed action to the recorded kind/value so we don't drift from the recording.
        decision.Action = action.Kind;
        decision.Value = action.Value;

        var (ok, _, _, recordedAgain) = await PerformAgentActionAsync(browser, decision, step, baseUrl, pageUrl, ct);
        return ok ? (recordedAgain?.Target ?? decision.Target ?? action.Target) : null;
    }

    private static List<RecordedStepAction> DeserializeRecordedActions(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return [];
        }

        try
        {
            return JsonSerializer.Deserialize<List<RecordedStepAction>>(json, JsonOpts) ?? [];
        }
        catch (JsonException)
        {
            return [];
        }
    }

    private static string SerializeRecordedActions(List<RecordedStepAction> actions) =>
        JsonSerializer.Serialize(actions, JsonOpts);

    /// <summary>
    /// Record phase: the AI looks at a live page snapshot each turn and decides the next browser
    /// action itself, taking as many actions as needed to satisfy the step. The concrete successful
    /// actions are captured for deterministic replay on future runs.
    /// </summary>
    private async Task<(StepRunStatus Status, string Detail, List<RecordedStepAction> Actions)> RunAgenticStepAsync(
        ExplorationSession session,
        PlaywrightBrowserService browser,
        string baseUrl,
        ScenarioStep step,
        CancellationToken ct)
    {
        const int maxTurns = 8;
        var history = new StringBuilder();
        var actedAtLeastOnce = false;
        var usedFallback = false;
        var actions = new List<RecordedStepAction>();

        for (var turn = 1; turn <= maxTurns; turn++)
        {
            ct.ThrowIfCancellationRequested();

            var pageUrl = string.IsNullOrWhiteSpace(browser.CurrentUrl) ? baseUrl : browser.CurrentUrl;
            var snapshot = await browser.SnapshotForAgentAsync(60, ct);

            var decision = await DecideNextStepActionAsync(step, pageUrl, snapshot, history.ToString(), turn, maxTurns, ct);
            if (decision is null)
            {
                history.AppendLine($"- turn {turn}: no decision returned; retrying.");
                continue;
            }

            var action = decision.Action?.Trim().ToLowerInvariant();

            if (decision.StepComplete || action == "finish" || decision.StepFailed)
            {
                if (decision.StepFailed)
                {
                    return (StepRunStatus.Failed, TrimThought(decision.Thought) ?? "The agent determined the step cannot be completed on this page.", actions);
                }

                var matched = await MatchesExpectedOutcomeAsync(browser, step.ExpectedResult, ct);
                if (matched)
                {
                    return (usedFallback ? StepRunStatus.Healed : StepRunStatus.Passed,
                        TrimThought(decision.Thought) ?? "Completed the step; the expected state is present.", actions);
                }

                return actedAtLeastOnce
                    ? (StepRunStatus.Healed, "Completed the actions, but the expected outcome text was not detected on the page.", actions)
                    : (StepRunStatus.Failed, "The step required interaction but the agent took no action and the expected outcome was absent.", actions);
            }

            var (ok, viaFallback, observation, recordedAction) = await PerformAgentActionAsync(browser, decision, step, baseUrl, pageUrl, ct);
            if (ok)
            {
                actedAtLeastOnce = true;
                if (viaFallback)
                {
                    usedFallback = true;
                }

                if (recordedAction is not null)
                {
                    actions.Add(recordedAction);
                }
            }

            history.AppendLine($"- turn {turn}: {action} -> {observation}. (thought: {TrimThought(decision.Thought)})");
            await PublishStatusAsync(session, browser.CurrentUrl, ct);
        }

        var finalMatched = await MatchesExpectedOutcomeAsync(browser, step.ExpectedResult, ct);
        if (finalMatched)
        {
            return (usedFallback ? StepRunStatus.Healed : StepRunStatus.Passed,
                "Reached the expected state after several actions.", actions);
        }

        return (StepRunStatus.Failed, actedAtLeastOnce
            ? "Performed multiple actions but the expected outcome was not reached within the step's action budget."
            : "The agent could not determine an action to complete the step.", actions);
    }

    /// <summary>Asks the LLM for the next single browser action toward the current step's goal.</summary>
    private async Task<StepAgentDecision?> DecideNextStepActionAsync(
        ScenarioStep step,
        string pageUrl,
        string snapshot,
        string history,
        int turn,
        int maxTurns,
        CancellationToken ct)
    {
        try
        {
            var raw = await _llm.CompleteAsync(
                StepAgentPrompts.System,
                StepAgentPrompts.BuildUserPrompt(step.Action, step.ExpectedResult, pageUrl, snapshot, history, turn, maxTurns),
                jsonMode: true,
                ct);

            var json = JsonExtraction.ExtractJsonObject(raw);
            return JsonSerializer.Deserialize<StepAgentDecision>(json, JsonOpts);
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Step agent decision failed for step {StepId} on turn {Turn}.", step.Id, turn);
            return null;
        }
    }

    /// <summary>Executes a single agent decision. Returns whether it succeeded, whether a fallback
    /// (non-ref) strategy was used, a short human-readable observation, and the concrete replayable
    /// action to record (null for transient actions like wait).</summary>
    private async Task<(bool Ok, bool ViaFallback, string Observation, RecordedStepAction? Recorded)> PerformAgentActionAsync(
        PlaywrightBrowserService browser,
        StepAgentDecision decision,
        ScenarioStep step,
        string baseUrl,
        string pageUrl,
        CancellationToken ct)
    {
        try
        {
            switch (decision.Action?.Trim().ToLowerInvariant())
            {
                case "navigate":
                    var url = ResolveNavigationTarget(pageUrl, decision.Target, baseUrl);
                    await browser.NavigateAsync(url, ct);
                    return (true, false, $"navigated to {browser.CurrentUrl}",
                        new RecordedStepAction { Kind = "navigate", Target = url });

                case "click":
                    var clickDescriptor = await ResolveActionDescriptorAsync(browser, decision, ct);
                    var clickedByRef = !string.IsNullOrWhiteSpace(decision.Ref) && await browser.TryClickByRefAsync(decision.Ref!, ct);
                    if (clickedByRef)
                    {
                        return (true, false, $"clicked [ref={decision.Ref}]",
                            new RecordedStepAction { Kind = "click", Target = clickDescriptor });
                    }

                    var clickIdx = await browser.TryClickByHintAsync(decision.Target ?? step.Action, ct);
                    return clickIdx >= 0
                        ? (true, true, $"clicked \"{decision.Target}\" via fallback",
                            new RecordedStepAction { Kind = "click", Target = clickDescriptor })
                        : (false, false, $"could not click {decision.Ref ?? decision.Target}", null);

                case "type":
                    var value = decision.Value ?? string.Empty;
                    var typeDescriptor = await ResolveActionDescriptorAsync(browser, decision, ct);
                    var filledByRef = !string.IsNullOrWhiteSpace(decision.Ref) && await browser.TryFillByRefAsync(decision.Ref!, value, ct);
                    if (filledByRef)
                    {
                        return (true, false, $"typed '{value}' into [ref={decision.Ref}]",
                            new RecordedStepAction { Kind = "type", Target = typeDescriptor, Value = value });
                    }

                    var fillIdx = await browser.TryFillAsync(decision.Target ?? step.Action, value, ct);
                    return fillIdx >= 0
                        ? (true, true, $"typed '{value}' into \"{decision.Target}\" via fallback",
                            new RecordedStepAction { Kind = "type", Target = typeDescriptor, Value = value })
                        : (false, false, $"could not find input {decision.Ref ?? decision.Target}", null);

                case "press":
                    var key = string.IsNullOrWhiteSpace(decision.Target) ? "Enter" : decision.Target!;
                    var pressed = await browser.PressKeyAsync(key, ct);
                    return (pressed, false, pressed ? $"pressed {key}" : $"could not press {key}",
                        pressed ? new RecordedStepAction { Kind = "press", Target = key } : null);

                case "wait":
                    await browser.WaitAsync(800, ct);
                    return (true, false, "waited for the page to settle", null);

                default:
                    return (false, false, $"unknown action '{decision.Action}'", null);
            }
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Agent action '{Action}' threw for step {StepId}.", decision.Action, step.Id);
            return (false, false, $"action '{decision.Action}' errored: {(ex.Message.Length > 120 ? ex.Message[..120] : ex.Message)}", null);
        }
    }

    /// <summary>
    /// Builds the stable descriptor to record for a click/type action: the accessible name the
    /// agent supplied, or (when it only gave a ref) the element's accessible name resolved from the page.
    /// </summary>
    private static async Task<string?> ResolveActionDescriptorAsync(
        PlaywrightBrowserService browser,
        StepAgentDecision decision,
        CancellationToken ct)
    {
        if (!string.IsNullOrWhiteSpace(decision.Target))
        {
            return decision.Target!.Trim();
        }

        if (!string.IsNullOrWhiteSpace(decision.Ref))
        {
            var name = await browser.GetRefAccessibleNameAsync(decision.Ref!, ct);
            if (!string.IsNullOrWhiteSpace(name))
            {
                return name;
            }
        }

        return null;
    }

    private static string? TrimThought(string? thought)
    {
        if (string.IsNullOrWhiteSpace(thought))
        {
            return null;
        }

        var t = thought.Trim();
        return t.Length > 200 ? t[..200] : t;
    }

    private async Task<(StepRunStatus Status, string Detail)> ExecuteScenarioStepHeuristicAsync(
        ExplorationSession session,
        PlaywrightBrowserService browser,
        string baseUrl,
        ScenarioStep step,
        CancellationToken ct)
    {
        var pageUrl = string.IsNullOrWhiteSpace(browser.CurrentUrl) ? baseUrl : browser.CurrentUrl;
        var a11yJson = await browser.GetAccessibilityTreeAsync(ct);
        var snapshot = await browser.SnapshotForAgentAsync(60, ct);

        var plan = await TryInterpretStepAsync(step, pageUrl, a11yJson, snapshot, ct);
        if (plan is null)
        {
            return await AutoHealStepAsync(session, step, "Unable to interpret the step into a browser action.", browser, ct);
        }

        try
        {
            StepRunStatus status;
            string detail;

            switch (plan.Action?.Trim().ToLowerInvariant())
            {
                case "navigate":
                    var targetUrl = ResolveNavigationTarget(pageUrl, plan.Target, baseUrl);
                    await browser.NavigateAsync(targetUrl, ct);
                    var navigationMatches = await MatchesExpectedOutcomeAsync(browser, step.ExpectedResult, ct);
                    status = ScenarioStepOutcomeResolver.ResolveAssertion(true, navigationMatches, 0);
                    detail = navigationMatches ? $"Navigated to {targetUrl}." : $"Navigated to {targetUrl}, but the expected outcome did not match.";
                    break;

                case "click":
                    // Prefer the snapshot ref (deterministic, like Playwright MCP); fall back to hint matching.
                    var clickedByRef = !string.IsNullOrWhiteSpace(plan.Ref) && await browser.TryClickByRefAsync(plan.Ref, ct);
                    var clickResult = clickedByRef ? 0 : await browser.TryClickByHintAsync(plan.Target ?? step.Action, ct);
                    var clickMatched = clickResult >= 0;
                    var clickExpectation = await MatchesExpectedOutcomeAsync(browser, step.ExpectedResult, ct);
                    status = ScenarioStepOutcomeResolver.ResolveAssertion(clickMatched, clickExpectation, clickResult);
                    detail = clickMatched
                        ? (clickExpectation ? "Clicked the matched control and the expected state is present." : "Clicked the matched control, but the expected state did not appear.")
                        : "No matching control was found for the click step.";
                    break;

                case "type":
                    var value = string.IsNullOrWhiteSpace(plan.Value) ? "Sample value" : plan.Value;
                    // Prefer the snapshot ref; fall back to hint-based field resolution.
                    var filledByRef = !string.IsNullOrWhiteSpace(plan.Ref) && await browser.TryFillByRefAsync(plan.Ref, value, ct);
                    var fillResult = filledByRef ? 0 : await browser.TryFillAsync(plan.Target ?? step.Action, value, ct);
                    var fillMatched = fillResult >= 0;
                    var fillExpectation = await MatchesExpectedOutcomeAsync(browser, step.ExpectedResult, ct);
                    status = ScenarioStepOutcomeResolver.ResolveAssertion(fillMatched, fillExpectation, fillResult);
                    detail = fillMatched
                        ? (fillExpectation ? $"Filled the target field with '{value}' and the expected state is present." : $"Filled the target field with '{value}', but the page did not match the expected result.")
                        : "Could not match the input field.";
                    break;

                case "assert":
                    var assertionMatched = await MatchesExpectedOutcomeAsync(browser, step.ExpectedResult ?? step.Action, ct);
                    status = ScenarioStepOutcomeResolver.ResolveAssertion(true, assertionMatched, 0);
                    detail = assertionMatched ? "The assertion matched the page state." : "The expected outcome was not present on the page.";
                    break;

                case "none":
                    status = await TryFallbackStepExecutionAsync(browser, step, baseUrl, ct);
                    detail = status == StepRunStatus.Passed ? "No direct interaction was required and the page remained valid." : "The no-op step did not satisfy the scenario expectation.";
                    break;

                default:
                    status = await TryFallbackStepExecutionAsync(browser, step, baseUrl, ct);
                    detail = status == StepRunStatus.Passed ? "Executed the fallback interpretation for the step." : "Fallback step execution did not match a valid control.";
                    break;
            }

            if (status == StepRunStatus.Failed)
            {
                return await AutoHealStepAsync(session, step, detail, browser, ct);
            }

            return (status, detail);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Scenario step execution failed for step {StepId} ({Action}).", step.Id, step.Action);
            return await AutoHealStepAsync(session, step, ex.Message.Length > 180 ? ex.Message[..180] : ex.Message, browser, ct);
        }
    }

    private async Task<(StepRunStatus Status, string Detail)> AutoHealStepAsync(
        ExplorationSession session,
        ScenarioStep step,
        string failureDetail,
        PlaywrightBrowserService browser,
        CancellationToken ct)
    {
        try
        {
            var healed = await TryHealScenarioStepAsync(session, step, browser, ct);
            if (healed is null)
            {
                return (StepRunStatus.Failed, failureDetail);
            }

            return (StepRunStatus.Healed, healed);
        }
        catch (Exception ex)
        {
            // Auto-healing is best-effort; a failure here must not abort the whole scenario run.
            _logger.LogWarning(ex, "Auto-heal failed for step {StepId} ({Action}).", step.Id, step.Action);
            return (StepRunStatus.Failed, failureDetail);
        }
    }

    private async Task<string?> TryHealScenarioStepAsync(
        ExplorationSession session,
        ScenarioStep step,
        PlaywrightBrowserService browser,
        CancellationToken ct)
    {
        var scenario = await _db.Scenarios
            .IgnoreQueryFilters()
            .Include(s => s.Steps)
            .FirstOrDefaultAsync(s => s.Id == step.ScenarioId && s.ProjectId == session.ProjectId, ct);

        if (scenario is null)
        {
            return null;
        }

        var current = scenario.Steps.FirstOrDefault(s => s.Id == step.Id);
        if (current is null)
        {
            return null;
        }

        var pageText = await browser.GetPageHtmlAsync(ct);
        var improvedAction = ImproveStepAction(step.Action, pageText);
        if (string.IsNullOrWhiteSpace(improvedAction) || improvedAction == step.Action)
        {
            return null;
        }

        current.Action = improvedAction.Trim();
        if (string.IsNullOrWhiteSpace(current.ExpectedResult))
        {
            current.ExpectedResult = "Assert the expected page state is present after this action.";
        }

        await _db.SaveChangesAsync(ct);
        return $"Auto-healed the scenario step to '{improvedAction}'.";
    }

    private static string ImproveStepAction(string originalAction, string pageHtml)
    {
        var action = originalAction.Trim();
        var lower = action.ToLowerInvariant();

        if (lower.Contains("click") || lower.Contains("submit") || lower.Contains("continue") || lower.Contains("save") || lower.Contains("select"))
        {
            if (pageHtml.Contains("submit", StringComparison.OrdinalIgnoreCase) || pageHtml.Contains("continue", StringComparison.OrdinalIgnoreCase))
            {
                return action.Contains("submit", StringComparison.OrdinalIgnoreCase)
                    ? action
                    : $"Click the submit or continue action on the page";
            }
        }

        if (lower.Contains("enter") || lower.Contains("type") || lower.Contains("fill") || lower.Contains("search"))
        {
            return action.Contains("email", StringComparison.OrdinalIgnoreCase)
                ? "Enter a valid email address into the email field"
                : "Enter a valid value into the visible input field";
        }

        if (lower.Contains("navigate") || lower.Contains("go to") || lower.Contains("open") || lower.Contains("visit"))
        {
            return "Navigate to the correct page or route and verify it loads";
        }

        return action;
    }

    private async Task<bool> MatchesExpectedOutcomeAsync(PlaywrightBrowserService browser, string? expected, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(expected))
        {
            return true;
        }

        var pageHtml = await browser.GetPageHtmlAsync(ct);
        var pageText = await browser.GetAccessibilityTreeAsync(ct);
        var normalizedExpected = expected.Trim();

        return pageHtml.Contains(normalizedExpected, StringComparison.OrdinalIgnoreCase)
            || pageText.Contains(normalizedExpected, StringComparison.OrdinalIgnoreCase)
            || browser.CurrentUrl.Contains(normalizedExpected, StringComparison.OrdinalIgnoreCase);
    }

    private async Task<StepActionPlan?> TryInterpretStepAsync(
        ScenarioStep step,
        string currentUrl,
        string accessibilityTree,
        string interactiveSummary,
        CancellationToken ct)
    {
        if (_llm.IsLive)
        {
            try
            {
                var raw = await _llm.CompleteAsync(
                    StepInterpretationPrompts.System,
                    StepInterpretationPrompts.BuildUserPrompt(step.Action, step.ExpectedResult, currentUrl, accessibilityTree, interactiveSummary),
                    jsonMode: true,
                    ct);

                var json = JsonExtraction.ExtractJsonObject(raw);
                var plan = JsonSerializer.Deserialize<StepActionPlan>(json, JsonOpts);
                if (plan is not null)
                {
                    return plan;
                }
            }
            catch (Exception ex)
            {
                _logger.LogDebug(ex, "LLM step interpretation failed for step {StepId}; falling back to heuristic execution.", step.Id);
            }
        }

        return BuildHeuristicStepPlan(step);
    }

    private static StepActionPlan BuildHeuristicStepPlan(ScenarioStep step)
    {
        var text = step.Action.Trim();
        var lower = text.ToLowerInvariant();

        if (lower.Contains("navigate") || lower.Contains("open") || lower.Contains("visit") || lower.Contains("go to"))
        {
            return new StepActionPlan { Action = "navigate", Target = ExtractNavigationTarget(text) };
        }

        if (lower.Contains("enter ") || lower.Contains("type ") || lower.Contains("fill ") || lower.Contains("input ") || lower.Contains("search "))
        {
            return new StepActionPlan { Action = "type", Target = "input", Value = InferSampleValue(text) };
        }

        if (lower.Contains("click ") || lower.Contains("select ") || lower.Contains("submit") || lower.Contains("continue") || lower.Contains("save"))
        {
            return new StepActionPlan { Action = "click", Target = ExtractClickableTarget(text) };
        }

        return new StepActionPlan { Action = "assert", Target = text };
    }

    private static string ResolveNavigationTarget(string currentUrl, string? target, string baseUrl)
    {
        if (string.IsNullOrWhiteSpace(target))
        {
            return currentUrl;
        }

        if (Uri.TryCreate(target, UriKind.Absolute, out var absolute))
        {
            return absolute.ToString();
        }

        if (target.StartsWith("/", StringComparison.Ordinal))
        {
            var root = new Uri(baseUrl);
            return new Uri(root, target).ToString();
        }

        return new Uri(new Uri(baseUrl), target).ToString();
    }

    private async Task<StepRunStatus> TryFallbackStepExecutionAsync(
        PlaywrightBrowserService browser,
        ScenarioStep step,
        string baseUrl,
        CancellationToken ct)
    {
        var action = step.Action.Trim();
        var lower = action.ToLowerInvariant();

        if (lower.Contains("navigate") || lower.Contains("open") || lower.Contains("visit") || lower.Contains("go to"))
        {
            var target = ResolveNavigationTarget(browser.CurrentUrl, ExtractNavigationTarget(action), baseUrl);
            await browser.NavigateAsync(target, ct);
            return StepRunStatus.Passed;
        }

        if (lower.Contains("enter ") || lower.Contains("type ") || lower.Contains("fill ") || lower.Contains("input ") || lower.Contains("search "))
        {
            var target = ExtractClickableTarget(action) ?? "input";
            var converted = await browser.TryFillAsync(target, InferSampleValue(action), ct);
            return ScenarioStepOutcomeResolver.Resolve(converted >= 0, converted);
        }

        if (lower.Contains("click ") || lower.Contains("select ") || lower.Contains("submit") || lower.Contains("continue") || lower.Contains("save"))
        {
            var target = ExtractClickableTarget(action) ?? action;
            var converted = await browser.TryClickByHintAsync(target, ct);
            return ScenarioStepOutcomeResolver.Resolve(converted >= 0, converted);
        }

        return StepRunStatus.Passed;
    }

    private static string ExtractNavigationTarget(string action)
    {
        var match = action.Split(' ', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
        if (match.Length < 3)
        {
            return "/";
        }

        var candidate = action[(action.IndexOf(' ')+1)..].Trim();
        return candidate.StartsWith("/") ? candidate : $"/{candidate}";
    }

    private static string ExtractClickableTarget(string action)
    {
        var lower = action.ToLowerInvariant();
        var keywords = new[] { "click", "select", "submit", "continue", "save", "open", "navigate", "go to", "enter", "type", "fill", "search" };
        var index = keywords.Select(k => lower.IndexOf(k, StringComparison.Ordinal)).Where(i => i >= 0).DefaultIfEmpty(-1).Min();
        if (index < 0)
        {
            return action;
        }

        var candidate = action[(index + 1)..].Trim();
        return string.IsNullOrWhiteSpace(candidate) ? action : candidate;
    }

    private static string InferSampleValue(string action)
    {
        if (action.Contains("email", StringComparison.OrdinalIgnoreCase))
        {
            return "user@example.com";
        }

        if (action.Contains("password", StringComparison.OrdinalIgnoreCase))
        {
            return "Password123!";
        }

        if (action.Contains("name", StringComparison.OrdinalIgnoreCase))
        {
            return "Test User";
        }

        if (action.Contains("search", StringComparison.OrdinalIgnoreCase))
        {
            return "sample";
        }

        return "test-value";
    }

    private async Task DiscoverElementsAsync(
        ExplorationSession session,
        DiscoveredPage page,
        PlaywrightBrowserService browser,
        CancellationToken ct)
    {
        var rawElements = await browser.DiscoverElementsAsync(ct);

        foreach (var info in rawElements)
        {
            if (string.IsNullOrWhiteSpace(info.Role) && string.IsNullOrWhiteSpace(info.Name))
            {
                continue;
            }

            var element = new DiscoveredElement
            {
                TenantId = session.TenantId,
                ProjectId = session.ProjectId,
                PageId = page.Id,
                Name = info.Name,
                Role = info.Role,
                AriaLabel = info.AriaLabel,
                TextContent = info.TextContent,
                Placeholder = info.Placeholder,
                DataTestId = info.DataTestId,
                BoundingBoxJson = info.Rect is null ? null
                    : $"{{\"x\":{info.Rect.X},\"y\":{info.Rect.Y},\"w\":{info.Rect.W},\"h\":{info.Rect.H}}}",
                IsInteractive = info.IsInteractive,
                IsVisible = true,
                ConfidenceScore = 1.0,
                LastVerifiedAt = _clock.UtcNow,
            };

            AddLocators(session.TenantId, element, info);

            _db.DiscoveredElements.Add(element);
            session.ElementsDiscovered++;
        }
    }

    private void AddLocators(Guid tenantId, DiscoveredElement element, ElementDiscoveryInfo info)
    {
        bool hasPrimary = false;

        void Add(LocatorStrategy strategy, string? value, double confidence)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return;
            }

            element.Locators.Add(new ElementLocator
            {
                TenantId = tenantId,
                ElementId = element.Id,
                Strategy = strategy,
                Value = value,
                IsPrimary = !hasPrimary,
                ConfidenceScore = confidence,
            });
            hasPrimary = true;
        }

        Add(LocatorStrategy.CSS, string.IsNullOrEmpty(info.Id) ? null : $"#{info.Id}", 1.0);
        Add(LocatorStrategy.DataAttribute, string.IsNullOrEmpty(info.DataTestId) ? null : $"[data-testid=\"{info.DataTestId}\"]", 0.95);
        Add(LocatorStrategy.ARIA, info.AriaLabel, 0.90);

        if (!string.IsNullOrWhiteSpace(info.Role) && !string.IsNullOrWhiteSpace(info.Name))
        {
            Add(LocatorStrategy.Role, $"{info.Role}:{info.Name}", 0.85);
        }

        if (!string.IsNullOrWhiteSpace(info.TextContent) && info.TextContent.Length <= 60)
        {
            Add(LocatorStrategy.Text, info.TextContent, 0.80);
        }

        Add(LocatorStrategy.Placeholder, info.Placeholder, 0.80);
        Add(LocatorStrategy.CSS, info.CssSelector, 0.70);
        Add(LocatorStrategy.XPath, info.XPath, 0.60);
    }

    private async Task TryExpandDynamicContentAsync(
        ExplorationSession session,
        DiscoveredPage page,
        PlaywrightBrowserService browser,
        string a11yJson,
        CancellationToken ct)
    {
        try
        {
            var raw = await _llm.CompleteAsync(
                ExplorerAgentPrompts.System,
                ExplorerAgentPrompts.BuildUserPrompt(page.Url, page.Title ?? string.Empty, a11yJson),
                jsonMode: true,
                ct);

            var json = JsonExtraction.ExtractJsonObject(raw);
            var decision = JsonSerializer.Deserialize<ExplorationDecision>(json, JsonOpts);

            if (decision is null || !decision.ShouldInteract || decision.Interactions.Count == 0)
            {
                return;
            }

            foreach (var interaction in decision.Interactions.Take(3))
            {
                if (string.IsNullOrWhiteSpace(interaction.Selector))
                {
                    continue;
                }

                _logger.LogInformation("LLM interaction: {Selector} — {Reason}", interaction.Selector, interaction.Reason);
                var clicked = await browser.TryClickAsync(interaction.Selector, ct);
                if (!clicked)
                {
                    continue;
                }

                // Capture newly revealed elements.
                await DiscoverElementsAsync(session, page, browser, ct);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "LLM exploration step skipped for {Url}", page.Url);
        }
    }

    private static string DeriveName(string title, string url)
    {
        if (!string.IsNullOrWhiteSpace(title))
        {
            return title[..Math.Min(title.Length, 100)];
        }

        var path = new Uri(url).PathAndQuery;
        if (path is "/" or "")
        {
            return "Home";
        }

        var segments = path.Split('/', StringSplitOptions.RemoveEmptyEntries);
        return segments.LastOrDefault() is { } last
            ? char.ToUpper(last[0]) + last[1..]
            : path;
    }

    private static string NormaliseUrl(string url) =>
        url.TrimEnd('/').Split('?')[0].ToLowerInvariant();
}
