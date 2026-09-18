using ATIP.Application.Engine.Contracts;
using ATIP.Application.Engine.Model;
using ATIP.Application.Features.Explorer.Agent;
using ATIP.Domain.Enums;
using ATIP.Infrastructure.Exploration;
using Microsoft.Extensions.Logging;
using Microsoft.Playwright;

namespace ATIP.Infrastructure.Engine.Web;

/// <summary>
/// Drives a browser through <see cref="PlaywrightBrowserService"/>.
/// </summary>
/// <remarks>
/// The driver owns the translation from the platform-neutral <see cref="TestAction"/> vocabulary to
/// Playwright calls, and — crucially — the locator loop: candidates are tried in the order the
/// resolver supplies, and whichever one wins is reported back so the ranking improves. Nothing here
/// calls a language model; healing is the engine's concern, so a replay stays deterministic.
/// </remarks>
public sealed class PlaywrightWebDriver : ITestDriver
{
    private static readonly HashSet<TestActionKind> Supported =
    [
        TestActionKind.Navigate, TestActionKind.Back, TestActionKind.Forward, TestActionKind.Reload,
        TestActionKind.Click, TestActionKind.DoubleClick, TestActionKind.RightClick, TestActionKind.Hover,
        TestActionKind.Type, TestActionKind.Clear, TestActionKind.Press, TestActionKind.Select,
        TestActionKind.Check, TestActionKind.Uncheck, TestActionKind.Upload, TestActionKind.ScrollTo,
        TestActionKind.Wait, TestActionKind.WaitFor,
        TestActionKind.SwitchTab, TestActionKind.NewTab, TestActionKind.CloseTab, TestActionKind.SwitchFrame,
        TestActionKind.HandleDialog,
        TestActionKind.SetCookie, TestActionKind.ClearCookies, TestActionKind.SetStorage, TestActionKind.Screenshot,
        TestActionKind.Assert, TestActionKind.Capture
    ];

    private readonly PlaywrightBrowserService _browser;
    private readonly ILocatorResolver _resolver;
    private readonly ILogger<PlaywrightWebDriver> _logger;
    private readonly bool _ownsBrowser;
    private bool _opened;

    public PlaywrightWebDriver(
        PlaywrightBrowserService browser,
        ILocatorResolver resolver,
        ILogger<PlaywrightWebDriver> logger,
        bool ownsBrowser = true)
    {
        _browser = browser;
        _resolver = resolver;
        _logger = logger;
        _ownsBrowser = ownsBrowser;
    }

    public TestPlatform Platform => TestPlatform.Web;

    /// <summary>The underlying service, so the explorer can keep using the same live browser.</summary>
    public PlaywrightBrowserService Browser => _browser;

    public bool Supports(TestActionKind kind) => Supported.Contains(kind);

    public async Task OpenAsync(RunContext context, CancellationToken cancellationToken)
    {
        if (_opened)
        {
            return;
        }

        // When the browser was handed in already running (the explorer's session), initialising it
        // again would throw away the page — and with it the login the scenario just performed.
        if (_ownsBrowser)
        {
            await _browser.InitializeAsync();
        }

        _opened = true;
    }

