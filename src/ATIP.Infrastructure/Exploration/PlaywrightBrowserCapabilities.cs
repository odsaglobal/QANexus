using ATIP.Domain.Enums;
using Microsoft.Playwright;

namespace ATIP.Infrastructure.Exploration;

/// <summary>
/// The browser primitives the generic test engine needs beyond exploration: answering native
/// dialogs, working inside iframes, moving between tabs, uploading files, and reading or writing
/// browser state.
/// </summary>
/// <remarks>
/// These live in their own file because they are driven by <c>PlaywrightWebDriver</c> rather than
/// by the explorer, and because they share the scoping rule that makes frames work: every locator
/// is built against the <em>current scope</em>, which is the page until a step switches into a
/// frame. Without that single choke point, "switch to frame" would silently have no effect on any
/// locator built elsewhere.
/// </remarks>
public sealed partial class PlaywrightBrowserService
{
    /// <summary>
    /// The frame path the session is currently inside, outermost first. Empty means the top-level
    /// document. A list rather than a single selector so nested frames (a payment widget inside a
    /// checkout frame) can be entered and left one level at a time.
    /// </summary>
    private readonly List<string> _frameSelectors = [];

    private DialogAnswer _dialogAnswer = DialogAnswer.AlwaysDismiss;

    /// <summary>Message of the most recent native dialog, so a step can assert on what it said.</summary>
    public string? LastDialogMessage { get; private set; }

    /// <summary>Type (alert/confirm/prompt/beforeunload) of the most recent native dialog.</summary>
    public string? LastDialogType { get; private set; }

    /// <summary>Selector path of the frame the session is currently scoped to, for logging.</summary>
    public string CurrentFramePath => _frameSelectors.Count == 0 ? "(top)" : string.Join(" > ", _frameSelectors);

    // ── Native dialogs ──────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Decides how the next native dialog is answered. Playwright blocks the page until a dialog
    /// is handled, so a handler must always be attached; this only changes its answer.
    /// </summary>
    /// <param name="accept">True to accept (OK), false to dismiss (Cancel).</param>
    /// <param name="promptText">Text to type into a prompt before accepting.</param>
    /// <param name="oneShot">
    /// When true the policy reverts to "dismiss" after one dialog. That is the safe default: a
    /// step that says "accept the delete confirmation" means that one confirmation, not every
    /// dialog for the rest of the run.
    /// </param>
    public void SetDialogPolicy(bool accept, string? promptText = null, bool oneShot = true)
    {
        _dialogAnswer = new DialogAnswer(accept, promptText, oneShot);
    }

    private async void HandleDialogAsync(object? sender, IDialog dialog)
    {
        var answer = _dialogAnswer;
        if (answer.OneShot)
        {
            _dialogAnswer = DialogAnswer.AlwaysDismiss;
        }

        try
        {
            LastDialogMessage = dialog.Message;
            LastDialogType = dialog.Type;

            if (answer.Accept)
            {
                await dialog.AcceptAsync(answer.PromptText ?? string.Empty);
            }
            else
            {
                await dialog.DismissAsync();
            }
        }
        catch
        {
            // The dialog can be gone already (navigation, tab closed). Nothing left to answer.
        }
    }

    private readonly record struct DialogAnswer(bool Accept, string? PromptText, bool OneShot)
    {
        public static DialogAnswer AlwaysDismiss => new(false, null, false);
    }

    // ── Frames ──────────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Scopes subsequent locators to an iframe. Pass <c>top</c> (or null/empty) to return to the
    /// main document, or <c>parent</c> to step out one level.
    /// </summary>
    /// <returns>False if the requested frame does not exist, leaving the scope unchanged.</returns>
    public async Task<bool> SwitchFrameAsync(string? selector, CancellationToken ct = default)
    {
        if (_page is null)
        {
            return false;
        }

        if (string.IsNullOrWhiteSpace(selector) || selector.Equals("top", StringComparison.OrdinalIgnoreCase))
        {
            _frameSelectors.Clear();
            return true;
        }

        if (selector.Equals("parent", StringComparison.OrdinalIgnoreCase))
        {
            if (_frameSelectors.Count > 0)
            {
                _frameSelectors.RemoveAt(_frameSelectors.Count - 1);
            }

            return true;
        }

        // Verify before committing: switching into a frame that is not there would silently
        // redirect every later locator into a dead scope, and the step that finally fails would be
        // the wrong one to blame.
        _frameSelectors.Add(selector);
        try
        {
            var probe = CurrentScope.Locator(":root");
            await probe.First.WaitForAsync(new LocatorWaitForOptions { State = WaitForSelectorState.Attached, Timeout = 5_000 });
            return true;
        }
        catch
        {
            _frameSelectors.RemoveAt(_frameSelectors.Count - 1);
            return false;
        }
    }

