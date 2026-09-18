using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using ATIP.Application.Common.Exceptions;
using ATIP.Application.Common.Interfaces;
using ATIP.Application.Common.Utilities;
using ATIP.Application.Engine.Contracts;
using ATIP.Application.Engine.Model;
using ATIP.Application.Features.Environments.Dtos;
using ATIP.Application.Features.Explorer.Agent;
using ATIP.Application.Features.Scenarios.Common;
using ATIP.Application.Features.Scenarios.Dtos;
using ATIP.Domain.Entities;
using ATIP.Domain.Enums;
using ATIP.Infrastructure.Configuration;
using ATIP.Infrastructure.Engine;
using ATIP.Infrastructure.Engine.Web;
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
    private readonly ITestEngine _engine;
    private readonly WebBrowserSession _browserSession;
    private readonly ILogger<ExplorerAgent> _logger;

    /// <summary>
    /// Postman-style variables resolved from the environment's test data. Any <c>{{key}}</c> token in a
    /// scenario step / mission is replaced with the matching value before the agent acts, so steps like
    /// "enter {{username}}" automatically use this environment's data. Populated per run in RunAsync.
    /// </summary>
    private Dictionary<string, string?> _variables = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>Project business-context document text injected into exploration missions (per run).</summary>
    private string _businessContext = string.Empty;

    /// <summary>
    /// Whether the scenario currently being RUN allows AI self-healing. Set per scenario in
    /// <see cref="WalkScenarioStepsAsync"/>. When false, a deterministic run never calls the LLM: a broken
    /// or missing locator fails the step outright instead of being re-located by the AI.
    /// </summary>
    private bool _autoHealEnabled = true;

    /// <summary>
    /// Identity and variable bag for the run in progress, handed to the engine with every action.
    /// Built once per session in <see cref="RunAsync"/>.
    /// </summary>
    private RunContext? _runContext;

    private static readonly System.Text.RegularExpressions.Regex VariableTokenRegex =
        new(@"\{\{\s*([^{}]+?)\s*\}\}", System.Text.RegularExpressions.RegexOptions.Compiled);

    public ExplorerAgent(
        IApplicationDbContext db,
        ILlmClient llm,
        IFileStorage fileStorage,
        IDateTimeProvider clock,
        IExplorationLiveStream live,
        Microsoft.Extensions.Options.IOptions<PlaywrightMcpOptions> mcpOptions,
        ITestEngine engine,
        WebBrowserSession browserSession,
        ILogger<ExplorerAgent> logger)
    {
        _db = db;
        _llm = llm;
        _fileStorage = fileStorage;
        _clock = clock;
        _live = live;
        _mcp = mcpOptions.Value;
        _engine = engine;
        _browserSession = browserSession;
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

        // Bind Postman-style variables from this environment's test data ({{key}} → value).
        _variables = await LoadEnvironmentVariablesAsync(session, cancellationToken);

        // Load the project's business-context documents so the AI understands the app's domain.
        _businessContext = await ScenarioBucket.LoadBusinessContextAsync(_db, session.ProjectId, cancellationToken);

        // Identity + variables for anything routed through the generic engine. The environment's
        // test data doubles as the engine's starting variables, so a step can already interpolate
        // {{username}} without the engine knowing where it came from.
        _runContext = new RunContext
        {
            TenantId = session.TenantId,
            ProjectId = session.ProjectId,
            EnvironmentId = session.EnvironmentId,
            SessionId = session.Id,
            ScenarioId = session.ScenarioId,
            BaseUrl = baseUrl,
            AllowHealing = false
        };
        _runContext.SetVariables(_variables);

        // EXPLORE (goal-driven, AI-authored) runs on the Playwright MCP server: the agent needs a rich
        // ARIA snapshot + LLM reasoning loop to discover how the real app works.
        //
        // RUN (ExecuteSavedSteps) deliberately does NOT: it executes the scenario's already-recorded
        // actions like a conventional automation framework — direct Playwright, deterministic locators,
        // zero MCP subprocess and zero LLM tokens. The AI is only consulted when a locator breaks AND the
        // scenario has auto-heal enabled. That keeps regression runs fast, repeatable and free.
        if (_mcp.Enabled && !session.ExecuteSavedSteps && (session.ScenarioId is not null || session.SuiteId is not null))
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

        // Hand the live browser to the engine so engine-routed actions land in this page — with
        // its cookies, its login and its cart — instead of a second, empty browser.
        _browserSession.Browser = browser;

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
                session.Status == ExplorationStatus.Cancelled ? $"{Noun(session)} cancelled." : $"{Noun(session)} completed.",
                session.Status == ExplorationStatus.Cancelled ? "warn" : "success",
                CancellationToken.None);
        }
        catch (OperationCanceledException)
        {
            // Reached when an awaited call throws instead of returning cooperatively (e.g. a cancelled
            // LLM/DB call) — without this, the session would be left stuck as "Running" forever since the
            // cooperative Status assignment above never executes. Covers user cancellation, the per-session
            // watchdog, and host shutdown alike.
            session.Status = ExplorationStatus.Cancelled;
            await PublishLogAsync(session, $"{Noun(session)} stopped (cancelled or the session time limit was reached).", "warn", CancellationToken.None);
        }
        catch (LlmUnavailableException ex)
        {
            await ReportAiOutageAsync(session, ex, CancellationToken.None);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogError(ex, "Exploration session {SessionId} failed.", session.Id);
            session.Status = ExplorationStatus.Failed;
            session.ErrorMessage = ex.Message[..Math.Min(ex.Message.Length, 500)];
            await PublishLogAsync(session, $"{Noun(session)} failed: {session.ErrorMessage}", "error", CancellationToken.None);
        }
        finally
        {
            session.CompletedAtUtc = _clock.UtcNow;
            await browser.StopScreencastAsync();
            await _db.SaveChangesAsync(CancellationToken.None);
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
            await mcp.ListTabsAsync(ct);
            await PublishTabsIfChangedAsync(session, mcp.Tabs, ct);
            if (_variables.Count > 0)
            {
                await PublishLogAsync(session, $"Bound {_variables.Count} test-data variable(s) from this environment (use as {{{{name}}}} in steps).", "info", ct);
            }

            foreach (var scenario in scenarios)
            {
                if (ct.IsCancellationRequested)
                {
                    break;
                }

                var orderedSteps = scenario.Steps.OrderBy(s => s.Order).ToList();

                await PublishLogAsync(session, $"Exploring the app to achieve scenario '{scenario.Title}' via MCP…", "info", ct);
                try
                {
                    await RunScenarioMissionViaMcpAsync(session, mcp, baseUrl, scenario, orderedSteps, ct);
                }
                catch (LlmUnavailableException ex)
                {
                    // The agent cannot choose actions or judge outcomes without the AI, so this scenario
                    // stopped mid-flight and every remaining one would stop the same way. Give the
                    // untested steps an honest verdict, then abort the whole run.
                    await BlockUntestedScenarioStepsAsync(session, scenario, orderedSteps, mcp.CurrentUrl, ex, ct);
                    throw;
                }
            }

            session.Status = ct.IsCancellationRequested ? ExplorationStatus.Cancelled : ExplorationStatus.Completed;
            await PublishLogAsync(session, $"{Noun(session)} completed.", "success", CancellationToken.None);
        }
        catch (OperationCanceledException)
        {
            // Same rationale as the non-MCP path: an awaited call throwing (rather than the loop noticing
            // ct.IsCancellationRequested cooperatively) must still leave the session in a terminal state.
            session.Status = ExplorationStatus.Cancelled;
            await PublishLogAsync(session, $"{Noun(session)} stopped (cancelled or the session time limit was reached).", "warn", CancellationToken.None);
        }
        catch (LlmUnavailableException ex)
        {
            await ReportAiOutageAsync(session, ex, CancellationToken.None);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogError(ex, "MCP scenario run failed for session {SessionId}.", session.Id);
            session.Status = ExplorationStatus.Failed;
            session.ErrorMessage = ex.Message[..Math.Min(ex.Message.Length, 500)];
            await PublishLogAsync(session, $"{Noun(session)} failed: {session.ErrorMessage}", "error", CancellationToken.None);
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
        while (!ct.IsCancellationRequested)
        {
            try
            {
                var frame = await mcp.ScreenshotBase64Async(ct);
                if (!string.IsNullOrEmpty(frame))
                {
                    await _live.PublishFrameAsync(session.TenantId, session.Id, frame, ct);
                }
            }
            catch (OperationCanceledException)
            {
                // expected on stop
                break;
            }
            catch (Exception ex)
            {
                // A single failed screenshot/publish (e.g. a transient SignalR hiccup) must NOT kill the
                // rest of the stream — previously an exception here silently ended live-view updates for
                // the whole remaining session (Steps/log kept updating via separate publishes, but the
                // "Live browser" image froze on the last successful frame, looking like nothing happened).
                _logger.LogDebug(ex, "MCP screenshot streaming iteration failed for session {SessionId}; will retry.", session.Id);
            }

            try
            {
                await Task.Delay(1200, ct);
            }
            catch (OperationCanceledException)
            {
                break;
            }
        }
    }

    /// <summary>
    /// Captures a screenshot of the current MCP-driven page and persists it via <see cref="IFileStorage"/>,
    /// returning the stored relative path (or null on any failure — screenshots are best-effort and must
    /// never fail a step). Used to attach visual evidence to each recorded <see cref="ScenarioStepResult"/>
    /// so the Executions "View" detail can show what the page actually looked like at each step.
    /// </summary>
    private async Task<string?> CaptureMcpStepScreenshotAsync(ExplorationSession session, PlaywrightMcpBrowser mcp, CancellationToken ct)
    {
        try
        {
            var base64 = await mcp.ScreenshotBase64Async(ct);
            if (string.IsNullOrEmpty(base64))
            {
                return null;
            }

            return await _fileStorage.SaveAsync(
                $"screenshots/{session.ProjectId}",
                $"{Guid.NewGuid():N}.jpg",
                new MemoryStream(Convert.FromBase64String(base64)),
                ct);
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Step screenshot capture failed for session {SessionId}.", session.Id);
            return null;
        }
    }

    /// <summary>Same as <see cref="CaptureMcpStepScreenshotAsync"/> but for the legacy CDP-driven browser path.</summary>
    private async Task<string?> CaptureBrowserStepScreenshotAsync(ExplorationSession session, PlaywrightBrowserService browser, CancellationToken ct)
    {
        try
        {
            var bytes = await browser.TakeScreenshotAsync(fullPage: false, ct);
            return await _fileStorage.SaveAsync(
                $"screenshots/{session.ProjectId}",
                $"{Guid.NewGuid():N}.png",
                new MemoryStream(bytes),
                ct);
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Step screenshot capture failed for session {SessionId}.", session.Id);
            return null;
        }
    }

    /// <summary>
    /// Structured QA verification of a step/scenario's expected result against the LIVE page. Decomposes
    /// the expectation into atomic conditions (presence/absence, counts/thresholds/ranges, ordering,
    /// navigation, validation/errors, data correctness) and evaluates EACH against concrete page evidence
    /// — the visible text and extracted counts/numbers, beyond the trimmed ARIA snapshot — so quantitative
    /// expectations like "do not show more than 10000 products" are checked with the real numbers, on any
    /// site. Publishes a per-check breakdown so testers see exactly what was validated. Falls back to a
    /// literal text match when no LLM is configured.
    /// </summary>
    private async Task<(bool Ok, string Reason)> VerifyOutcomeAsync(
        ExplorationSession session,
        ScenarioStep step,
        PlaywrightMcpBrowser mcp,
        string snapshot,
        CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(step.ExpectedResult))
        {
            return (true, "no explicit expected outcome");
        }

        // Let async/XHR-driven content (search results, lazy lists) render before we read the page.
        await mcp.WaitForPageSettledAsync(ct: ct);

        // Concrete DOM evidence the ARIA snapshot omits: visible text + detected counts/numbers/list sizes.
        var pageData = await SafeExtractPageDataAsync(mcp, ct);

        return await VerifyExpectedOutcomeAsync(session, step, mcp.CurrentUrl, snapshot, pageData, ct);
    }

    /// <summary>
    /// Browser-agnostic core of the QA verification described above: given the page's URL, element
    /// snapshot and extracted text/number evidence, decide whether the step's expected result actually
    /// holds. Shared by BOTH engines — the MCP exploration loop and the deterministic Playwright
    /// run/learn loop — so a step is judged by the same standard however it was executed.
    /// </summary>
    private async Task<(bool Ok, string Reason)> VerifyExpectedOutcomeAsync(
        ExplorationSession session,
        ScenarioStep step,
        string url,
        string snapshot,
        string pageData,
        CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(step.ExpectedResult))
        {
            return (true, "no explicit expected outcome");
        }

        var expected = ResolveVars(step.ExpectedResult);

        if (!_llm.IsLive)
        {
            var matched = McpMatchesExpected($"{snapshot}\n{pageData}", url, expected);
            return (matched, matched
                ? "expected text present on the page"
                : "Actual: the expected text was not found anywhere on the page (AI verification unavailable).");
        }

        try
        {
            var user = QaVerificationPrompts.BuildUserPrompt(expected, ResolveVars(step.Action), url, snapshot, pageData);
            var raw = await _llm.CompleteAsync(QaVerificationPrompts.System, user, jsonMode: true, ct);
            var json = JsonExtraction.ExtractJsonObject(raw);
            var verdict = JsonSerializer.Deserialize<QaVerdict>(json, JsonOpts);
            if (verdict is null)
            {
                return (false, "outcome could not be verified");
            }

            // Surface the QA checklist so the tester sees exactly which conditions passed/failed.
            foreach (var check in verdict.Checks ?? new List<QaCheck>())
            {
                if (string.IsNullOrWhiteSpace(check.Description))
                {
                    continue;
                }

                var evidence = string.IsNullOrWhiteSpace(check.Evidence) ? string.Empty : $" — {check.Evidence.Trim()}";
                await PublishLogAsync(
                    session,
                    $"    {(check.Passed ? "✓" : "✗")} {check.Description.Trim()}{evidence}",
                    check.Passed ? "success" : "warn",
                    ct);
            }

            if (verdict.Satisfied)
            {
                var passSummary = string.IsNullOrWhiteSpace(verdict.Summary)
                    ? "all QA checks passed"
                    : verdict.Summary.Trim();
                return (true, passSummary);
            }

            // A failure is read as "expected X, got Y". The report already shows X, so the message must
            // lead with Y — prefer the dedicated observation, and fall back to the summary only when the
            // model omitted it.
            var observed = StripVerdictPreamble(verdict.Actual);
            if (string.IsNullOrWhiteSpace(observed))
            {
                observed = StripVerdictPreamble(verdict.Summary);
            }

            var failedChecks = (verdict.Checks ?? new List<QaCheck>())
                .Where(c => !c.Passed && !string.IsNullOrWhiteSpace(c.Description))
                .Select(c => string.IsNullOrWhiteSpace(c.Evidence)
                    ? c.Description!.Trim()
                    : $"{c.Description!.Trim()} — {c.Evidence!.Trim()}")
                .ToList();

            if (string.IsNullOrWhiteSpace(observed))
            {
                observed = failedChecks.Count > 0
                    ? string.Join("; ", failedChecks)
                    : "the expected result was not met (no further detail was reported).";
            }

            return (false, $"Actual: {observed}");
        }
        catch (Exception ex) when (ex is not LlmUnavailableException)
        {
            // Only genuine verification problems may be reported as an unmet expectation. If the AI
            // provider is down we know nothing about the page, and calling that a failed check would
            // blame the application under test for an outage on our side.
            _logger.LogDebug(ex, "QA verification failed for step {StepId}; treating as unmet.", step.Id);
            return (false, "outcome could not be verified");
        }
    }

    /// <summary>
    /// Removes leading verdict boilerplate ("The expected result is NOT satisfied: …") that models add
    /// despite being told not to. Without this the report reads "…the expected result was not met: The
    /// expected result is NOT satisfied: …" before it ever says what actually happened.
    /// </summary>
    private static string StripVerdictPreamble(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return string.Empty;
        }

        var cleaned = VerdictPreamblePattern.Replace(text.Trim(), string.Empty).Trim();
        return cleaned.Length == 0 ? string.Empty : cleaned;
    }

    private static readonly Regex VerdictPreamblePattern = new(
        @"^\s*(?:the\s+)?(?:actual(?:\s+result)?|expected\s+(?:result|outcome)\s+(?:is|was)?\s*(?:not|n't)?\s*(?:satisfied|met)|verdict)\s*[:\-\u2013\u2014]\s*",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant,
        TimeSpan.FromSeconds(1));

    private static async Task<string> SafeExtractPageDataAsync(PlaywrightMcpBrowser mcp, CancellationToken ct)
    {
        try
        {
            return await mcp.ExtractPageDataAsync(ct);
        }
        catch
        {
            return string.Empty;
        }
    }


    private static async Task<(bool Ok, string Observation, bool ViaRef)> PerformMcpActionAsync(
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
                return (true, $"navigated to {mcp.CurrentUrl}", false);
            case "click":
                var clickedViaRef = !string.IsNullOrWhiteSpace(decision.Ref)
                    && await mcp.ClickAsync(decision.Target ?? "element", decision.Ref!, ct);
                if (clickedViaRef)
                {
                    return (true, $"clicked [ref={decision.Ref}]", true);
                }
                if (!string.IsNullOrWhiteSpace(decision.Target))
                {
                    // Snapshot ref may be stale on a dynamic page — relocate + click by descriptor.
                    var clickedByDescriptor = await mcp.ClickByDescriptorAsync(decision.Target!, ct);
                    if (clickedByDescriptor)
                    {
                        return (true, $"clicked \"{decision.Target}\" (by descriptor; the snapshot ref was stale)", false);
                    }
                }
                return (false, $"could not click {decision.Ref} (it may be hidden or covered by a modal/overlay — dismiss any blocking popup first, e.g. press Escape or click its close/Skip/Not now control)", false);
            case "type":
                var typed = !string.IsNullOrWhiteSpace(decision.Ref)
                    && await mcp.TypeAsync(decision.Target ?? "field", decision.Ref!, decision.Value ?? string.Empty, ct);
                if (!typed && (!string.IsNullOrWhiteSpace(decision.Target) || !string.IsNullOrWhiteSpace(decision.Value)))
                {
                    // Relocate the field by its label/placeholder and fill it directly.
                    typed = await mcp.FillByDescriptorAsync(decision.Target ?? string.Empty, decision.Value ?? string.Empty, ct);
                    if (typed)
                    {
                        return (true, $"typed \"{decision.Value}\" into \"{decision.Target}\" (by descriptor; the snapshot ref was stale)", false);
                    }
                }
                return (typed, typed ? $"typed into [ref={decision.Ref}]" : $"could not type into {decision.Ref} (the field may be covered by a modal/overlay — dismiss any blocking popup first)", false);
            case "press":
                var key = string.IsNullOrWhiteSpace(decision.Target) ? "Enter" : decision.Target!;
                var pressed = await mcp.PressKeyAsync(key, ct);
                return (pressed, pressed ? $"pressed {key}" : $"could not press {key}", false);
            case "select":
                var option = decision.Value ?? string.Empty;
                var selected = !string.IsNullOrWhiteSpace(decision.Ref)
                    && !string.IsNullOrWhiteSpace(option)
                    && await mcp.SelectOptionAsync(decision.Target ?? "dropdown", decision.Ref!, option, ct);
                return (selected,
                    selected
                        ? $"selected \"{option}\" in [ref={decision.Ref}]"
                        : $"could not select \"{option}\" in {decision.Ref ?? decision.Target} (is it a native dropdown listed with its options?)",
                    selected);
            case "wait":
                await mcp.WaitAsync(1, ct);
                return (true, "waited", false);
            default:
                return (false, $"unknown action '{decision.Action}'", false);
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
        // TestRail-imported scenarios never populate the scenario-level ExpectedResult (only each
        // individual step does) — without this fallback, VerifyOutcomeAsync sees a blank expected
        // outcome and short-circuits to a vacuous "goal reached: no explicit expected outcome" pass
        // regardless of what actually happened in the browser. Fall back to the LAST hint step's
        // ExpectedResult (the scenario's final/overall assertion) so the mission is actually verified.
        var effectiveExpectedResult = !string.IsNullOrWhiteSpace(scenario.ExpectedResult)
            ? scenario.ExpectedResult
            : hintSteps.LastOrDefault(s => !string.IsNullOrWhiteSpace(s.ExpectedResult))?.ExpectedResult;

        // NOTE: the project's business context is NOT appended to the mission text here — the step
        // agent prompt now carries it as a dedicated, clearly-delimited section (see
        // StepAgentPrompts.BuildUserPrompt), which keeps the mission itself a crisp goal statement
        // and stops domain docs from being mistaken for instructions.
        var mission = BuildScenarioMission(scenario, hintSteps, effectiveExpectedResult);
        var missionStep = new ScenarioStep { ScenarioId = scenario.Id, Order = 0, Action = mission, ExpectedResult = effectiveExpectedResult };

        // Wording the snapshot trimmer ranks against, so the controls this scenario is about survive the
        // budget even when they sit far down a long page.
        var goalText = $"{scenario.Title} {mission} {effectiveExpectedResult}";

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
        // Click/type targets that produced NO observable change. We refuse to repeat them so the agent
        // stops hammering an inert control (e.g. a filter section heading) and picks a different element.
        var noEffectKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var order = 1;
        var reachedGoal = false;
        var goalReason = string.Empty;
        var decided = false;
        // How many times we've rejected an "I'm done" claim made before the agent did anything at all.
        var prematureCompletions = 0;

        for (var turn = 1; turn <= maxTurns; turn++)
        {
            ct.ThrowIfCancellationRequested();

            // Popups can also arrive a beat AFTER the click that triggered them (deferred handlers,
            // interstitial redirects), so re-check at the top of every turn rather than only inline.
            await mcp.FollowNewestTabAsync(ct);
            await PublishTabsIfChangedAsync(session, mcp.Tabs, ct);

            // Re-arm the page guards BEFORE looking at or touching the page. They live in the document and
            // are lost on every navigation, and a navigation can happen without an action we settle after
            // (e.g. "press Enter" submits a search). Arming them only after an action is too late: the very
            // next click — a product card, which on real catalogues is target="_blank" — would open a second
            // tab that MCP never switches to, so the agent would keep clicking a page it already left.
            await mcp.StabilizePageAsync(ct);
            var snapshot = TrimSnapshot(await mcp.SnapshotAsync(ct), goalText);
            var decision = await DecideNextStepActionAsync(missionStep, mcp.CurrentUrl, snapshot, history.ToString(), turn, maxTurns, ct);
            if (decision is null)
            {
                history.AppendLine($"- turn {turn}: no decision returned.");
                continue;
            }

            decision.Value = ResolveVarsOrNull(decision.Value);
            var action = decision.Action?.Trim().ToLowerInvariant();

            // MCP reports a composite control's accessible name as the WHOLE of its nested text, so a
            // product card arrives as a 400-character blob of title + rating + price + badges. Left
            // alone that becomes an unreadable step AND an unreplayable locator, because most of the
            // blob is volatile — the price, the review count and "Only 1 left" all change by the next
            // run. Condense it to the stable leading phrase. Only element-addressing verbs are touched:
            // "navigate" carries a URL in Target and "press" carries a key name, and cutting either
            // would break the action itself.
            if (action is "click" or "type" or "select")
            {
                decision.Target = CondenseTargetDescriptor(decision.Target);
            }

            if (decision.StepComplete || action == "finish" || decision.StepFailed)
            {
                // A scenario with no top-level ExpectedResult inherits its goal from the LAST authored step,
                // and that step is very often a weak assertion that is ALREADY TRUE on the landing page ("the
                // search box is displayed"). The agent then verifies the goal on turn 1, sees it satisfied,
                // and reports success without ever exercising the flow — the scenario passes having clicked
                // nothing. A goal cannot be achieved by doing nothing, so reject an early completion while the
                // authored script still has steps and not one action has landed. Capped so a genuinely
                // no-op scenario still terminates instead of burning the whole turn budget.
                if (!decision.StepFailed && discovered.Count == 0 && hintSteps.Count > 0 && prematureCompletions < 2)
                {
                    prematureCompletions++;
                    history.AppendLine(
                        $"- turn {turn}: REJECTED your \"goal reached\" claim — you have not performed a SINGLE action yet. "
                        + "The starting page cannot already satisfy this scenario; preconditions being met is NOT the goal. "
                        + "Carry out the authored steps against the real UI, starting with the next one you have not done.");
                    await PublishLogAsync(
                        session,
                        "  ↷ Ignoring an early \"goal reached\" — no action has been performed yet.",
                        "warn",
                        ct);
                    continue;
                }

                if (decision.StepFailed)
                {
                    goalReason = TrimThought(decision.Thought) ?? "The agent concluded the scenario cannot be accomplished on this app.";
                }
                else
                {
                    var (ok, reason) = await VerifyOutcomeAsync(session, missionStep, mcp, snapshot, ct);
                    reachedGoal = ok;
                    goalReason = ok ? (TrimThought(decision.Thought) ?? reason) : reason;
                }

                decided = true;
                break;
            }

            // Refuse to repeat a click/type on a target that already produced no effect — otherwise the
            // agent can loop on an inert control (e.g. it kept clicking the "Brand" filter heading). Tell
            // it to choose a different, more specific element instead of burning turns on the same one.
            var actionKey = action is "click" or "type"
                ? $"{action}|{(decision.Target ?? decision.Ref ?? string.Empty).Trim().ToLowerInvariant()}"
                : null;
            if (actionKey is not null && noEffectKeys.Contains(actionKey))
            {
                var label = decision.Target ?? decision.Ref ?? "that element";
                history.AppendLine($"- turn {turn}: SKIPPED repeat {action} on \"{label}\" — it had NO effect earlier. Pick a DIFFERENT, more specific element (e.g. a concrete brand/option/checkbox that is already visible in the snapshot, not a section heading or a \"MORE\"/expand link), or a different approach.");
                await PublishLogAsync(session, $"  ↷ Skipping repeat {action} on \"{label}\" (no effect earlier) — trying a different element.", "warn", ct);
                continue;
            }

            var preActionUrl = mcp.CurrentUrl;

            // Announce the action BEFORE performing it. Everything that follows — settling the page,
            // re-snapshotting, the descriptor retry, the screenshot — takes seconds, so publishing only
            // the verdict made the log and the Steps panel visibly trail the live browser, which had
            // already moved on. The announcement carries a stable id and is REWRITTEN in place by the
            // verdict below, so the log gains no duplicate lines.
            var logId = $"turn-{turn}";
            var pendingDesc = DescribeMcpAction(action, decision, decision.Target ?? preActionUrl);
            await PublishLogAsync(session, $"  → {order}. {pendingDesc}", "pending", ct, logId);
            await PublishStepAsync(
                session,
                scenario,
                new ScenarioStep { Order = order, Action = pendingDesc },
                "Running",
                TrimThought(decision.Thought),
                preActionUrl,
                ct);

            var (performed, observation, viaRef) = await PerformMcpActionAsync(mcp, decision, baseUrl, ct);

            // If the click opened a popup/new tab, move to it before anything else looks at the page.
            // Everything downstream — the effect check, the after-snapshot the agent reasons over, the
            // step screenshot and the recorded URL — must describe the page the user can actually see.
            if (performed && action is "click" or "press")
            {
                var popupUrl = await mcp.FollowNewestTabAsync(ct);
                if (popupUrl is not null)
                {
                    observation += $" — this opened a new tab; switched to it ({popupUrl}).";
                    await PublishLogAsync(session, $"     ↳ opened a new tab — switched to {popupUrl}", "info", ct);
                }

                await PublishTabsIfChangedAsync(session, mcp.Tabs, ct);
            }

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

            // "performed" only means the click/type landed on an element — NOT that it did anything.
            // Many controls (e.g. a sort/filter link on a dynamic SPA) can be clicked with no effect,
            // which previously got recorded as a false "Passed". Verify the page actually changed
            // before claiming success; if nothing changed, record the step as unverified (Skipped) and
            // tell the agent so it can try a different approach instead of assuming it worked.
            var hadEffect = true;
            if (performed && action is "click" or "type" or "select")
            {
                // Let the click's effect (navigation, re-render, XHR results) settle before comparing.
                await mcp.WaitForPageSettledAsync(ct: ct);
                var afterSnapshot = TrimSnapshot(await mcp.SnapshotAsync(ct), goalText);
                hadEffect = !string.Equals(preActionUrl, mcp.CurrentUrl, StringComparison.Ordinal)
                    || !SnapshotsEquivalent(snapshot, afterSnapshot);

                // Self-heal: a ref-based click can report success (Playwright found the element and
                // dispatched a click) yet have no real effect — common for custom checkbox/radio filter
                // widgets (e.g. Flipkart's Brand list) where the accessible ref points at a wrapper that
                // doesn't actually toggle the underlying control. Before giving up, retry once by
                // descriptor (which also matches checkbox/radio/label elements) targeting the same
                // element's name — this often lands on the real toggle and fixes it within the same turn.
                if (!hadEffect && action == "click" && viaRef && !string.IsNullOrWhiteSpace(decision.Target))
                {
                    var healed = await mcp.ClickByDescriptorAsync(decision.Target!, ct);
                    if (healed)
                    {
                        await mcp.WaitForPageSettledAsync(ct: ct);
                        afterSnapshot = TrimSnapshot(await mcp.SnapshotAsync(ct), goalText);
                        hadEffect = !string.Equals(preActionUrl, mcp.CurrentUrl, StringComparison.Ordinal)
                            || !SnapshotsEquivalent(snapshot, afterSnapshot);
                        if (hadEffect)
                        {
                            observation += " — the ref-based click had no effect, but a descriptor-based retry on the same element succeeded.";
                        }
                    }
                }

                if (!hadEffect)
                {
                    if (actionKey is not null)
                    {
                        noEffectKeys.Add(actionKey);
                    }
                    observation += " — but the page did not change afterwards, so this control may not have taken effect (e.g. the sort/filter was not applied); try a different, more specific element (e.g. a concrete option/checkbox, not a section heading).";
                }
            }

            // Escape/dismissal and scrolling keypresses are housekeeping, not meaningful test steps —
            // and repeated identical actions (e.g. the agent hammering Escape at a stubborn modal) are
            // just noise. Scroll keys in particular are pure flailing: the snapshot already covers the
            // whole document, so PageDown reveals nothing, yet lazy-loading makes the page change and
            // the step used to be recorded as a genuine "Passed" action.
            var isHousekeepingKey = action == "press"
                && (string.IsNullOrWhiteSpace(decision.Target)
                    || HousekeepingKeys.Contains(decision.Target!.Trim()));

            // Correct the agent's mental model immediately, otherwise it burns every remaining turn
            // scrolling for a control that was never hidden — just absent from a trimmed snapshot.
            if (action == "press" && !string.IsNullOrWhiteSpace(decision.Target) && ScrollKeys.Contains(decision.Target!.Trim()))
            {
                observation += " — NOTE: scrolling shows you NOTHING new. The snapshot already covers the whole page from top to bottom, so a control you cannot find there is not below the fold — it does not exist under that name. Stop scrolling: act on a control that IS in the snapshot, or look for the same control under different wording.";
            }

            var isRecordable = performed && !isHousekeepingKey && action is "navigate" or "click" or "type" or "press" or "select";
            var recorded = false;
            if (isRecordable)
            {
                var desc = DescribeMcpAction(action, decision, mcp.CurrentUrl);
                var isDuplicate = discovered.Count > 0
                    && string.Equals(discovered[^1].Action, desc, StringComparison.OrdinalIgnoreCase);
                if (!isDuplicate)
                {
                    var stepStatus = hadEffect ? StepRunStatus.Passed : StepRunStatus.Skipped;
                    var stepDetail = hadEffect
                        ? TrimThought(decision.Thought)
                        : "The control was clicked but the page did not change — it may not have applied (e.g. the sort/filter did not take effect).";

                    // Only record a verified action as a proposed step; unverified no-op clicks
                    // shouldn't be suggested back to the user as real, reproducible steps.
                    if (hadEffect)
                    {
                        // Capture the CONCRETE action alongside the human description so that applying
                        // these steps yields a deterministically replayable script — a later RUN can then
                        // execute it with plain Playwright, with no MCP server and no AI tokens.
                        var script = SerializeRecordedActions(
                        [
                            new RecordedStepAction
                            {
                                Kind = action!,
                                Target = action == "navigate" ? mcp.CurrentUrl : decision.Target,
                                Value = decision.Value,
                            },
                        ]);

                        discovered.Add(new ProposedStep
                        {
                            Order = order,
                            Action = desc,
                            ExpectedResult = CleanExpectedResult(decision.ExpectedResult),
                            RecordedActionsJson = script,
                        });
                    }

                    // Bring the element we just acted on into frame so the screenshot is evidence of
                    // THIS action, not of whatever sits at the top of a long page. No-ops when the
                    // element is already visible or when the action navigated away from it.
                    if (!string.IsNullOrWhiteSpace(decision.Ref) && action is "click" or "type" or "select")
                    {
                        await mcp.ScrollIntoViewAsync(decision.Target ?? "element", decision.Ref!, ct);
                    }

                    var screenshotPath = await CaptureMcpStepScreenshotAsync(session, mcp, ct);
                    _db.ScenarioStepResults.Add(new ScenarioStepResult
                    {
                        TenantId = session.TenantId,
                        ProjectId = session.ProjectId,
                        SessionId = session.Id,
                        ScenarioId = scenario.Id,
                        StepOrder = order,
                        Action = desc,
                        Status = stepStatus,
                        Detail = stepDetail,
                        Url = mcp.CurrentUrl,
                        ScreenshotPath = screenshotPath,
                    });
                    await _db.SaveChangesAsync(ct);

                    await PublishStepAsync(session, scenario, new ScenarioStep { Order = order, Action = desc }, stepStatus, stepDetail ?? string.Empty, mcp.CurrentUrl, ct);
                    await PublishLogAsync(
                        session,
                        hadEffect ? $"  ✓ {order}. {desc}" : $"  ⚠ {order}. {desc} (no visible effect)",
                        hadEffect ? "success" : "warn",
                        ct,
                        logId);
                    order++;
                    recorded = true;
                }
            }

            // The turn produced no recorded step (the action was rejected, was housekeeping such as an
            // Escape keypress, or simply repeated the previous one). Rewrite the announcement so the log
            // never leaves a step sitting at "in progress" forever.
            if (!recorded)
            {
                await PublishLogAsync(
                    session,
                    performed ? $"  ↷ {pendingDesc} — not recorded as a step." : $"  ✗ {pendingDesc} — could not be performed.",
                    performed ? "info" : "warn",
                    ct,
                    logId);
            }

            history.AppendLine($"- turn {turn}: {action} -> {observation}. (thought: {TrimThought(decision.Thought)})");
            await PublishStatusAsync(session, mcp.CurrentUrl, ct);
        }

        if (!decided)
        {
            var (ok, reason) = await VerifyOutcomeAsync(session, missionStep, mcp, TrimSnapshot(await mcp.SnapshotAsync(ct), goalText), ct);
            reachedGoal = ok;
            goalReason = reason;
        }

        // The loop above only records a result when the agent performs a BROWSER ACTION. Assertion-only
        // steps ("Read the results summary line and check the total does not exceed 10000") perform no
        // action, so they used to end the session with no verdict at all — the UI showed them as an empty
        // circle, neither passed nor failed. Judge every authored step that never got a result against the
        // FINAL page state so the scenario always reports a complete pass/fail picture.
        await VerifyUnactionedStepsAsync(session, mcp, scenario, hintSteps, order, ct);

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

    /// <summary>
    /// Gives a verdict to the authored steps the goal-driven mission never produced a browser action for
    /// — chiefly assertion-only steps, which are pure validations and therefore CANNOT be covered by the
    /// action-recording path. Each one is checked against the FINAL page (same QA verifier the run engine
    /// uses), so a scenario never finishes with steps left in an ambiguous "no verdict" state.
    /// </summary>
    private async Task VerifyUnactionedStepsAsync(
        ExplorationSession session,
        PlaywrightMcpBrowser mcp,
        Scenario scenario,
        List<ScenarioStep> hintSteps,
        int nextOrder,
        CancellationToken ct)
    {
        var remaining = hintSteps.Where(s => s.Order >= nextOrder).OrderBy(s => s.Order).ToList();
        if (remaining.Count == 0)
        {
            return;
        }

        var snapshot = TrimSnapshot(
            await mcp.SnapshotAsync(ct),
            $"{scenario.Title} {string.Join(' ', remaining.Select(s => $"{s.Action} {s.ExpectedResult}"))}");
        var screenshotPath = await CaptureMcpStepScreenshotAsync(session, mcp, ct);

        foreach (var step in remaining)
        {
            ct.ThrowIfCancellationRequested();

            StepRunStatus status;
            string detail;
            if (string.IsNullOrWhiteSpace(step.ExpectedResult))
            {
                // Nothing to do and nothing to assert — mark it explicitly so it isn't mistaken for a
                // step the engine forgot about.
                status = StepRunStatus.Skipped;
                detail = "No browser action was needed and the step declares no expected result to validate.";
            }
            else
            {
                var (ok, reason) = await VerifyOutcomeAsync(session, step, mcp, snapshot, ct);
                status = ok ? StepRunStatus.Passed : StepRunStatus.Failed;
                detail = ok
                    ? $"Validated against the final page: {reason}"
                    : $"Validation failed on the final page: {reason}";
            }

            var action = ResolveVars(step.Action);
            _db.ScenarioStepResults.Add(new ScenarioStepResult
            {
                TenantId = session.TenantId,
                ProjectId = session.ProjectId,
                SessionId = session.Id,
                ScenarioId = scenario.Id,
                StepOrder = step.Order,
                Action = action,
                Status = status,
                Detail = detail,
                Url = mcp.CurrentUrl,
                ScreenshotPath = screenshotPath,
            });
            await _db.SaveChangesAsync(ct);

            await PublishStepAsync(session, scenario, step, status, detail, mcp.CurrentUrl, ct);
            await PublishLogAsync(
                session,
                status switch
                {
                    StepRunStatus.Passed => $"  ✓ {step.Order}. {action} (validation)",
                    StepRunStatus.Failed => $"  ✗ {step.Order}. {action} (validation) — {detail}",
                    _ => $"  ↷ {step.Order}. {action} (no action, no validation)",
                },
                status switch
                {
                    StepRunStatus.Passed => "success",
                    StepRunStatus.Failed => "error",
                    _ => "info",
                },
                ct);
        }
    }

    /// <summary>
    /// Puts a session into a terminal Failed state when the AI provider — not the application under test
    /// — is what broke, and says so in plain language. Without this the run surfaced only the symptom
    /// ("outcome could not be verified"), which reads as an ambiguous page and sends testers hunting for
    /// a defect that does not exist.
    /// </summary>
    private async Task ReportAiOutageAsync(ExplorationSession session, LlmUnavailableException ex, CancellationToken ct)
    {
        _logger.LogError(ex, "Exploration session {SessionId} stopped: the AI provider is unavailable.", session.Id);

        session.Status = ExplorationStatus.Failed;
        session.ErrorMessage = Truncate($"AI provider unavailable: {ex.Message}", 500);

        await PublishLogAsync(session, $"{Noun(session)} stopped — the AI provider is unavailable: {ex.Message}", "error", ct);
        await PublishLogAsync(
            session,
            "This is an infrastructure problem on our side, not a defect in the application under test. "
            + "Steps that never ran are marked Skipped rather than Failed. Check the AI credentials, quota and endpoint, then run again.",
            "warn",
            ct);
    }

    /// <summary>
    /// Records a not-run verdict for every authored step that never produced a result before the AI
    /// became unavailable. They are <see cref="StepRunStatus.Skipped"/> on purpose: the app was never
    /// exercised, so calling them Failed would report a defect that was never observed.
    /// </summary>
    private async Task BlockUntestedScenarioStepsAsync(
        ExplorationSession session,
        Scenario scenario,
        List<ScenarioStep> steps,
        string? currentUrl,
        LlmUnavailableException ex,
        CancellationToken ct)
    {
        if (steps.Count == 0)
        {
            return;
        }

        // Whatever the mission already judged stands; only fill the gaps it left behind.
        var judgedOrders = await _db.ScenarioStepResults
            .IgnoreQueryFilters()
            .Where(r => r.SessionId == session.Id && r.ScenarioId == scenario.Id)
            .Select(r => r.StepOrder)
            .ToListAsync(ct);

        var untested = steps.Where(s => !judgedOrders.Contains(s.Order)).ToList();
        if (untested.Count == 0)
        {
            return;
        }

        var detail = Truncate($"Not executed — the AI provider became unavailable: {ex.Message}", 500);

        foreach (var step in untested)
        {
            var action = ResolveVars(step.Action);
            _db.ScenarioStepResults.Add(new ScenarioStepResult
            {
                TenantId = session.TenantId,
                ProjectId = session.ProjectId,
                SessionId = session.Id,
                ScenarioId = scenario.Id,
                StepOrder = step.Order,
                Action = action,
                Status = StepRunStatus.Skipped,
                Detail = detail,
                Url = currentUrl,
            });

            await PublishStepAsync(session, scenario, step, StepRunStatus.Skipped, detail, currentUrl, ct);
            await PublishLogAsync(session, $"  ↷ {step.Order}. {action} — not executed (AI provider unavailable).", "warn", ct);
        }

        await _db.SaveChangesAsync(ct);
    }

    private static string Truncate(string value, int max) =>
        value.Length <= max ? value : value[..max];


    /// (1) the environment's Postman-style key/value variables (Environment.VariablesJson);
    /// (2) test data sets — each dataset's FIRST row supplies values, exposed bare (<c>{{username}}</c>,
    /// first dataset wins) and dataset-qualified (<c>{{Checkout users.username}}</c>).
    /// </summary>
    private async Task<Dictionary<string, string?>> LoadEnvironmentVariablesAsync(
        ExplorationSession session,
        CancellationToken ct)
    {
        var vars = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);
        try
        {
            // (1) Environment key/value variables (highest precedence).
            var environment = await _db.Environments
                .IgnoreQueryFilters()
                .FirstOrDefaultAsync(e => e.Id == session.EnvironmentId, ct);

            if (!string.IsNullOrWhiteSpace(environment?.VariablesJson))
            {
                foreach (var kvp in EnvironmentVariableSerialization.ParseValues(environment.VariablesJson))
                {
                    vars[kvp.Key] = kvp.Value;
                }
            }

            // (2) Test data sets (fill any gaps; qualified keys always added).
            var sets = await _db.TestDataSets
                .IgnoreQueryFilters()
                .Where(d => d.EnvironmentId == session.EnvironmentId)
                .ToListAsync(ct);

            foreach (var set in sets)
            {
                var columns = JsonSerializer.Deserialize<List<string>>(set.ColumnsJson, JsonOpts) ?? [];
                var rows = JsonSerializer.Deserialize<List<Dictionary<string, string?>>>(set.RowsJson, JsonOpts) ?? [];
                var first = rows.FirstOrDefault();

                foreach (var column in columns)
                {
                    var value = first is not null && first.TryGetValue(column, out var v) ? v : null;
                    vars[$"{set.Name}.{column}"] = value;
                    if (!vars.ContainsKey(column))
                    {
                        vars[column] = value;
                    }
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Failed to load variables for environment {EnvironmentId}.", session.EnvironmentId);
        }

        return vars;
    }

    /// <summary>Replaces <c>{{key}}</c> tokens with values bound from the environment's test data.
    /// Unknown tokens are left as-is so they remain visible instead of silently blanking a field.</summary>
    private string ResolveVars(string text)
    {
        if (string.IsNullOrEmpty(text) || _variables.Count == 0
            || text.IndexOf("{{", StringComparison.Ordinal) < 0)
        {
            return text;
        }

        return VariableTokenRegex.Replace(text, match =>
        {
            var key = match.Groups[1].Value.Trim();
            return _variables.TryGetValue(key, out var value) && value is not null ? value : match.Value;
        });
    }

    private string? ResolveVarsOrNull(string? text) => text is null ? null : ResolveVars(text);

    private static string BuildScenarioMission(Scenario scenario, List<ScenarioStep> hintSteps, string? effectiveExpectedResult)
    {
        var sb = new StringBuilder();
        sb.AppendLine("Accomplish this test scenario by exploring and interacting with the REAL web application in front of you.");
        sb.AppendLine($"Scenario: {scenario.Title}");
        if (!string.IsNullOrWhiteSpace(scenario.Preconditions))
        {
            sb.AppendLine($"Preconditions (the required STARTING STATE): {scenario.Preconditions}");
            sb.AppendLine("You start on the site's landing/home page, so the preconditions are NOT met yet — FIRST establish them yourself by navigating through the real UI (e.g. run a search or open the relevant section/product) until you reach that starting state, THEN begin the steps below. To reach a product listing / search results, use the SEARCH box (type a query and submit); do NOT click promotional banners, sale-campaign tiles or hero images, which lead to marketing pages without the standard filters/controls.");
        }
        if (!string.IsNullOrWhiteSpace(effectiveExpectedResult))
        {
            sb.AppendLine($"Intended outcome: {effectiveExpectedResult}");
        }
        if (hintSteps.Count > 0)
        {
            var flow = hintSteps.Select((s, i) => string.IsNullOrWhiteSpace(s.ExpectedResult)
                ? $"{i + 1}) {s.Action}"
                : $"{i + 1}) {s.Action} (expect: {s.ExpectedResult})");
            sb.AppendLine($"Follow this intended flow IN ORDER, performing each step through the app's real UI: {string.Join("; ", flow)}.");
            sb.AppendLine("Carry out every one of those steps in sequence (after the preconditions are established). Only adapt a step when the corresponding control genuinely does not exist in the real UI (find the closest equivalent), and only skip one if it is truly impossible — never skip a step just to reach the end faster.");
        }
        sb.AppendLine("If a login/OTP, cookie, location or promotional popup or modal is blocking the page, close or dismiss it first (click its ✕/close/'Not now'/'Skip' control, or press Escape) — do NOT try to log in; continue as a guest.");
        sb.Append("Reproduce the intended end-to-end user journey via the real UI (do not take URL shortcuts that bypass steps). When every step is done and the intended outcome is reached — or it genuinely cannot be — finish.");
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

    /// <summary>
    /// Keypresses that are page housekeeping rather than test steps. Scroll keys are here because the
    /// snapshot already spans the whole document — scrolling reveals nothing new, so pressing them is
    /// always the agent flailing, never a step worth recording or replaying.
    /// </summary>
    private static readonly HashSet<string> HousekeepingKeys = new(StringComparer.OrdinalIgnoreCase)
    {
        "Escape", "Esc", "PageDown", "PageUp", "Home", "End",
    };

    /// <summary>The subset of <see cref="HousekeepingKeys"/> that merely moves the viewport.</summary>
    private static readonly HashSet<string> ScrollKeys = new(StringComparer.OrdinalIgnoreCase)
    {
        "PageDown", "PageUp", "Home", "End",
    };

    private static string DescribeMcpAction(string? action, StepAgentDecision decision, string currentUrl) => action switch
    {
        "navigate" => $"Navigate to {currentUrl}",
        "click" => $"Click \"{decision.Target ?? "element"}\"",
        "type" => $"Enter \"{decision.Value}\" into \"{decision.Target ?? "field"}\"",
        "press" => $"Press {decision.Target ?? "Enter"}",
        "select" => $"Select \"{decision.Value}\" in the \"{decision.Target ?? "dropdown"}\" dropdown",
        _ => decision.Action ?? "action",
    };

    /// <summary>The longest element descriptor worth keeping: enough to identify a control, not a page dump.</summary>
    private const int MaxTargetDescriptorLength = 80;

    private static readonly Regex WhitespaceRunPattern = new(
        @"\s+",
        RegexOptions.CultureInvariant,
        TimeSpan.FromSeconds(1));

    /// <summary>
    /// Reduces the accessible name of a composite control to a short, stable descriptor.
    /// <para>
    /// This matters for correctness, not just readability. The descriptor is stored as the replay
    /// target, so every volatile fragment carried into it — a price, a review count, a "16% off"
    /// badge — is a guaranteed mismatch on the next run. Keeping the leading phrase keeps the part
    /// that actually identifies the control (a product's name, a link's label) and drops the
    /// surrounding text that merely happened to be nested inside it.
    /// </para>
    /// <para>
    /// Every rule here is site-agnostic: no currency symbols, no e-commerce vocabulary. Anything of
    /// that sort would work on one storefront and quietly mangle the next one.
    /// </para>
    /// </summary>
    internal static string? CondenseTargetDescriptor(string? target)
    {
        if (string.IsNullOrWhiteSpace(target))
        {
            return target;
        }

        var text = WhitespaceRunPattern.Replace(target.Trim(), " ");

        // MCP elides a long name with an ellipsis. What follows it is a sibling's text that got
        // concatenated in, never part of this control's own label.
        var ellipsis = text.IndexOf('…');
        if (ellipsis < 0)
        {
            ellipsis = text.IndexOf("...", StringComparison.Ordinal);
        }

        if (ellipsis > 0)
        {
            text = text[..ellipsis];
        }

        text = CollapseRepeatedPhrase(text);

        if (text.Length > MaxTargetDescriptorLength)
        {
            // Cut on a word boundary so the descriptor stays a readable phrase. If the boundary falls
            // implausibly early the text is one long token, so a hard cut is the honest option.
            var boundary = text.LastIndexOf(' ', MaxTargetDescriptorLength);
            text = boundary > MaxTargetDescriptorLength / 2
                ? text[..boundary]
                : text[..MaxTargetDescriptorLength];
        }

        return text.Trim().TrimEnd('-', '–', ',', '.', '|', '/').Trim();
    }

    /// <summary>
    /// Folds a name that is the same phrase twice back to once. An accessible name routinely doubles
    /// up when a control wraps an icon carrying the identical label ("Cart Cart"), and the repetition
    /// both reads badly and weakens descriptor matching.
    /// </summary>
    private static string CollapseRepeatedPhrase(string text)
    {
        var words = text.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (words.Length < 2 || words.Length % 2 != 0)
        {
            return text;
        }

        var half = words.Length / 2;
        return words.Take(half).SequenceEqual(words.Skip(half), StringComparer.OrdinalIgnoreCase)
            ? string.Join(' ', words.Take(half))
            : text;
    }

    /// <summary>
    /// Normalises the agent's expected-result text for storage on a proposed step: trims, drops
    /// non-answers ("n/a", "none"), and caps the length to the column limit. Returns null when the
    /// agent did not supply a meaningful assertion, so the field stays empty rather than noisy.
    /// </summary>
    private static string? CleanExpectedResult(string? expected)
    {
        if (string.IsNullOrWhiteSpace(expected))
        {
            return null;
        }

        var text = expected.Trim();
        if (text.Length < 3
            || text.Equals("n/a", StringComparison.OrdinalIgnoreCase)
            || text.Equals("na", StringComparison.OrdinalIgnoreCase)
            || text.Equals("none", StringComparison.OrdinalIgnoreCase)
            || text.Equals("unknown", StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        return text.Length > 1000 ? text[..1000] : text;
    }

    /// <summary>
    /// Reduces a large MCP accessibility snapshot to the header plus the most relevant interactive
    /// elements so heavy SPAs (e.g. Flipkart) don't blow up the LLM prompt — keeping decisions fast,
    /// cheap and focused on things the agent can actually act on.
    /// </summary>
    // Whether two accessibility snapshots describe the same page state, ignoring churny bits that
    // change between snapshots even when the user-visible page did not (e.g. the [ref=eN] handles MCP
    // re-assigns on every capture, and incidental whitespace). Used to detect clicks that "landed" but
    // had no observable effect so we don't record them as false passes.
    private static bool SnapshotsEquivalent(string a, string b)
        => string.Equals(NormalizeSnapshotForCompare(a), NormalizeSnapshotForCompare(b), StringComparison.Ordinal);

    private static string NormalizeSnapshotForCompare(string snapshot)
    {
        if (string.IsNullOrEmpty(snapshot))
        {
            return string.Empty;
        }

        // Drop the volatile ref handles, then collapse all whitespace so formatting jitter is ignored.
        var withoutRefs = System.Text.RegularExpressions.Regex.Replace(snapshot, @"\[ref=e\d+\]", string.Empty);
        return System.Text.RegularExpressions.Regex.Replace(withoutRefs, @"\s+", " ").Trim();
    }

    /// <summary>
    /// Words that carry no identifying power when matching a step's wording against page elements —
    /// mostly the verbs and connectives every step is written with.
    /// </summary>
    private static readonly HashSet<string> SnapshotStopWords = new(StringComparer.OrdinalIgnoreCase)
    {
        "the", "and", "for", "with", "that", "this", "from", "into", "then", "when", "should", "must",
        "user", "page", "click", "clicks", "clicking", "select", "selects", "enter", "enters", "type",
        "types", "press", "verify", "verifies", "check", "checks", "ensure", "displayed", "display",
        "shown", "show", "shows", "see", "sees", "are", "was", "were", "have", "has", "not", "all",
        "any", "new", "using", "via", "their", "there", "which", "each", "some",
    };

    /// <summary>
    /// Distinctive words from the step/goal text, used to spot the elements a step is actually about.
    /// </summary>
    private static string[] GoalTokens(string? goal)
    {
        if (string.IsNullOrWhiteSpace(goal))
        {
            return [];
        }

        return goal
            .Split((char[])['\n', '\r', '\t', ' ', '.', ',', ';', ':', '"', '\'', '(', ')', '[', ']', '/', '\\', '-', '_', '!', '?'], StringSplitOptions.RemoveEmptyEntries)
            .Select(w => w.Trim().ToLowerInvariant())
            .Where(w => w.Length >= 3 && !SnapshotStopWords.Contains(w))
            .Distinct(StringComparer.Ordinal)
            .Take(14)
            .ToArray();
    }

    /// <summary>
    /// Shrinks a huge page snapshot to fit the model's budget WITHOUT hiding the control the step needs.
    /// Retention is by relevance, not document position. This matters enormously on real content pages:
    /// a Flipkart product page snapshots to ~1080 lines, and its "Add to cart" control sits on line 715
    /// behind ~390 earlier interactive elements (galleries, offers, reviews, recommendations). A naive
    /// "keep the first N lines" trim exhausted its budget around line 300, so the agent never saw the
    /// button, reported it missing, and burned its remaining turns scrolling for something that was
    /// never below the fold — it was simply cut from the snapshot.
    ///
    /// Priority order: page header, then elements whose text matches the step's own wording, then real
    /// controls (buttons, inputs, dropdowns…), then everything else. Output stays in document order so
    /// nesting and section grouping still read correctly.
    /// </summary>
    private static string TrimSnapshot(string snapshot, string? goal = null, int maxInterestingLines = 240, int maxChars = 12000)
    {
        if (string.IsNullOrEmpty(snapshot) || snapshot.Length <= maxChars)
        {
            return snapshot;
        }

        var tokens = GoalTokens(goal);
        var lines = snapshot.Split('\n');
        var header = new List<int>();
        var relevant = new List<(int Index, int Score)>();
        var primary = new List<int>();
        var secondary = new List<int>();

        for (var i = 0; i < lines.Length; i++)
        {
            var line = lines[i];

            if (header.Count < 8 &&
                (line.StartsWith("###", StringComparison.Ordinal)
                 || line.Contains("Page URL", StringComparison.Ordinal)
                 || line.Contains("Page Title", StringComparison.Ordinal)
                 || line.TrimStart().StartsWith("```", StringComparison.Ordinal)))
            {
                header.Add(i);
                continue;
            }

            var actionable = line.Contains("[ref=", StringComparison.Ordinal);

            // Elements the STEP is about. Custom widgets are why this bucket exists: a div-based control
            // has no button role, so role-based ranking alone would never rescue it — but its label still
            // echoes the step's wording. Ranked by how many distinct step words it matches, so "Add to
            // cart" outranks the dozens of cards that merely match "product".
            if (actionable && tokens.Length > 0)
            {
                var score = tokens.Count(t => line.Contains(t, StringComparison.OrdinalIgnoreCase));
                if (score > 0)
                {
                    relevant.Add((i, score));
                    continue;
                }
            }

            // Controls that CARRY OUT a step — the ones a link-heavy page would otherwise crowd out.
            var isPrimary = line.Contains("button", StringComparison.Ordinal)
                || line.Contains("textbox", StringComparison.Ordinal)
                || line.Contains("searchbox", StringComparison.Ordinal)
                || line.Contains("combobox", StringComparison.Ordinal)
                || line.Contains("listbox", StringComparison.Ordinal)
                || line.Contains("option", StringComparison.Ordinal)
                || line.Contains("checkbox", StringComparison.Ordinal)
                || line.Contains("radio", StringComparison.Ordinal)
                || line.Contains("switch", StringComparison.Ordinal)
                || line.Contains("slider", StringComparison.Ordinal)
                || line.Contains("spinbutton", StringComparison.Ordinal)
                || line.Contains("menuitem", StringComparison.Ordinal)
                || line.Contains("tab", StringComparison.Ordinal)
                || line.Contains("dialog", StringComparison.Ordinal)
                || line.Contains("cursor=pointer", StringComparison.Ordinal)
                || line.Contains("heading ", StringComparison.Ordinal);

            if (isPrimary)
            {
                primary.Add(i);
            }
            else if (actionable || line.Contains("link", StringComparison.Ordinal))
            {
                secondary.Add(i);
            }
        }

        var budget = maxChars;
        var keep = new SortedSet<int>();
        foreach (var i in header)
        {
            keep.Add(i);
            budget -= lines[i].Length + 1;
        }

        // Relevance first (best-matching first), then real controls, then the remainder. The relevance
        // bucket is capped so a broad word in the step ("product" on a catalogue) cannot swallow the
        // whole budget and starve the genuine controls.
        var ranked = relevant.OrderByDescending(r => r.Score).ThenBy(r => r.Index).Select(r => r.Index).ToList();
        var buckets = new[]
        {
            (Lines: ranked, MaxLines: Math.Max(40, maxInterestingLines / 2), MaxChars: maxChars / 2),
            (Lines: primary, MaxLines: maxInterestingLines, MaxChars: maxChars),
            (Lines: secondary, MaxLines: maxInterestingLines, MaxChars: maxChars),
        };

        foreach (var (bucketLines, bucketMaxLines, bucketMaxChars) in buckets)
        {
            var bucketKept = 0;
            var bucketChars = 0;
            foreach (var i in bucketLines)
            {
                if (bucketKept >= bucketMaxLines
                    || bucketChars >= bucketMaxChars
                    || keep.Count - header.Count >= maxInterestingLines
                    || budget <= 0)
                {
                    break;
                }

                keep.Add(i);
                bucketKept++;
                bucketChars += lines[i].Length + 1;
                budget -= lines[i].Length + 1;
            }
        }

        var sb = new StringBuilder();
        foreach (var i in keep)
        {
            sb.AppendLine(lines[i]);
        }

        var kept = keep.Count - header.Count;
        var dropped = relevant.Count + primary.Count + secondary.Count - kept;
        sb.AppendLine($"… (snapshot trimmed to {kept} elements most relevant to this step; {dropped} lower-value element(s) omitted)");
        return sb.ToString();
    }

    /// <summary>Broadcasts a single scenario step's result to the tenant's live-view group as it executes.</summary>
    private Task PublishStepAsync(
        ExplorationSession session,
        Scenario scenario,
        ScenarioStep step,
        StepRunStatus status,
        string? detail,
        string? url,
        CancellationToken ct) =>
        PublishStepAsync(session, scenario, step, status.ToString(), detail, url, ct);

    /// <summary>
    /// Same as above but with a free-form status, so a step can also be announced as <c>"Running"</c>
    /// the moment it starts — a state that has no <see cref="StepRunStatus"/> member because it is
    /// never persisted, only streamed.
    /// </summary>
    private async Task PublishStepAsync(
        ExplorationSession session,
        Scenario scenario,
        ScenarioStep step,
        string status,
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
                    status,
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
    private async Task PublishLogAsync(ExplorationSession session, string message, string level, CancellationToken ct, string? id = null)
    {
        try
        {
            await _live.PublishLogAsync(
                session.TenantId,
                session.Id,
                new ExplorationLogEntry(level, message, _clock.UtcNow, id),
                ct);
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Failed to publish live log for session {SessionId}.", session.Id);
        }
    }

    /// <summary>
    /// Announces which test the run is on. Sent when a scenario starts and again with its verdict, so
    /// the live view can walk a suite one test at a time instead of showing an undifferentiated stream
    /// of steps whose numbering silently restarts at each scenario boundary.
    /// </summary>
    private async Task PublishScenarioAsync(
        ExplorationSession session,
        Scenario scenario,
        int index,
        int total,
        int stepCount,
        string status,
        CancellationToken ct)
    {
        try
        {
            await _live.PublishScenarioAsync(
                session.TenantId,
                session.Id,
                new ScenarioProgressLiveUpdate(scenario.Id, scenario.Title, index, total, stepCount, status),
                ct);
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Failed to publish live scenario progress for session {SessionId}.", session.Id);
        }
    }

    // Signature of the tab strip last sent to viewers, so an unchanged strip isn't re-broadcast on
    // every turn. Per-agent-instance state; each session runs on its own scoped agent.
    private string _lastPublishedTabs = string.Empty;

    /// <summary>
    /// What this session is, for user-facing log lines. A session bound to a scenario or suite is a
    /// "Run" — reporting "Exploration completed." at the end of a regression run is simply wrong.
    /// </summary>
    private static string Noun(ExplorationSession session) =>
        session.ScenarioId is not null || session.SuiteId is not null ? "Run" : "Exploration";

    /// <summary>
    /// Streams the browser's open tabs to the live view, but only when they actually changed — the
    /// strip is re-read every turn and is identical the vast majority of the time.
    /// </summary>
    private async Task PublishTabsIfChangedAsync(ExplorationSession session, IReadOnlyList<BrowserTabInfo> tabs, CancellationToken ct)
    {
        try
        {
            var signature = string.Join("|", tabs.Select(t => $"{t.Index}:{(t.IsActive ? "*" : "")}{t.Url}"));
            if (signature == _lastPublishedTabs)
            {
                return;
            }

            _lastPublishedTabs = signature;
            await _live.PublishTabsAsync(session.TenantId, session.Id, tabs, ct);
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Failed to publish live tabs for session {SessionId}.", session.Id);
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

            // Optional LLM step: find hidden content to interact with.
            if (_llm.IsLive)
            {
                await PublishLogAsync(session, "  • Asking AI to reveal hidden content (modals, menus)…", "info", ct);
                await TryExpandDynamicContentAsync(discoveredPage, browser, a11yJson, ct);
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

        // A filter narrows the suite to the cases the user picked (any value within a facet, all
        // facets together). Applied here rather than at queue time so the run reflects the
        // scenarios as they are now.
        var filter = ScenarioRunFilter.Deserialize(session.RunFilterJson);
        if (!filter.IsEmpty)
        {
            var total = ordered.Count;
            ordered = ordered.Where(filter.Matches).ToList();
            await PublishLogAsync(
                session,
                $"Filter ({filter.Describe()}) — running {ordered.Count} of {total} test(s) in the suite.",
                "info",
                ct);
        }

        // Sequential run in a single shared browser session: navigate once, then walk each
        // scenario's steps so state carries over between scenarios.
        await browser.NavigateAsync(baseUrl, ct);

        await PublishLogAsync(session, $"Suite '{suite.Name}' — {ordered.Count} test(s) to run.", "info", ct);

        for (var i = 0; i < ordered.Count; i++)
        {
            if (ct.IsCancellationRequested)
            {
                break;
            }

            await WalkScenarioStepsAsync(session, browser, baseUrl, ordered[i], ct, i + 1, ordered.Count);
        }

        session.Status = ExplorationStatus.Completed;
    }

    /// <summary>Executes every step of a single scenario against the current browser page, recording per-step results.</summary>
    /// <param name="index">1-based position of this scenario in the run; 1 for a single-scenario run.</param>
    /// <param name="total">Number of scenarios the run will execute.</param>
    private async Task WalkScenarioStepsAsync(
        ExplorationSession session,
        PlaywrightBrowserService browser,
        string baseUrl,
        Scenario scenario,
        CancellationToken ct,
        int index = 1,
        int total = 1)
    {
        var orderedSteps = scenario.Steps
            .OrderBy(s => s.Order)
            .ToList();

        if (orderedSteps.Count == 0)
        {
            await PublishScenarioAsync(session, scenario, index, total, 0, nameof(StepRunStatus.Skipped), ct);
            return;
        }

        // Per-scenario auto-heal policy governs whether this run may fall back to the AI at all.
        _autoHealEnabled = scenario.AutoHealEnabled;

        var recordedCount = orderedSteps.Count(s => !string.IsNullOrWhiteSpace(s.RecordedActionsJson));
        await PublishScenarioAsync(session, scenario, index, total, orderedSteps.Count, "Running", ct);
        await PublishLogAsync(
            session,
            total > 1
                ? $"Test {index} of {total}: '{scenario.Title}' — {orderedSteps.Count} step(s)"
                : $"Running scenario '{scenario.Title}' — {orderedSteps.Count} step(s)",
            "info",
            ct);
        await PublishLogAsync(
            session,
            $"Deterministic execution (direct Playwright — no AI): {recordedCount}/{orderedSteps.Count} step(s) have a recorded script · auto-heal {(scenario.AutoHealEnabled ? "ON" : "OFF")}.",
            "info",
            ct);

        var currentUrl = browser.CurrentUrl;

        // Worst step outcome seen so far — the scenario's verdict. Ordered so that a later, worse
        // result always wins: one failed step fails the test however many steps passed around it.
        var verdict = StepRunStatus.Passed;
        var executed = 0;

        // Send the strip before the first step runs. Steps can take tens of seconds, and publishing
        // only on completion left the live view with no tab strip for the whole of step 1.
        await PublishTabsIfChangedAsync(session, await browser.ListTabsAsync(), ct);

        foreach (var step in orderedSteps)
        {
            if (ct.IsCancellationRequested)
            {
                break;
            }

            await PublishLogAsync(session, $"→ Step {step.Order}: {step.Action}", "info", ct);
            var result = await ExecuteScenarioStepAsync(session, browser, baseUrl, step, ct);
            var screenshotPath = await CaptureBrowserStepScreenshotAsync(session, browser, ct);
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
                ChecksJson = result.Checks is { Count: > 0 } checks ? JsonSerializer.Serialize(checks, JsonOpts) : null,
                ScreenshotPath = screenshotPath,
                Url = browser.CurrentUrl ?? currentUrl,
            });

            executed++;
            if (WorseThan(result.Status, verdict))
            {
                verdict = result.Status;
            }

            currentUrl = browser.CurrentUrl;
            await _db.SaveChangesAsync(ct);
            await PublishStepAsync(session, scenario, step, result.Status, result.Detail, browser.CurrentUrl ?? currentUrl, ct);
            await PublishLogAsync(
                session,
                $"  {StepStatusGlyph(result.Status)} Step {step.Order} {result.Status}: {result.Detail}",
                result.Status == StepRunStatus.Failed ? "error" : result.Status == StepRunStatus.Healed ? "warn" : "success",
                ct);
            await PublishStatusAsync(session, browser.CurrentUrl, ct);
            await PublishTabsIfChangedAsync(session, await browser.ListTabsAsync(), ct);
        }

        // A run stopped part-way through has no verdict to report — saying "Passed" because the steps
        // that did run passed would be a lie about the ones that never ran.
        var final = executed < orderedSteps.Count ? nameof(StepRunStatus.Skipped) : verdict.ToString();
        await PublishScenarioAsync(session, scenario, index, total, orderedSteps.Count, final, ct);
        await PublishLogAsync(
            session,
            $"{StepStatusGlyph(verdict)} '{scenario.Title}' — {final} ({executed}/{orderedSteps.Count} step(s) executed)",
            final == nameof(StepRunStatus.Failed) ? "error" : final == nameof(StepRunStatus.Passed) ? "success" : "warn",
            ct);
    }

    /// <summary>Ranks step outcomes so a scenario's verdict is the worst result any of its steps produced.</summary>
    private static bool WorseThan(StepRunStatus candidate, StepRunStatus current) => Rank(candidate) > Rank(current);

    private static int Rank(StepRunStatus status) => status switch
    {
        StepRunStatus.Passed => 0,
        StepRunStatus.Healed => 1,
        StepRunStatus.Skipped => 2,
        StepRunStatus.Failed => 3,
        _ => 0,
    };

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
        var missionText = session.Prompt!;
        var history = new StringBuilder();
        var recorded = new List<(string Action, string? Expected)>();
        var missionOk = false;

        for (var turn = 1; turn <= maxTurns && !ct.IsCancellationRequested; turn++)
        {
            var pageUrl = string.IsNullOrWhiteSpace(browser.CurrentUrl) ? baseUrl : browser.CurrentUrl;
            var snapshot = await browser.SnapshotForAgentAsync(60, ct);

            var decision = await DecideMissionActionAsync(missionText, pageUrl, snapshot, history.ToString(), turn, maxTurns, ct);
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
            if (ok && action is "navigate" or "click" or "type" or "press" or "select")
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
                MissionAgentPrompts.BuildUserPrompt(mission, pageUrl, snapshot, history, turn, maxTurns, _businessContext),
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
                    var navigated = await RunAgentActionAsync(TestActionKind.Navigate, target: null, value: url, ct);
                    return navigated.Ok
                        ? (true, $"navigated to {browser.CurrentUrl}")
                        : (false, $"could not navigate to {url}");

                case "click":
                    var clickTarget = await BuildAgentTargetAsync(browser, decision.Ref, decision.Target, ct);
                    var clicked = await RunAgentActionAsync(TestActionKind.Click, clickTarget, value: null, ct);
                    return clicked.Ok
                        ? (true, $"clicked \"{clickTarget}\"")
                        : (false, $"could not click {decision.Ref ?? decision.Target}");

                case "type":
                    var value = decision.Value ?? string.Empty;
                    var typeTarget = await BuildAgentTargetAsync(browser, decision.Ref, decision.Target, ct);
                    var typed = await RunAgentActionAsync(TestActionKind.Type, typeTarget, value, ct);
                    return typed.Ok
                        ? (true, $"typed '{value}' into \"{typeTarget}\"")
                        : (false, $"could not find input {decision.Ref ?? decision.Target}");

                case "press":
                    var key = string.IsNullOrWhiteSpace(decision.Target) ? "Enter" : decision.Target!;
                    var pressed = await RunAgentActionAsync(TestActionKind.Press, target: null, value: key, ct);
                    return (pressed.Ok, pressed.Ok ? $"pressed {key}" : $"could not press {key}");

                case "select":
                    var option = decision.Value ?? string.Empty;
                    var selectTarget = await BuildAgentTargetAsync(browser, decision.Ref, decision.Target, ct);
                    var selected = await RunAgentActionAsync(TestActionKind.Select, selectTarget, option, ct);
                    return selected.Ok
                        ? (true, $"selected '{option}' in \"{selectTarget}\"")
                        : (false, $"could not select '{option}' in {decision.Target}");

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
        "select" => $"Select \"{d.Value}\" in the {d.Target ?? "dropdown"} dropdown",
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

    private async Task<StepOutcome> ExecuteScenarioStepAsync(
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

        // Deterministic fast-path: a plain "Navigate to <url>" step is fully described by its own text,
        // so it never needs the AI (or a prior recording) to execute. Record it on first sight so every
        // later run replays it like any other scripted action.
        if (TryParseNavigationStep(step.Action, baseUrl, out var navigationUrl))
        {
            var navigationActions = new List<RecordedStepAction>
            {
                new() { Kind = "navigate", Target = navigationUrl },
            };

            step.RecordedActionsJson = SerializeRecordedActions(navigationActions);
            step.NeedsReview = false;
            step.ReviewReason = null;
            await _db.SaveChangesAsync(ct);
            await PublishLogAsync(session, $"  ⤓ Step {step.Order} is a plain navigation — recorded without AI.", "info", ct);

            return await ReplayRecordedStepAsync(session, browser, baseUrl, step, navigationActions, ct);
        }

        // No recording yet. With auto-heal OFF the run must stay fully deterministic — we will not spend
        // AI tokens teaching the agent this step. Report it as an actionable failure instead.
        if (!_autoHealEnabled)
        {
            var noScript = "This step has no recorded automation yet. Explore the scenario to ground its steps, or enable auto-heal so the run can learn this step.";
            step.NeedsReview = true;
            step.ReviewReason = noScript;
            await _db.SaveChangesAsync(ct);
            await PublishLogAsync(session, $"  ⚠ Step {step.Order} has no recorded actions and auto-heal is off — skipping AI.", "warn", ct);
            return (StepRunStatus.Failed, noScript);
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
    private async Task<StepOutcome> ReplayRecordedStepAsync(
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

            var (ok, failureDetail) = await ExecuteRecordedActionAsync(browser, action, baseUrl, ct);
            if (ok)
            {
                continue;
            }

            // Healing means re-locating an element, so it only applies to actions that address one.
            // An API call or a SQL statement that fails has failed on its own terms — the driver's
            // message is the real diagnosis, and re-running the AI over a page snapshot could not
            // possibly repair it.
            if (!RecordedActionTargetsElement(action))
            {
                var driverReason = failureDetail is { Length: > 0 }
                    ? failureDetail
                    : $"The {action.Platform} step '{action.Kind}' did not succeed.";

                step.NeedsReview = true;
                step.ReviewReason = driverReason;
                await _db.SaveChangesAsync(ct);
                await PublishLogAsync(session, $"  ✗ Step {step.Order}: {driverReason}", "error", ct);
                return (StepRunStatus.Failed, driverReason);
            }

            // The recorded locator failed. This is the ONLY point in a run where the AI may be used, and
            // only when the scenario opts in — otherwise the run stays a pure deterministic replay.
            if (!_autoHealEnabled)
            {
                var offReason = $"Could not locate '{action.Target}' to {action.Kind} for this step, and auto-heal is disabled for this scenario.";
                step.NeedsReview = true;
                step.ReviewReason = offReason;
                await _db.SaveChangesAsync(ct);
                await PublishLogAsync(session, $"  ✗ Step {step.Order}: {offReason}", "error", ct);
                return (StepRunStatus.Failed, offReason);
            }

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

        var expectation = ResolveVars(step.ExpectedResult ?? string.Empty);

        // Preferred path: the expectation has already been compiled into assertions, so the verdict is
        // reached by reading values off the page and comparing them. No AI, and the same page always
        // produces the same answer.
        var compiled = CompiledAssertionsFor(recorded, expectation);
        if (compiled.Count > 0)
        {
            await browser.WaitForPageSettledAsync(ct: ct);

            var checks = new List<StepCheckResult>(compiled.Count);
            foreach (var assertion in compiled)
            {
                checks.Add(await AssertionEvaluator.EvaluateAsync(browser, assertion, ct));
            }

            // An assertion whose selector no longer resolves is the assertion-level equivalent of a
            // stale locator: the application may well be fine and the test simply out of date. Failing
            // the step on that would be a false alarm, so — exactly as with auto-heal, and only with the
            // same consent — the stale assertions are dropped and the step re-verified and recompiled.
            var stale = checks.Any(c => c.Unresolved);
            if (stale && _autoHealEnabled && _llm.IsLive)
            {
                recorded.RemoveAll(a => string.Equals(a.Kind?.Trim(), "assert", StringComparison.OrdinalIgnoreCase));
                step.RecordedActionsJson = SerializeRecordedActions(recorded);
                await _db.SaveChangesAsync(ct);
                await PublishLogAsync(
                    session,
                    $"  ↻ Step {step.Order}: a compiled assertion no longer resolves on this page — re-verifying and recompiling.",
                    "warn",
                    ct);
            }
            else
            {
                foreach (var check in checks)
                {
                    await PublishLogAsync(
                        session,
                        $"  {(check.Passed ? "✓" : "✗")} {check.Label} — {check.Actual}",
                        check.Passed ? "info" : "error",
                        ct);
                }

                var failedChecks = checks.Where(c => !c.Passed).ToList();
                if (failedChecks.Count > 0)
                {
                    return new StepOutcome(
                        StepRunStatus.Failed,
                        $"Actual: {string.Join("; ", failedChecks.Select(f => f.Actual))}",
                        checks);
                }

                return new StepOutcome(
                    anyHealed ? StepRunStatus.Healed : StepRunStatus.Passed,
                    anyHealed
                        ? $"Replayed the recorded actions, healed a changed locator, and verified {checks.Count} assertion(s) without AI."
                        : $"Replayed the recorded actions and verified {checks.Count} assertion(s) without AI.",
                    checks);
            }
        }

        var (matched, verificationReason) = await MatchesExpectedOutcomeAsync(session, browser, step, ct);

        // Compile the expectation now, while the page and the verdict for it are both in hand, so
        // future runs need no model. Compiling on a FAILING step is just as valuable as on a passing
        // one — it is how a broken requirement gets its per-assertion breakdown on the very first run.
        var freshChecks = await TryCompileAssertionsAsync(session, browser, step, recorded, expectation, matched, ct);

        // Replaying the recorded actions successfully is NOT the same as the step passing: the
        // clicks can all land while the application still does the wrong thing. The expected
        // result is the assertion, so a step whose outcome is not met fails even on a clean replay.
        if (!matched)
        {
            // The reason already leads with "Actual: …"; prefixing another "the expected result was not
            // met" would say the same thing twice before the reader reaches the observation.
            return new StepOutcome(StepRunStatus.Failed, verificationReason, freshChecks);
        }

        var status = anyHealed ? StepRunStatus.Healed : StepRunStatus.Passed;
        var detail = anyHealed
            ? "Replayed the recorded actions and healed a changed locator; the expected result is present."
            : "Replayed the recorded actions; the expected result is present.";
        return new StepOutcome(status, detail, freshChecks);
    }

    /// <summary>
    /// The step's compiled assertions, but only those still describing the CURRENT expected result.
    /// <para>
    /// An author who rewrites an expectation would otherwise keep silently checking the old one — the
    /// worst kind of test, one that is green about the wrong requirement. A mismatch simply drops the
    /// stale assertions, which sends the step back through verification and recompiles it.
    /// </para>
    /// </summary>
    private static List<StepAssertion> CompiledAssertionsFor(List<RecordedStepAction> recorded, string expectation) =>
        recorded
            .Where(a => string.Equals(a.Kind?.Trim(), "assert", StringComparison.OrdinalIgnoreCase)
                        && a.Assertion is not null
                        && string.Equals((a.CompiledFrom ?? string.Empty).Trim(), expectation.Trim(), StringComparison.Ordinal))
            .Select(a => a.Assertion!)
            .ToList();

    /// <summary>
    /// Turns a step's English expected result into stored, machine-checkable assertions.
    /// <para>
    /// The verdict for this page has just been established, which is what makes the result
    /// trustworthy: the compiled assertions are immediately evaluated and kept ONLY if they reach that
    /// same verdict. A set that disagrees is a mistranslation, and a set that cannot resolve its
    /// selectors is a broken test — either would fail the step forever for a reason that has nothing to
    /// do with the application, so both are discarded and the step simply keeps using AI verification.
    /// </para>
    /// </summary>
    /// <param name="verdict">Whether the expectation actually held on this page.</param>
    /// <returns>The per-assertion breakdown when compilation succeeded; null when it did not.</returns>
    private async Task<IReadOnlyList<StepCheckResult>?> TryCompileAssertionsAsync(
        ExplorationSession session,
        PlaywrightBrowserService browser,
        ScenarioStep step,
        List<RecordedStepAction> recorded,
        string expectation,
        bool verdict,
        CancellationToken ct)
    {
        if (!_llm.IsLive || string.IsNullOrWhiteSpace(expectation))
        {
            return null;
        }

        List<StepAssertion> proposed;
        try
        {
            var digest = await browser.GetAssertionContextAsync(ct: ct);
            if (string.IsNullOrWhiteSpace(digest))
            {
                return null;
            }

            var reply = await _llm.CompleteAsync(
                AssertionCompilerPrompts.System,
                AssertionCompilerPrompts.BuildUserPrompt(expectation, ResolveVars(step.Action), browser.CurrentUrl, digest),
                jsonMode: true,
                ct);

            var json = JsonExtraction.ExtractJsonObject(reply);
            if (string.IsNullOrWhiteSpace(json))
            {
                return null;
            }

            proposed = JsonSerializer.Deserialize<CompiledAssertions>(json, JsonOpts)?.Assertions ?? new();
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            // Compiling is an optimisation, never a requirement: a step must not change its verdict
            // because the compiler had a bad day.
            _logger.LogWarning(ex, "Could not compile assertions for step {StepId}", step.Id);
            return null;
        }

        proposed = proposed
            .Where(a => a is not null && !string.IsNullOrWhiteSpace(a.Operator))
            .Take(10)
            .ToList();

        if (proposed.Count == 0)
        {
            return null;
        }

        var checks = new List<StepCheckResult>(proposed.Count);
        foreach (var assertion in proposed)
        {
            checks.Add(await AssertionEvaluator.EvaluateAsync(browser, assertion, ct));
        }

        if (checks.Any(c => c.Unresolved) || checks.All(c => c.Passed) != verdict)
        {
            await PublishLogAsync(
                session,
                $"  ⤓ Discarded the compiled assertions for step {step.Order} — they did not reproduce this run's verdict.",
                "warn",
                ct);
            return null;
        }

        recorded.RemoveAll(a => string.Equals(a.Kind?.Trim(), "assert", StringComparison.OrdinalIgnoreCase));
        foreach (var assertion in proposed)
        {
            recorded.Add(new RecordedStepAction
            {
                Kind = "assert",
                Target = assertion.Label,
                Assertion = assertion,
                CompiledFrom = expectation,
            });
        }

        step.RecordedActionsJson = SerializeRecordedActions(recorded);
        await _db.SaveChangesAsync(ct);
        await PublishLogAsync(
            session,
            $"  ⤓ Compiled {proposed.Count} assertion(s) for step {step.Order} — future runs verify this step without AI.",
            "info",
            ct);

        return checks;
    }

    /// <summary>
    /// Executes one recorded action by handing it to the generic engine, returning whether it
    /// succeeded and, when it did not, the driver's own explanation.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The recording is translated into a platform-neutral <c>TestAction</c> and routed by platform,
    /// which is what lets a scenario recorded in the browser also carry an API call or a database
    /// check without this method growing a branch per system.
    /// </para>
    /// <para>
    /// The failure detail is returned rather than only logged because the caller decides between
    /// healing a locator and reporting a genuine defect, and it cannot tell those apart from a bare
    /// false.
    /// </para>
    /// <para>
    /// Navigation targets are resolved here rather than in the driver because the recorded form is
    /// relative to <em>where the run currently is</em>, not to the environment root, and only the
    /// agent knows that.
    /// </para>
    /// </remarks>
    private async Task<(bool Ok, string? Detail)> ExecuteRecordedActionAsync(
        PlaywrightBrowserService browser,
        RecordedStepAction action,
        string baseUrl,
        CancellationToken ct)
    {
        // Assertions are evaluated after the whole step has replayed, against the settled page.
        // Running them inline would check the outcome before the application has produced it.
        if (string.Equals(action.Kind?.Trim(), "assert", StringComparison.OrdinalIgnoreCase))
        {
            return (true, null);
        }

        var resolvedTarget = string.Equals(action.Kind?.Trim(), "navigate", StringComparison.OrdinalIgnoreCase)
            ? ResolveNavigationTarget(browser.CurrentUrl ?? baseUrl, action.Target, baseUrl)
            : action.Target;

        var testAction = RecordedActionMapper.ToTestAction(action, resolvedTarget);
        if (testAction is null || _runContext is null)
        {
            _logger.LogWarning(
                "Recorded action '{Kind}' could not be translated into an executable action (target '{Target}').",
                action.Kind,
                action.Target);
            return (false, $"'{action.Kind}' is not an action the engine knows how to perform.");
        }

        var result = await _engine.ExecuteAsync(testAction, _runContext, ct);

        if (result.Unsupported is not null)
        {
            _logger.LogWarning("Recorded action '{Kind}' is not supported: {Reason}", action.Kind, result.Unsupported);
            return (false, result.Unsupported);
        }

        if (!result.Performed || !result.Passed)
        {
            // The single most common reason a deterministic replay turns into an AI heal, and
            // therefore the one worth leaving a permanent trace of.
            _logger.LogInformation(
                "Replay of '{Kind}' on '{Target}' did not succeed: {Detail}",
                action.Kind,
                action.Target,
                result.Detail);
            return (false, result.Detail);
        }

        return (true, null);
    }

    /// <summary>
    /// Whether a recorded action addresses an element on a screen, and so could be repaired by
    /// re-locating it. Mirrors the engine's own notion of which verbs take a target.
    /// </summary>
    private static bool RecordedActionTargetsElement(RecordedStepAction action) =>
        action.Platform is TestPlatform.Web or TestPlatform.Mobile
        && RecordedActionMapper.TargetsElement(action.Kind);

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
        if (!ok)
        {
            return null;
        }

        var healed = recordedAgain?.Target ?? decision.Target ?? action.Target;

        // If the AI hands back the same descriptor that just failed to replay, the recording cannot be
        // improved: every future run will heal again and spend another AI call. Surface that rather
        // than letting it loop silently.
        if (string.Equals(healed, action.Target, StringComparison.OrdinalIgnoreCase))
        {
            await PublishLogAsync(
                session,
                $"  ⚠ Step {step.Order}: the AI re-located the element but produced the same descriptor '{action.Target}', which replay cannot resolve. This step will need AI on every run until the description is refined.",
                "warn",
                ct);
        }

        return healed;
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

                var matched = (await MatchesExpectedOutcomeAsync(session, browser, step, ct)).Ok;
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

                // A step owns exactly ONE outcome. The moment that outcome is visible the step is
                // done — letting the agent keep going makes it record actions that belong to LATER
                // steps, which then have nothing left to do and fail on replay (e.g. "Open the cart
                // and click Checkout" recording the whole rest of the checkout). Only the zero-token
                // local confirmation is used here so an early exit never costs an AI call.
                if (await ConfirmedExpectedResultLocallyAsync(browser, step, ct))
                {
                    return (usedFallback ? StepRunStatus.Healed : StepRunStatus.Passed,
                        "Reached the step's expected result.", actions);
                }
            }

            history.AppendLine($"- turn {turn}: {action} -> {observation}. (thought: {TrimThought(decision.Thought)})");
            await PublishStatusAsync(session, browser.CurrentUrl, ct);
        }

        var finalMatched = (await MatchesExpectedOutcomeAsync(session, browser, step, ct)).Ok;
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
                StepAgentPrompts.BuildUserPrompt(ResolveVars(step.Action), ResolveVarsOrNull(step.ExpectedResult), pageUrl, snapshot, history, turn, maxTurns, _businessContext),
                jsonMode: true,
                ct);

            var json = JsonExtraction.ExtractJsonObject(raw);
            return JsonSerializer.Deserialize<StepAgentDecision>(json, JsonOpts);
        }
        catch (Exception ex) when (ex is not LlmUnavailableException)
        {
            // A malformed or unusable reply is a per-turn hiccup the loop can recover from by asking
            // again. A dead provider is NOT — it must propagate so the run stops instead of spending
            // every remaining turn on calls that cannot succeed.
            _logger.LogDebug(ex, "Step agent decision failed for step {StepId} on turn {Turn}.", step.Id, turn);
            return null;
        }
    }

    /// <summary>Executes a single agent decision. Returns whether it succeeded, whether a fallback
    /// (non-ref) strategy was used, a short human-readable observation, and the concrete replayable
    /// action to record (null for transient actions like wait).</summary>
    /// <remarks>
    /// Recording and replay go through the same engine on purpose. When they used different
    /// matchers, a step could be recorded by one set of rules and replayed by another, so a step
    /// that demonstrably worked while exploring could still fail on its very first replay. Routing
    /// both through <see cref="ITestEngine"/> means anything we record has, by construction, just
    /// been resolved by the code that will replay it.
    /// </remarks>
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
                    var navigated = await RunAgentActionAsync(TestActionKind.Navigate, target: null, value: url, ct);
                    return navigated.Ok
                        ? (true, false, $"navigated to {browser.CurrentUrl}",
                            new RecordedStepAction { Kind = "navigate", Target = url })
                        : (false, false, $"could not navigate to {url}", null);

                case "click":
                    var clickDescriptor = await ResolveActionDescriptorAsync(browser, decision, ct);
                    var clickTarget = await BuildAgentTargetAsync(browser, decision.Ref, clickDescriptor ?? step.Action, ct);
                    var clicked = await RunAgentActionAsync(TestActionKind.Click, clickTarget, value: null, ct);
                    return clicked.Ok
                        ? (true, clicked.ViaFallback, $"clicked \"{clickTarget}\"",
                            new RecordedStepAction { Kind = "click", Target = clickDescriptor })
                        : (false, false, $"could not click {decision.Ref ?? decision.Target}", null);

                case "type":
                    var value = decision.Value ?? string.Empty;
                    var typeDescriptor = await ResolveActionDescriptorAsync(browser, decision, ct);
                    var typeTarget = await BuildAgentTargetAsync(browser, decision.Ref, typeDescriptor ?? step.Action, ct);
                    var typed = await RunAgentActionAsync(TestActionKind.Type, typeTarget, value, ct);
                    return typed.Ok
                        ? (true, typed.ViaFallback, $"typed '{value}' into \"{typeTarget}\"",
                            new RecordedStepAction { Kind = "type", Target = typeDescriptor, Value = value })
                        : (false, false, $"could not find input {decision.Ref ?? decision.Target}", null);

                case "press":
                    var key = string.IsNullOrWhiteSpace(decision.Target) ? "Enter" : decision.Target!;
                    var pressed = await RunAgentActionAsync(TestActionKind.Press, target: null, value: key, ct);
                    return (pressed.Ok, false, pressed.Ok ? $"pressed {key}" : $"could not press {key}",
                        pressed.Ok ? new RecordedStepAction { Kind = "press", Target = key } : null);

                case "select":
                    var option = decision.Value ?? string.Empty;
                    var selectDescriptor = await ResolveActionDescriptorAsync(browser, decision, ct);
                    var selectTarget = await BuildAgentTargetAsync(browser, decision.Ref, selectDescriptor ?? step.Action, ct);
                    var selected = await RunAgentActionAsync(TestActionKind.Select, selectTarget, option, ct);
                    return selected.Ok
                        ? (true, selected.ViaFallback, $"selected '{option}' in \"{selectTarget}\"",
                            new RecordedStepAction { Kind = "select", Target = selectDescriptor, Value = option })
                        : (false, false, $"could not select '{option}' in {decision.Target}", null);

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

    /// <summary>Runs one agent-chosen action through the engine.</summary>
    private async Task<(bool Ok, bool ViaFallback)> RunAgentActionAsync(
        TestActionKind kind,
        ElementTarget? target,
        string? value,
        CancellationToken ct)
    {
        if (_runContext is null)
        {
            return (false, false);
        }

        var result = await _engine.ExecuteAsync(
            new TestAction { Kind = kind, Target = target, Value = value },
            _runContext,
            ct);

        // "Healed" here means a candidate after the first one won — i.e. the precise locator missed
        // and something further down the list rescued the action. That is exactly what the caller
        // means by a fallback, and it is worth surfacing because such steps are the fragile ones.
        return (result.Performed && result.Passed, result.Healed);
    }

    /// <summary>
    /// Builds the element target for an agent decision: the durable locator read off the ref'd
    /// element first, the ref itself as a safety net, and the descriptor as the final fallback.
    /// </summary>
    /// <remarks>
    /// The ordering is deliberate. Trying the durable locator first is what lets it win, and only a
    /// winner gets promoted into the object repository — if the volatile ref always went first, the
    /// run would keep succeeding while learning nothing. The ref stays in the list so that an
    /// element we could not describe durably still gets acted on rather than failing the step.
    /// </remarks>
    private static async Task<ElementTarget> BuildAgentTargetAsync(
        PlaywrightBrowserService browser,
        string? snapshotRef,
        string? descriptor,
        CancellationToken ct)
    {
        var target = new ElementTarget { Descriptor = descriptor };

        if (string.IsNullOrWhiteSpace(snapshotRef))
        {
            return target;
        }

        var durable = await browser.DescribeRefAsync(snapshotRef!, ct);
        if (durable is { } d)
        {
            target.Candidates.Add(new LocatorCandidate { Strategy = d.Strategy, Value = d.Value, Rank = -100 });
        }

        var refId = System.Text.RegularExpressions.Regex.Match(snapshotRef!, "e\\d+");
        if (refId.Success)
        {
            target.Candidates.Add(new LocatorCandidate
            {
                Strategy = LocatorStrategy.CSS,
                Value = $"[data-atip-ref='{refId.Value}']",
                Rank = -50,
                Volatile = true
            });
        }

        return target;
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
                    await RunAgentActionAsync(TestActionKind.Navigate, target: null, value: targetUrl, ct);
                    var navigationMatches = (await MatchesExpectedOutcomeAsync(session, browser, step, ct)).Ok;
                    status = ScenarioStepOutcomeResolver.ResolveAssertion(true, navigationMatches, 0);
                    detail = navigationMatches ? $"Navigated to {targetUrl}." : $"Navigated to {targetUrl}, but the expected outcome did not match.";
                    break;

                case "click":
                    var clickTarget = await BuildAgentTargetAsync(browser, plan.Ref, plan.Target ?? step.Action, ct);
                    var clickOutcome = await RunAgentActionAsync(TestActionKind.Click, clickTarget, value: null, ct);
                    var clickMatched = clickOutcome.Ok;
                    var clickExpectation = (await MatchesExpectedOutcomeAsync(session, browser, step, ct)).Ok;
                    status = ScenarioStepOutcomeResolver.ResolveAssertion(clickMatched, clickExpectation, clickOutcome.ViaFallback ? 1 : 0);
                    detail = clickMatched
                        ? (clickExpectation ? "Clicked the matched control and the expected state is present." : "Clicked the matched control, but the expected state did not appear.")
                        : "No matching control was found for the click step.";
                    break;

                case "type":
                    var value = string.IsNullOrWhiteSpace(plan.Value) ? "Sample value" : plan.Value;
                    var fillTarget = await BuildAgentTargetAsync(browser, plan.Ref, plan.Target ?? step.Action, ct);
                    var fillOutcome = await RunAgentActionAsync(TestActionKind.Type, fillTarget, value, ct);
                    var fillMatched = fillOutcome.Ok;
                    var fillExpectation = (await MatchesExpectedOutcomeAsync(session, browser, step, ct)).Ok;
                    status = ScenarioStepOutcomeResolver.ResolveAssertion(fillMatched, fillExpectation, fillOutcome.ViaFallback ? 1 : 0);
                    detail = fillMatched
                        ? (fillExpectation ? $"Filled the target field with '{value}' and the expected state is present." : $"Filled the target field with '{value}', but the page did not match the expected result.")
                        : "Could not match the input field.";
                    break;

                case "assert":
                    // An "assert" step often carries its expectation in the action text itself.
                    var assertionStep = string.IsNullOrWhiteSpace(step.ExpectedResult)
                        ? new ScenarioStep { Order = step.Order, Action = step.Action, ExpectedResult = step.Action }
                        : step;
                    var assertionMatched = (await MatchesExpectedOutcomeAsync(session, browser, assertionStep, ct)).Ok;
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

    /// <summary>
    /// Whether the step's expected result actually holds on the live page.
    /// <para>
    /// Verification is two-tier so a RUN stays cheap without becoming dishonest. A plain substring
    /// test of the whole expectation is useless — expected results are written as sentences ("The
    /// Checkout: Your Information page is displayed.") while the page only contains the fragment
    /// ("Checkout: Your Information") — so tier 1 matches the expectation's quoted literals and
    /// content words instead. Tier 1 can only ever CONFIRM an outcome; it is deliberately not
    /// allowed to reject one, and it refuses to judge negations, comparisons and counts at all
    /// (those are precisely where naive text matching produces false passes). Anything it cannot
    /// confirm escalates to tier 2: the same structured, evidence-based QA verification the
    /// exploration engine uses. Net effect: a passing replay costs zero AI tokens, while a
    /// questionable one is judged properly rather than rubber-stamped.
    /// </para>
    /// </summary>
    private async Task<(bool Ok, string Reason)> MatchesExpectedOutcomeAsync(
        ExplorationSession session,
        PlaywrightBrowserService browser,
        ScenarioStep step,
        CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(step.ExpectedResult))
        {
            return (true, "no explicit expected outcome");
        }

        var expected = ResolveVars(step.ExpectedResult);

        // Read the page only once it has stopped changing, otherwise a click that triggers a
        // navigation is judged against the page it navigated away from.
        await browser.WaitForPageSettledAsync(ct: ct);

        var snapshot = await SafeSnapshotForAgentAsync(browser, ct);
        var pageText = await SafeAccessibilityTreeAsync(browser, ct);

        // The snapshot and the accessibility outline are both budgeted/filtered for the AGENT. Neither
        // is a reliable basis for an ASSERTION: the snapshot's text cap can be exhausted before the
        // relevant part of a long page, and role-less framework-rendered controls never appear in the
        // outline at all. Read the full visible text as well, so a step is judged against what a user
        // would actually see rather than against a truncated view of it.
        var visibleText = await SafeVisibleTextAsync(browser, ct);
        var evidence = $"{snapshot}\n{pageText}\n{visibleText}\n{browser.CurrentUrl}";

        if (TryConfirmOutcomeLocally(expected, evidence))
        {
            return (true, "expected result confirmed on the page");
        }

        return await VerifyExpectedOutcomeAsync(session, step, browser.CurrentUrl, snapshot, $"{pageText}\n{visibleText}", ct);
    }

    /// <summary>
    /// Cheap, conservative confirmation of an expected result against page evidence. Returns true ONLY
    /// when every quoted literal and every significant content word of the expectation is present.
    /// Returns false for "cannot confirm" — never as a verdict of failure — so the caller escalates to
    /// AI verification. Expectations involving negation, comparison or specific numbers are never
    /// confirmed here, because presence-matching cannot decide them (e.g. "no results are displayed"
    /// would be wrongly satisfied by a "results for X instead" banner).
    /// </summary>
    private static bool TryConfirmOutcomeLocally(string expected, string evidence)
    {
        if (string.IsNullOrWhiteSpace(evidence))
        {
            return false;
        }

        if (UndecidableExpectationPattern.IsMatch(expected))
        {
            return false;
        }

        var confirmedSomething = false;

        // Quoted literals are the author's exact wording — they must appear verbatim.
        foreach (Match quote in QuotedLiteralPattern.Matches(expected))
        {
            var literal = quote.Groups["text"].Value.Trim();
            if (literal.Length < 2)
            {
                continue;
            }

            if (!evidence.Contains(literal, StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            confirmedSomething = true;
        }

        // Then every meaningful word outside the QA boilerplate must be present.
        foreach (Match word in ContentWordPattern.Matches(expected))
        {
            var token = word.Value;
            if (token.Length < 3 || OutcomeBoilerplateWords.Contains(token))
            {
                continue;
            }

            if (!evidence.Contains(token, StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            confirmedSomething = true;
        }

        return confirmedSomething;
    }

    private static readonly Regex QuotedLiteralPattern = new(
        "[\"'\u201c\u2018](?<text>[^\"'\u201d\u2019]{2,120})[\"'\u201d\u2019]",
        RegexOptions.CultureInvariant,
        TimeSpan.FromSeconds(1));

    private static readonly Regex ContentWordPattern = new(
        @"[A-Za-z][A-Za-z\-]{2,}",
        RegexOptions.CultureInvariant,
        TimeSpan.FromSeconds(1));

    /// <summary>
    /// Negation, comparison, quantity and ordering language. Presence-matching cannot decide any of
    /// these, so such expectations always go to full AI verification.
    /// </summary>
    private static readonly Regex UndecidableExpectationPattern = new(
        @"\d|\bno\b|\bnot\b|\bnone\b|\bnever\b|\bwithout\b|\bempty\b|\bzero\b|\babsent\b|\bexcept\b|\bcannot\b|\bcan't\b|\bisn't\b|\baren't\b|\bdoesn't\b|\bdon't\b|\bfewer\b|\bless\b|\bmore\b|\bleast\b|\bmost\b|\bmaximum\b|\bminimum\b|\bgreater\b|\bhigher\b|\blower\b|\bbetween\b|\bexactly\b|\bascending\b|\bdescending\b|\bsorted\b|\border(ed)?\b|\bbefore\b|\bafter\b|\bonly\b",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant,
        TimeSpan.FromSeconds(1));

    /// <summary>
    /// Words that appear in how testers PHRASE an expectation rather than in the app's UI, so their
    /// absence from the page says nothing. Deliberately excludes domain-bearing nouns.
    /// </summary>
    private static readonly HashSet<string> OutcomeBoilerplateWords = new(StringComparer.OrdinalIgnoreCase)
    {
        "the", "and", "are", "was", "were", "been", "being", "for", "with", "that", "this", "its",
        "page", "screen", "user", "should", "must", "will", "shall", "then",
        "displayed", "display", "displays", "shown", "show", "shows", "showing",
        "appear", "appears", "appeared", "visible", "presented", "present",
        "correctly", "successfully", "successful", "properly", "expected",
        "reads", "read", "says", "state", "still", "remains", "remain",
        "field", "fields", "contains", "contain", "containing",
        "populated", "masked", "entered", "enter", "value",
    };

    /// <summary>
    /// Zero-token check of whether the step's expected result is ALREADY visible. Used to end an
    /// agentic step at its own boundary; deliberately does not escalate to AI verification, because a
    /// "not yet" answer here simply means the agent should take another action.
    /// </summary>
    private async Task<bool> ConfirmedExpectedResultLocallyAsync(
        PlaywrightBrowserService browser,
        ScenarioStep step,
        CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(step.ExpectedResult))
        {
            return false;
        }

        await browser.WaitForPageSettledAsync(ct: ct);

        var snapshot = await SafeSnapshotForAgentAsync(browser, ct);
        var pageText = await SafeAccessibilityTreeAsync(browser, ct);

        return TryConfirmOutcomeLocally(
            ResolveVars(step.ExpectedResult),
            $"{snapshot}\n{pageText}\n{browser.CurrentUrl}");
    }

    private static async Task<string> SafeSnapshotForAgentAsync(PlaywrightBrowserService browser, CancellationToken ct)
    {
        try
        {
            return await browser.SnapshotForAgentAsync(60, ct);
        }
        catch
        {
            return string.Empty;
        }
    }

    private static async Task<string> SafeAccessibilityTreeAsync(PlaywrightBrowserService browser, CancellationToken ct)
    {
        try
        {
            return await browser.GetAccessibilityTreeAsync(ct);
        }
        catch
        {
            return string.Empty;
        }
    }

    private static async Task<string> SafeVisibleTextAsync(PlaywrightBrowserService browser, CancellationToken ct)
    {
        try
        {
            return await browser.GetVisibleTextAsync(ct: ct);
        }
        catch
        {
            return string.Empty;
        }
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

    /// <summary>
    /// Matches a plain navigation step ("Navigate to https://…", "Go to /cart", "Open the checkout page")
    /// and resolves its absolute URL. Such steps are fully described by their own text, so a run can
    /// execute them deterministically without consulting the AI.
    /// </summary>
    private static bool TryParseNavigationStep(string action, string baseUrl, out string url)
    {
        url = string.Empty;

        if (string.IsNullOrWhiteSpace(action))
        {
            return false;
        }

        var match = NavigationStepPattern.Match(action.Trim());
        if (!match.Success)
        {
            return false;
        }

        var target = match.Groups["target"].Value.Trim().Trim('"', '\'', '`', '.', ',', ')');
        if (target.Length == 0)
        {
            return false;
        }

        if (Uri.TryCreate(target, UriKind.Absolute, out var absolute)
            && (absolute.Scheme == Uri.UriSchemeHttp || absolute.Scheme == Uri.UriSchemeHttps))
        {
            url = absolute.ToString();
            return true;
        }

        // Relative path against the environment base URL ("/inventory.html").
        if (target.StartsWith('/')
            && Uri.TryCreate(baseUrl, UriKind.Absolute, out var root)
            && Uri.TryCreate(root, target, out var resolved))
        {
            url = resolved.ToString();
            return true;
        }

        return false;
    }

    /// <summary>
    /// Matches a step that is ONLY a navigation. The trailing anchor is essential: without it a
    /// compound step such as "Navigate to /login and log in as standard_user" also matches, and the
    /// step is then recorded as a bare navigate — silently dropping the login while still reporting
    /// success. Compound steps must fall through to the agent so every action is actually performed.
    /// </summary>
    private static readonly Regex NavigationStepPattern = new(
        @"^(?:navigate|go|browse|open|visit)\s+(?:to\s+)?(?:the\s+)?(?<target>\S+?)[.,;]?\s*$",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant,
        TimeSpan.FromSeconds(1));

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
            var filled = await RunAgentActionAsync(
                TestActionKind.Type,
                new ElementTarget { Descriptor = target },
                InferSampleValue(action),
                ct);
            return ScenarioStepOutcomeResolver.Resolve(filled.Ok, filled.Ok ? 0 : -1);
        }

        if (lower.Contains("click ") || lower.Contains("select ") || lower.Contains("submit") || lower.Contains("continue") || lower.Contains("save"))
        {
            var target = ExtractClickableTarget(action) ?? action;
            var clicked = await RunAgentActionAsync(
                TestActionKind.Click,
                new ElementTarget { Descriptor = target },
                value: null,
                ct);
            return ScenarioStepOutcomeResolver.Resolve(clicked.Ok, clicked.Ok ? 0 : -1);
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

    /// <summary>
    /// Clicks AI-nominated controls to reveal content hidden behind modals and menus, so the link
    /// crawl can see pages that are not reachable from the initial DOM.
    /// </summary>
    private async Task TryExpandDynamicContentAsync(
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
                await browser.TryClickAsync(interaction.Selector, ct);
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