    public async Task<ActionResult> ExecuteAsync(TestAction action, RunContext context, CancellationToken cancellationToken)
    {
        if (!Supports(action.Kind))
        {
            return ActionResult.NotSupported($"The web driver cannot perform '{action.Kind}'.");
        }

        var timeout = action.OptionAsInt(ActionOptionNames.TimeoutMs, context.DefaultTimeoutMs);
        var value = context.Resolve(action.Value);

        try
        {
            return action.Kind switch
            {
                TestActionKind.Navigate => await NavigateAsync(value, context, cancellationToken),
                TestActionKind.Back => Result(await _browser.GoBackAsync(cancellationToken), "Navigated back", "No history entry to go back to"),
                TestActionKind.Forward => Result(await _browser.GoForwardAsync(cancellationToken), "Navigated forward", "No history entry to go forward to"),
                TestActionKind.Reload => Result(await _browser.ReloadAsync(cancellationToken), "Reloaded the page", "Reload failed"),

                TestActionKind.Wait => await WaitAsync(action, context),
                TestActionKind.SwitchFrame => Result(
                    await _browser.SwitchFrameAsync(action.Option(ActionOptionNames.FrameSelector) ?? value, cancellationToken),
                    $"Switched to frame '{_browser.CurrentFramePath}'",
                    "The requested frame was not found"),

                TestActionKind.SwitchTab => await SwitchTabAsync(action, context, cancellationToken),
                TestActionKind.NewTab => Result(await _browser.OpenNewTabAsync(value, cancellationToken), "Opened a new tab", "Could not open a new tab"),
                TestActionKind.CloseTab => Result(await _browser.CloseCurrentTabAsync(cancellationToken), "Closed the tab", "Refused to close the only open tab"),

                TestActionKind.HandleDialog => HandleDialog(action),

                TestActionKind.SetCookie => Result(
                    await _browser.SetCookieAsync(
                        action.Option(ActionOptionNames.CookieName) ?? string.Empty,
                        value,
                        action.Option(ActionOptionNames.CookieDomain),
                        path: null,
                        cancellationToken),
                    "Cookie set",
                    "Could not set the cookie"),
                TestActionKind.ClearCookies => Result(await _browser.ClearCookiesAsync(cancellationToken), "Cookies cleared", "Could not clear cookies"),
                TestActionKind.SetStorage => Result(
                    await _browser.SetStorageAsync(
                        action.Option(ActionOptionNames.StorageKind) ?? "local",
                        action.Option(ActionOptionNames.StorageKey) ?? string.Empty,
                        value,
                        cancellationToken),
                    "Storage entry written",
                    "Could not write the storage entry"),

                TestActionKind.Screenshot => await ScreenshotAsync(),
                TestActionKind.Assert => await AssertAsync(action, cancellationToken),

                // A key press with no element goes to the page: recordings routinely say "press
                // Enter" after typing, meaning the focused field, not a named one.
                TestActionKind.Press when action.Target is null || action.Target.IsEmpty =>
                    Result(
                        await _browser.PressKeyAsync(string.IsNullOrWhiteSpace(value) ? "Enter" : value!, cancellationToken),
                        $"Pressed {value ?? "Enter"}",
                        $"Could not press {value ?? "Enter"}"),

                _ => await ElementActionAsync(action, value, context, timeout, cancellationToken)
            };
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            // A driver must not throw for ordinary trouble: one bad step should fail that step, not
            // tear down a run that still has useful coverage left in it.
            _logger.LogWarning(ex, "Web action {Kind} failed.", action.Kind);
            return ActionResult.Fail($"{action.Kind} failed: {ex.Message}");
        }
    }

    public async Task<EvidenceCapture?> CaptureEvidenceAsync(CancellationToken cancellationToken)
    {
        if (!_opened)
        {
            return null;
        }

        try
        {
            return new EvidenceCapture
            {
                ScreenshotBase64 = Convert.ToBase64String(await _browser.TakeScreenshotAsync(fullPage: false)),
                Location = _browser.CurrentUrl,
                Title = _browser.CurrentTitle,
                Text = await _browser.GetVisibleTextAsync(4000)
            };
        }
        catch
        {
            return null;
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (_ownsBrowser)
        {
            await _browser.DisposeAsync();
        }
    }

    // ── Action implementations ──────────────────────────────────────────────────────────────

    private async Task<ActionResult> NavigateAsync(string? value, RunContext context, CancellationToken ct)
    {
        var url = ResolveUrl(value, context);
        if (string.IsNullOrWhiteSpace(url))
        {
            return ActionResult.Fail("Navigate requires a URL.");
        }

        var (title, finalUrl, status) = await _browser.NavigateAsync(url, ct);
        return ActionResult.Ok($"Opened {finalUrl} ({status}) — {title}", finalUrl);
    }

    /// <summary>
    /// Turns a possibly relative target into an absolute URL using the environment's base. Steps
    /// are written as "/checkout" so the same scenario can run against staging and production.
    /// </summary>
    private static string? ResolveUrl(string? value, RunContext context)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return context.BaseUrl;
        }

        if (value.StartsWith("http://", StringComparison.OrdinalIgnoreCase)
            || value.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
        {
            return value;
        }

        if (string.IsNullOrWhiteSpace(context.BaseUrl))
        {
            return value;
        }

        return Uri.TryCreate(new Uri(context.BaseUrl!), value, out var combined) ? combined.ToString() : value;
    }

    private async Task<ActionResult> WaitAsync(TestAction action, RunContext context)
    {
        var ms = action.OptionAsInt(ActionOptionNames.TimeoutMs, 0);
        if (ms <= 0 && int.TryParse(context.Resolve(action.Value), out var parsed))
        {
            ms = parsed;
        }

        ms = Math.Clamp(ms <= 0 ? 800 : ms, 50, 60_000);
        await _browser.WaitAsync(ms);
        return ActionResult.Ok($"Waited {ms}ms");
    }