    /// <summary>
    /// Where locators are built: the page, or the frame chain the session has switched into.
    /// </summary>
    private ElementScope CurrentScope
    {
        get
        {
            if (_page is null)
            {
                throw new InvalidOperationException("Browser is not initialised.");
            }

            if (_frameSelectors.Count == 0)
            {
                return ElementScope.Of(_page);
            }

            var frame = _page.FrameLocator(_frameSelectors[0]);
            for (var i = 1; i < _frameSelectors.Count; i++)
            {
                frame = frame.FrameLocator(_frameSelectors[i]);
            }

            return ElementScope.Of(frame);
        }
    }

    // ── Tabs ────────────────────────────────────────────────────────────────────────────────

    /// <summary>Makes the tab at <paramref name="index"/> (as reported by ListTabsAsync) active.</summary>
    public async Task<bool> SwitchToTabAsync(int index, CancellationToken ct = default)
    {
        var pages = OpenPages();
        if (index < 0 || index >= pages.Count)
        {
            return false;
        }

        await ActivateAsync(pages[index]);
        return true;
    }

    /// <summary>
    /// Makes active the first tab whose URL or title contains <paramref name="needle"/>. Matching
    /// on a fragment rather than the whole value because the interesting part of a popup's URL is
    /// usually a path, and the rest is session noise that changes every run.
    /// </summary>
    public async Task<bool> SwitchToTabMatchingAsync(string needle, bool byTitle, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(needle))
        {
            return false;
        }

        foreach (var page in OpenPages())
        {
            string haystack;
            try
            {
                haystack = byTitle ? await page.TitleAsync() : page.Url;
            }
            catch
            {
                continue;
            }

            if (haystack.Contains(needle, StringComparison.OrdinalIgnoreCase))
            {
                await ActivateAsync(page);
                return true;
            }
        }