    private async Task<ActionResult> SwitchTabAsync(TestAction action, RunContext context, CancellationToken ct)
    {
        if (action.Option(ActionOptionNames.TabUrlContains) is { Length: > 0 } urlNeedle)
        {
            var matched = await _browser.SwitchToTabMatchingAsync(context.Resolve(urlNeedle)!, byTitle: false, ct);
            return Result(matched, $"Switched to the tab whose URL contains '{urlNeedle}'", $"No open tab has '{urlNeedle}' in its URL");
        }

        if (action.Option(ActionOptionNames.TabTitleContains) is { Length: > 0 } titleNeedle)
        {
            var matched = await _browser.SwitchToTabMatchingAsync(context.Resolve(titleNeedle)!, byTitle: true, ct);
            return Result(matched, $"Switched to the tab titled like '{titleNeedle}'", $"No open tab has '{titleNeedle}' in its title");
        }

        var index = action.OptionAsInt(ActionOptionNames.TabIndex, -1);
        if (index < 0 && int.TryParse(context.Resolve(action.Value), out var fromValue))
        {
            index = fromValue;
        }

        if (index < 0)
        {
            return ActionResult.Fail("Switch tab needs an index, a URL fragment, or a title fragment.");
        }

        return Result(await _browser.SwitchToTabAsync(index, ct), $"Switched to tab {index}", $"There is no tab at index {index}");
    }

    private ActionResult HandleDialog(TestAction action)
    {
        var accept = !string.Equals(action.Option(ActionOptionNames.DialogAction), "dismiss", StringComparison.OrdinalIgnoreCase);
        var promptText = action.Option(ActionOptionNames.DialogPromptText);

        // Set before the action that triggers the dialog, because Playwright blocks the triggering
        // call until the dialog is answered — there is no "after" in which to react.
        _browser.SetDialogPolicy(accept, promptText);

        return ActionResult.Ok(accept
            ? $"The next dialog will be accepted{(promptText is null ? string.Empty : $" with '{promptText}'")}"
            : "The next dialog will be dismissed");
    }

    private async Task<ActionResult> ScreenshotAsync()
    {
        var shot = await _browser.TakeScreenshotAsync(fullPage: true);
        return shot.Length == 0
            ? ActionResult.Fail("Screenshot could not be captured.")
            : ActionResult.Ok("Screenshot captured", Convert.ToBase64String(shot));
    }

    private async Task<ActionResult> AssertAsync(TestAction action, CancellationToken ct)
    {
        if (action.Assertion is null)
        {
            return ActionResult.Fail("Assert action has no compiled assertion.");
        }

        var check = await AssertionEvaluator.EvaluateAsync(_browser, action.Assertion, ct);
        return ActionResult.Assertion(check.Passed, check.Passed ? $"{check.Label}: passed" : $"{check.Label}: {check.Actual}", [check]);
    }

    /// <summary>Everything that needs an element resolved first.</summary>
    private async Task<ActionResult> ElementActionAsync(
        TestAction action,
        string? value,
        RunContext context,
        int timeout,
        CancellationToken ct)
    {
        if (action.Target is null || action.Target.IsEmpty)
        {
            return ActionResult.Fail($"{action.Kind} needs a target element.");
        }

        var resolution = await ResolveElementAsync(action.Target, action.Kind, context, timeout, ct);
        if (resolution.Locator is null)
        {
            // Report the miss as well as the hit: a stored locator that no longer resolves has to
            // accrue failures, otherwise nothing is ever quarantined and every run keeps paying a
            // full timeout for a selector the page dropped long ago.
            await ReportAsync(action.Target, resolution, context, ct);
            return ActionResult.Fail($"Could not find {action.Target} on {_browser.CurrentUrl}.");
        }

        var locator = resolution.Locator;
        var healed = resolution.Winner is not null && resolution.AttemptIndex > 0;

        switch (action.Kind)
        {
            case TestActionKind.Click:
                await locator.ScrollIntoViewIfNeededAsync(new() { Timeout = timeout });
                await locator.ClickAsync(new LocatorClickOptions { Timeout = timeout });
                break;

            case TestActionKind.DoubleClick:
                await locator.DblClickAsync(new LocatorDblClickOptions { Timeout = timeout });
                break;

            case TestActionKind.RightClick:
                await locator.ClickAsync(new LocatorClickOptions { Button = MouseButton.Right, Timeout = timeout });
                break;

            case TestActionKind.Hover:
                await locator.HoverAsync(new LocatorHoverOptions { Timeout = timeout });
                break;

            case TestActionKind.Type:
                await locator.FillAsync(value ?? string.Empty, new LocatorFillOptions { Timeout = timeout });
                break;

            case TestActionKind.Clear:
                await locator.FillAsync(string.Empty, new LocatorFillOptions { Timeout = timeout });
                break;

            case TestActionKind.Press:
                await locator.PressAsync(string.IsNullOrWhiteSpace(value) ? "Enter" : value!, new LocatorPressOptions { Timeout = timeout });
                break;

            case TestActionKind.Select:
                await SelectAsync(locator, value, timeout);
                break;

            case TestActionKind.Check:
                await locator.CheckAsync(new LocatorCheckOptions { Timeout = timeout });
                break;

            case TestActionKind.Uncheck:
                await locator.UncheckAsync(new LocatorUncheckOptions { Timeout = timeout });
                break;

            case TestActionKind.ScrollTo:
                await locator.ScrollIntoViewIfNeededAsync(new() { Timeout = timeout });
                break;

            case TestActionKind.Upload:
                var paths = (action.Option(ActionOptionNames.FilePaths) ?? value ?? string.Empty)
                    .Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
                if (!await _browser.UploadFilesAsync(locator, paths, ct))
                {
                    return ActionResult.Fail("Upload failed: one or more files do not exist.");
                }
                break;

            case TestActionKind.WaitFor:
                var condition = action.Option(ActionOptionNames.Condition) ?? "visible";
                if (!await _browser.WaitForStateAsync(locator, condition, timeout, ct))
                {
                    return ActionResult.Fail($"{action.Target} did not become '{condition}' within {timeout}ms.");
                }
                break;

            case TestActionKind.Capture:
                var captured = await CaptureFromElementAsync(locator, action, timeout);
                await ReportAsync(action.Target, resolution, context, ct);
                return ActionResult.Ok($"Captured '{captured}' from {action.Target}", captured, resolution.Winner, healed);

            default:
                return ActionResult.NotSupported($"The web driver cannot perform '{action.Kind}'.");
        }

        await ReportAsync(action.Target, resolution, context, ct);
        await _browser.WaitForPageSettledAsync(1500);

        return ActionResult.Ok($"{action.Kind} on {action.Target}", null, resolution.Winner, healed);
    }

    private static async Task SelectAsync(ILocator locator, string? value, int timeout)
    {
        // Native selects match on either the visible label or the underlying value, and recordings
        // legitimately carry both. Try the label first because that is what a user sees.
        try
        {
            await locator.SelectOptionAsync(new SelectOptionValue { Label = value }, new() { Timeout = timeout });
        }
        catch
        {
            await locator.SelectOptionAsync(new SelectOptionValue { Value = value }, new() { Timeout = timeout });
        }
    }

    private async Task<string?> CaptureFromElementAsync(ILocator locator, TestAction action, int timeout)
    {
        var source = (action.Option(ActionOptionNames.CaptureSource) ?? "text").Trim().ToLowerInvariant();
        var expression = action.Option(ActionOptionNames.CaptureExpression);

        return source switch
        {
            "value" => await locator.InputValueAsync(new LocatorInputValueOptions { Timeout = timeout }),
            "attribute" when !string.IsNullOrWhiteSpace(expression) =>
                await locator.GetAttributeAsync(expression!, new LocatorGetAttributeOptions { Timeout = timeout }),
            "url" => _browser.CurrentUrl,
            "title" => _browser.CurrentTitle,
            _ => (await locator.TextContentAsync(new LocatorTextContentOptions { Timeout = timeout }))?.Trim()
        };
    }

    // ── Locator resolution ──────────────────────────────────────────────────────────────────

    private readonly record struct Resolution(
        ILocator? Locator,
        LocatorCandidate? Winner,
        IReadOnlyList<LocatorCandidate> Attempted,
        int AttemptIndex);