        return false;
    }

    /// <summary>Opens a new tab and makes it active, optionally navigating it.</summary>
    public async Task<bool> OpenNewTabAsync(string? url, CancellationToken ct = default)
    {
        if (_page is null)
        {
            return false;
        }

        var opened = await _page.Context.NewPageAsync();
        if (!string.IsNullOrWhiteSpace(url))
        {
            await opened.GotoAsync(url, new PageGotoOptions { WaitUntil = WaitUntilState.DOMContentLoaded, Timeout = 30_000 });
        }

        AdoptPage(opened);
        return true;
    }

    /// <summary>
    /// Closes the active tab and falls back to the last surviving one. Refuses to close the final
    /// tab, since a session with no page cannot do anything afterwards.
    /// </summary>
    public async Task<bool> CloseCurrentTabAsync(CancellationToken ct = default)
    {
        var pages = OpenPages();
        if (_page is null || pages.Count <= 1)
        {
            return false;
        }

        var closing = _page;
        await closing.CloseAsync();

        var survivor = OpenPages().LastOrDefault();
        if (survivor is not null)
        {
            await ActivateAsync(survivor);
        }

        return true;
    }

    private List<IPage> OpenPages() =>
        _browser?.Contexts.SelectMany(c => c.Pages).Where(p => !p.IsClosed).ToList() ?? [];

    private async Task ActivateAsync(IPage page)
    {
        _page = page;
        // A different tab means a different frame tree; anything we had switched into is gone.
        _frameSelectors.Clear();

        try
        {
            await page.BringToFrontAsync();
        }
        catch
        {
            // Headless browsers have nothing to bring forward.
        }

        if (_onFrame is not null)
        {
            _ = FollowScreencastAsync(page);
        }
    }

    // ── History ─────────────────────────────────────────────────────────────────────────────

    public async Task<bool> GoBackAsync(CancellationToken ct = default)
    {
        if (_page is null)
        {
            return false;
        }

        var response = await _page.GoBackAsync(new PageGoBackOptions { WaitUntil = WaitUntilState.DOMContentLoaded });
        await SettleActivePageAsync(600);
        return response is not null;
    }

    public async Task<bool> GoForwardAsync(CancellationToken ct = default)
    {
        if (_page is null)
        {
            return false;
        }

        var response = await _page.GoForwardAsync(new PageGoForwardOptions { WaitUntil = WaitUntilState.DOMContentLoaded });
        await SettleActivePageAsync(600);
        return response is not null;
    }

    public async Task<bool> ReloadAsync(CancellationToken ct = default)
    {
        if (_page is null)
        {
            return false;
        }

        await _page.ReloadAsync(new PageReloadOptions { WaitUntil = WaitUntilState.DOMContentLoaded });
        await SettleActivePageAsync(600);
        return true;
    }

    // ── Browser state ───────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Sets a cookie on the active context. Used to start a scenario already authenticated instead
    /// of replaying the login UI in every single test.
    /// </summary>
    public async Task<bool> SetCookieAsync(string name, string? value, string? domain, string? path, CancellationToken ct = default)
    {
        if (_page is null || string.IsNullOrWhiteSpace(name))
        {
            return false;
        }

        var cookie = new Cookie
        {
            Name = name,
            Value = value ?? string.Empty,
            Path = string.IsNullOrWhiteSpace(path) ? "/" : path
        };

        if (string.IsNullOrWhiteSpace(domain))
        {
            // Without an explicit domain, scope the cookie to the page we are on. Playwright
            // requires either a URL or a domain+path pair, and guessing the domain from the URL
            // gets subdomains wrong.
            cookie.Url = _page.Url;
        }
        else
        {
            cookie.Domain = domain;
        }

        await _page.Context.AddCookiesAsync([cookie]);
        return true;
    }

    public async Task<bool> ClearCookiesAsync(CancellationToken ct = default)
    {
        if (_page is null)
        {
            return false;
        }

        await _page.Context.ClearCookiesAsync();
        return true;
    }

    /// <summary>Writes a localStorage or sessionStorage entry on the active origin.</summary>
    public async Task<bool> SetStorageAsync(string kind, string key, string? value, CancellationToken ct = default)
    {
        if (_page is null || string.IsNullOrWhiteSpace(key))
        {
            return false;
        }

        var store = kind.Equals("session", StringComparison.OrdinalIgnoreCase) ? "sessionStorage" : "localStorage";

        try
        {
            await _page.EvaluateAsync(
                $"([k, v]) => window.{store}.setItem(k, v)",
                new[] { key, value ?? string.Empty });
            return true;
        }
        catch
        {
            // Storage throws on about:blank and in private-mode-like contexts.
            return false;
        }
    }

    /// <summary>Reads a localStorage or sessionStorage entry; null when absent.</summary>
    public async Task<string?> GetStorageAsync(string kind, string key, CancellationToken ct = default)
    {
        if (_page is null || string.IsNullOrWhiteSpace(key))
        {
            return null;
        }

        var store = kind.Equals("session", StringComparison.OrdinalIgnoreCase) ? "sessionStorage" : "localStorage";

        try
        {
            return await _page.EvaluateAsync<string?>($"k => window.{store}.getItem(k)", key);
        }
        catch
        {
            return null;
        }
    }

    // ── Locator resolution ──────────────────────────────────────────────────────────────────

    /// <summary>
    /// Builds a locator from a stored <see cref="LocatorStrategy"/> and value, inside the current
    /// frame scope. Returns null when the strategy has no browser equivalent (mobile dialects,
    /// visual matching).
    /// </summary>
    private ILocator? BuildLocator(LocatorStrategy strategy, string value)
    {
        if (_page is null || string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        var scope = CurrentScope;

        switch (strategy)
        {
            case LocatorStrategy.CSS:
            case LocatorStrategy.DataAttribute:
                return scope.Locator(value);
            case LocatorStrategy.TestId:
                // Match whichever hook the application actually uses rather than forcing one.
                return scope.Locator(
                    $"[data-testid={Quote(value)}],[data-test={Quote(value)}],[data-qa={Quote(value)}]");
            case LocatorStrategy.XPath:
                return scope.Locator(value.StartsWith("xpath=") ? value : $"xpath={value}");
            case LocatorStrategy.ARIA:
            case LocatorStrategy.NearbyLabel:
                return scope.Locator($"[aria-label={Quote(value)}]");
            case LocatorStrategy.Placeholder:
                return scope.GetByPlaceholder(value);
            case LocatorStrategy.Text:
                return scope.GetByText(value);
            case LocatorStrategy.Label:
                return scope.GetByLabel(value);
            case LocatorStrategy.AltText:
                return scope.GetByAltText(value);
            case LocatorStrategy.Title:
                return scope.GetByTitle(value);
            case LocatorStrategy.Role:
                var parts = value.Split(':', 2);
                if (parts.Length == 2 && Enum.TryParse<AriaRole>(parts[0], ignoreCase: true, out var role))
                {
                    return scope.GetByRole(role, parts[1]);
                }

                return scope.GetByText(parts[^1]);
            default:
                // Visual and the mobile selector dialects are not addressable by Playwright.
                return null;
        }
    }

    /// <summary>
    /// Resolves a locator to a single element, waiting for it to attach.
    /// </summary>
    /// <param name="index">
    /// Which match to take when several are expected (the third row's Delete button).
    /// </param>
    /// <param name="allowAmbiguous">
    /// When false, a locator matching several elements resolves to nothing: acting on an arbitrary
    /// one of them is how a test ends up silently exercising the wrong control. Callers retry with
    /// true only after every candidate has failed, so a page with genuinely repeated markup still
    /// runs rather than blocking on a technicality.
    /// </param>
    public async Task<ILocator?> ResolveAsync(
        LocatorStrategy strategy,
        string value,
        int? index,
        int timeoutMs,
        bool allowAmbiguous = false,
        CancellationToken ct = default)
    {
        var locator = BuildLocator(strategy, value);
        if (locator is null)
        {
            return null;
        }

        try
        {
            if (index is { } position)
            {
                var nth = locator.Nth(position);
                await nth.WaitForAsync(new LocatorWaitForOptions { State = WaitForSelectorState.Attached, Timeout = timeoutMs });
                return nth;
            }

            await locator.First.WaitForAsync(new LocatorWaitForOptions { State = WaitForSelectorState.Attached, Timeout = timeoutMs });

            if (allowAmbiguous)
            {
                return locator.First;
            }

            var matches = await locator.CountAsync();
            return matches == 1 ? locator.First : null;
        }
        catch
        {
            return null;
        }
    }

    /// <summary>Number of elements a strategy/value pair matches in the current scope.</summary>
    public async Task<int> CountInScopeAsync(LocatorStrategy strategy, string value, CancellationToken ct = default)
    {
        try
        {
            var locator = BuildLocator(strategy, value);
            return locator is null ? 0 : await locator.CountAsync();
        }
        catch
        {
            return 0;
        }
    }

    /// <summary>
    /// Derives a durable locator for an element the agent addressed by snapshot ref.
    /// </summary>
    /// <remarks>
    /// The ref itself is an attribute we stamp on the DOM for the lifetime of one snapshot, so it
    /// cannot be recorded. Reading a lasting identity off the same element at the moment the action
    /// succeeds is what turns an exploratory click into a step that replays deterministically —
    /// otherwise every future run has to re-derive the element from its wording, and the wording is
    /// exactly what marketing changes. Preference order follows durability: an explicit test hook
    /// outlives a redesign, an id usually survives it, a role plus accessible name survives most
    /// re-styling, and a name attribute is the last thing that still means something.
    /// </remarks>
    public async Task<(LocatorStrategy Strategy, string Value)?> DescribeRefAsync(string reference, CancellationToken ct = default)
    {
        var locator = ResolveRefLocator(reference);
        if (locator is null)
        {
            return null;
        }

        try
        {
            if (await locator.CountAsync() == 0)
            {
                return null;
            }

            var descriptor = await locator.EvaluateAsync<string?>(
                """
                el => {
                  const testId = el.getAttribute('data-testid') || el.getAttribute('data-test') || el.getAttribute('data-qa');
                  if (testId) return 'TestId|' + testId;
                  const id = el.getAttribute('id');
                  if (id && !/\d{4,}/.test(id)) return 'CSS|[id="' + id + '"]';
                  const label = (el.getAttribute('aria-label') || '').trim();
                  const role = el.getAttribute('role')
                    || (el.tagName === 'BUTTON' ? 'button'
                      : el.tagName === 'A' ? 'link'
                      : el.tagName === 'SELECT' ? 'combobox'
                      : el.tagName === 'TEXTAREA' ? 'textbox'
                      : el.tagName === 'INPUT' ? ((el.type === 'submit' || el.type === 'button') ? 'button' : 'textbox')
                      : '');
                  const name = label || (el.innerText || '').trim() || (el.getAttribute('placeholder') || '').trim();
                  if (role && name) return 'Role|' + role + ':' + name.slice(0, 80);
                  const nameAttr = el.getAttribute('name');
                  if (nameAttr) return 'CSS|' + el.tagName.toLowerCase() + '[name="' + nameAttr + '"]';
                  return '';
                }
                """);

            if (string.IsNullOrWhiteSpace(descriptor))
            {
                return null;
            }

            var parts = descriptor!.Split('|', 2);
            if (parts.Length != 2 || string.IsNullOrWhiteSpace(parts[1]))
            {
                return null;
            }

            return parts[0] switch
            {
                "TestId" => (LocatorStrategy.TestId, parts[1]),
                "Role" => (LocatorStrategy.Role, parts[1]),
                "CSS" => (LocatorStrategy.CSS, parts[1]),
                _ => null
            };
        }
        catch
        {
            return null;
        }
    }

    /// <summary>Attaches files to a file input. Missing paths are rejected before Playwright is asked.</summary>
    public async Task<bool> UploadFilesAsync(ILocator target, IReadOnlyList<string> paths, CancellationToken ct = default)
    {
        if (paths.Count == 0 || paths.Any(p => !File.Exists(p)))
        {
            return false;
        }

        await target.SetInputFilesAsync([.. paths]);
        return true;
    }

    /// <summary>
    /// Waits until a condition holds for a locator. Returned as a bool rather than thrown so a
    /// timeout reads as a test failure with context, not as an engine crash.
    /// </summary>
    public async Task<bool> WaitForStateAsync(ILocator target, string condition, int timeoutMs, CancellationToken ct = default)
    {
        try
        {
            switch (condition.Trim().ToLowerInvariant())
            {
                case "hidden":
                    await target.WaitForAsync(new LocatorWaitForOptions { State = WaitForSelectorState.Hidden, Timeout = timeoutMs });
                    return true;
                case "detached":
                    await target.WaitForAsync(new LocatorWaitForOptions { State = WaitForSelectorState.Detached, Timeout = timeoutMs });
                    return true;
                case "enabled":
                    await target.WaitForAsync(new LocatorWaitForOptions { State = WaitForSelectorState.Visible, Timeout = timeoutMs });
                    return await target.IsEnabledAsync();
                default:
                    await target.WaitForAsync(new LocatorWaitForOptions { State = WaitForSelectorState.Visible, Timeout = timeoutMs });
                    return true;
            }
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// A place locators can be built against — the page, or an iframe the session switched into.
    /// </summary>
    /// <remarks>
    /// Playwright's .NET bindings give <see cref="IPage"/> and <see cref="IFrameLocator"/> the same
    /// query methods without a shared interface, so every frame-aware call site would otherwise
    /// need its own if/else. This wrapper unifies them in one place.
    /// </remarks>
    private readonly struct ElementScope
    {
        private readonly IPage? _page;
        private readonly IFrameLocator? _frame;

        private ElementScope(IPage? page, IFrameLocator? frame)
        {
            _page = page;
            _frame = frame;
        }

        public static ElementScope Of(IPage page) => new(page, null);

        public static ElementScope Of(IFrameLocator frame) => new(null, frame);

        public ILocator Locator(string selector) =>
            _page is not null ? _page.Locator(selector) : _frame!.Locator(selector);

        public ILocator GetByText(string text) =>
            _page is not null ? _page.GetByText(text) : _frame!.GetByText(text);

        public ILocator GetByLabel(string text) =>
            _page is not null ? _page.GetByLabel(text) : _frame!.GetByLabel(text);

        public ILocator GetByPlaceholder(string text) =>
            _page is not null ? _page.GetByPlaceholder(text) : _frame!.GetByPlaceholder(text);

        public ILocator GetByAltText(string text) =>
            _page is not null ? _page.GetByAltText(text) : _frame!.GetByAltText(text);

        public ILocator GetByTitle(string text) =>
            _page is not null ? _page.GetByTitle(text) : _frame!.GetByTitle(text);

        public ILocator GetByRole(AriaRole role, string name) =>
            _page is not null
                ? _page.GetByRole(role, new PageGetByRoleOptions { Name = name, Exact = false })
                : _frame!.GetByRole(role, new FrameLocatorGetByRoleOptions { Name = name, Exact = false });
    }
}