    /// <summary>
    /// Walks the candidate list until one resolves. The candidates come from the object repository
    /// (ranked by what has been working) plus whatever the action carried inline; if the target
    /// only has a human descriptor, that is expanded into the usual family of guesses so
    /// descriptor-only steps go down the same path as everything else.
    /// </summary>
    private async Task<Resolution> ResolveElementAsync(
        ElementTarget target,
        TestActionKind kind,
        RunContext context,
        int timeout,
        CancellationToken ct)
    {
        var candidates = (await _resolver.ResolveAsync(target, context, ct)).ToList();

        if (candidates.Count == 0 && !string.IsNullOrWhiteSpace(target.Descriptor))
        {
            candidates.AddRange(DescriptorCandidates(target.Descriptor!, kind));
        }

        // A short per-candidate budget: the list is a search, and spending the full timeout on each
        // miss turns a nine-candidate lookup into a minute of waiting. The last candidate gets the
        // full budget so a genuinely slow page still has its chance.
        var probeTimeout = Math.Max(1000, Math.Min(timeout, 3000));

        // First pass demands an unambiguous match, so a precise candidate later in the list beats a
        // loose one earlier. Only if every candidate is ambiguous or absent does the second pass
        // accept the first match — real pages repeat markup, and refusing outright would fail
        // scenarios that a human would consider perfectly well specified.
        for (var pass = 0; pass < 2; pass++)
        {
            for (var i = 0; i < candidates.Count; i++)
            {
                var candidate = candidates[i];
                var budget = i == candidates.Count - 1 ? timeout : probeTimeout;

                var locator = await _browser.ResolveAsync(
                    candidate.Strategy,
                    candidate.Value,
                    target.Index,
                    pass == 0 ? budget : probeTimeout,
                    allowAmbiguous: pass == 1,
                    ct);

                if (locator is not null)
                {
                    return new Resolution(locator, candidate, candidates, i);
                }
            }
        }

        return new Resolution(null, null, candidates, -1);
    }

    private async Task ReportAsync(ElementTarget target, Resolution resolution, RunContext context, CancellationToken ct)
    {
        if (resolution.Attempted.Count > 0)
        {
            await _resolver.ReportOutcomeAsync(target, resolution.Attempted, resolution.Winner, context, ct);
        }
    }

    /// <summary>
    /// Expands a human descriptor ("Place order", "Email") into the ordered locator guesses that
    /// have historically resolved such hints.
    /// </summary>
    /// <remarks>
    /// The order depends on the verb. "Type" almost always addresses a field, which is found by its
    /// label or placeholder; "click" almost always addresses a control, which is found by its role
    /// and accessible name. Probing in the wrong order still works but pays a failed lookup per
    /// action, and on a slow page that is the difference between a brisk run and a crawl.
    /// </remarks>
    private static IEnumerable<LocatorCandidate> DescriptorCandidates(string descriptor, TestActionKind kind)
    {
        var rank = 0;

        yield return LocatorCandidate.Of(LocatorStrategy.TestId, descriptor, rank++);

        var fieldFirst = kind is TestActionKind.Type or TestActionKind.Clear
            or TestActionKind.Select or TestActionKind.Upload or TestActionKind.Check or TestActionKind.Uncheck;

        if (fieldFirst)
        {
            yield return LocatorCandidate.Of(LocatorStrategy.Label, descriptor, rank++);
            yield return LocatorCandidate.Of(LocatorStrategy.Placeholder, descriptor, rank++);
            yield return LocatorCandidate.Of(LocatorStrategy.Role, $"Textbox:{descriptor}", rank++);
            yield return LocatorCandidate.Of(LocatorStrategy.ARIA, descriptor, rank++);
        }
        else
        {
            yield return LocatorCandidate.Of(LocatorStrategy.Role, $"Button:{descriptor}", rank++);
            yield return LocatorCandidate.Of(LocatorStrategy.Role, $"Link:{descriptor}", rank++);
            yield return LocatorCandidate.Of(LocatorStrategy.ARIA, descriptor, rank++);
            yield return LocatorCandidate.Of(LocatorStrategy.Label, descriptor, rank++);
            yield return LocatorCandidate.Of(LocatorStrategy.Text, descriptor, rank++);
            yield return LocatorCandidate.Of(LocatorStrategy.Title, descriptor, rank++);
        }

        // Attribute fallbacks: recordings often store an element id or a submit button's @value
        // rather than anything a user can see, and no semantic strategy will find those.
        yield return LocatorCandidate.Of(
            LocatorStrategy.CSS,
            $"[id=\"{descriptor}\"],[name=\"{descriptor}\"],input[value=\"{descriptor}\"]",
            rank++);

        if (kind == TestActionKind.Select)
        {
            // Last resort for a dropdown: the page's only <select>. Narrow enough to be safe,
            // and it rescues recordings whose descriptor named the sort order rather than the control.
            yield return LocatorCandidate.Of(LocatorStrategy.CSS, "select", rank);
        }
    }

    private static ActionResult Result(bool ok, string success, string failure) =>
        ok ? ActionResult.Ok(success) : ActionResult.Fail(failure);
}
