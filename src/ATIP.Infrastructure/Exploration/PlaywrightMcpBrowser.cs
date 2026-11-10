using System.Globalization;
using System.Text.Json;
using System.Text.RegularExpressions;
using ATIP.Application.Common.Interfaces;
using Microsoft.Extensions.Logging;
using ModelContextProtocol.Client;
using ModelContextProtocol.Protocol;

namespace ATIP.Infrastructure.Exploration;

/// <summary>
/// Drives a browser through the official Playwright MCP server (<c>@playwright/mcp</c>) over stdio,
/// using the real Model Context Protocol tool surface — <c>browser_navigate</c>, <c>browser_snapshot</c>,
/// <c>browser_click</c>, <c>browser_type</c>, <c>browser_press_key</c>, <c>browser_take_screenshot</c> —
/// exactly as Copilot/Claude drive a browser. The MCP server owns and installs its own Chromium.
/// </summary>
public sealed class PlaywrightMcpBrowser : IAsyncDisposable, IAssertionPageReader
{
    private readonly ILogger _logger;
    private readonly string _command;
    private readonly IList<string> _args;
    private readonly SemaphoreSlim _gate = new(1, 1);
    private McpClient? _client;

    /// <summary>Best-effort current page URL, parsed from the latest navigate/snapshot output.</summary>
    public string CurrentUrl { get; private set; } = string.Empty;

    /// <summary>Best-effort current document title, parsed from the latest navigate/snapshot output.</summary>
    public string CurrentTitle { get; private set; } = string.Empty;

    public PlaywrightMcpBrowser(ILogger logger, string command, IList<string> args)
    {
        _logger = logger;
        _command = command;
        _args = args;
    }

    public async Task InitializeAsync(CancellationToken ct = default)
    {
        var transport = new StdioClientTransport(new StdioClientTransportOptions
        {
            Name = "playwright-mcp",
            Command = _command,
            Arguments = _args,
        });

        _client = await McpClient.CreateAsync(transport, cancellationToken: ct);
        _logger.LogInformation("Playwright MCP server started ({Command} {Args}).", _command, string.Join(' ', _args));
    }

    public async Task<string> NavigateAsync(string url, CancellationToken ct = default)
    {
        CurrentUrl = url;
        var text = await CallTextAsync("browser_navigate", new Dictionary<string, object?> { ["url"] = url }, ct);
        UpdateUrlFrom(text);
        return text;
    }

    /// <summary>Returns the accessibility (ARIA) snapshot with [ref=eN] handles the agent selects from.</summary>
    public async Task<string> SnapshotAsync(CancellationToken ct = default)
    {
        var text = await CallTextAsync("browser_snapshot", null, ct);
        UpdateUrlFrom(text);
        return text;
    }

    // NOTE: the snapshot handle argument is called "target" (and is REQUIRED) — sending the older "ref"
    // name makes the server reject every call with a schema-validation error, which silently downgraded
    // ALL interactions to the JS descriptor fallbacks below.
    public async Task<bool> ClickAsync(string element, string reference, CancellationToken ct = default) =>
        (await CallAsync("browser_click", new Dictionary<string, object?> { ["element"] = element, ["target"] = reference }, ct)).Ok;

    public async Task<bool> TypeAsync(string element, string reference, string text, CancellationToken ct = default) =>
        (await CallAsync("browser_type", new Dictionary<string, object?> { ["element"] = element, ["target"] = reference, ["text"] = text }, ct)).Ok;

    public async Task<bool> PressKeyAsync(string key, CancellationToken ct = default) =>
        (await CallAsync("browser_press_key", new Dictionary<string, object?> { ["key"] = key }, ct)).Ok;

    /// <summary>
    /// Chooses an option in a native &lt;select&gt;. Native dropdowns cannot be driven by clicking their
    /// options (they are not in the accessibility snapshot), so this is a distinct action.
    /// </summary>
    public async Task<bool> SelectOptionAsync(string element, string reference, string option, CancellationToken ct = default) =>
        (await CallAsync("browser_select_option", new Dictionary<string, object?>
        {
            ["element"] = element,
            ["target"] = reference,
            ["values"] = new[] { option },
        }, ct)).Ok;

    public async Task WaitAsync(double seconds, CancellationToken ct = default) =>
        await CallAsync("browser_wait_for", new Dictionary<string, object?> { ["time"] = seconds }, ct);

    /// <summary>
    /// Ref-free click by an element's human descriptor (accessible name / visible text / value), located
    /// and clicked in-page via JS. This is resilient to stale snapshot refs on dynamic SPAs (carousels,
    /// lazy-loaded content) where an <c>[ref=eN]</c> can detach between snapshot and click. Returns true if
    /// a matching visible control was clicked.
    /// </summary>
    public async Task<bool> ClickByDescriptorAsync(string descriptor, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(descriptor))
        {
            return false;
        }

        var want = System.Text.Json.JsonSerializer.Serialize(descriptor);
        var js = $$"""
            () => {
              const want = ({{want}} || '').replace(/\s+/g, ' ').trim().toLowerCase();
              if (!want) return false;
              const norm = (s) => (s || '').replace(/\s+/g, ' ').trim().toLowerCase();
              const vis = (n) => { const r = n.getBoundingClientRect(); const s = getComputedStyle(n); return r.width > 0 && r.height > 0 && s.visibility !== 'hidden' && s.display !== 'none'; };
              // Native checkboxes/radios for filter options (Brand, Size, etc.) are very often visually
              // hidden behind a custom-styled box (opacity:0 / clip / 1px) while still being the actual
              // clickable/toggleable element — only exclude these if truly display:none (removed from layout).
              const visInput = (n) => getComputedStyle(n).display !== 'none';
              const labelFor = (n) => {
                if (n.id) { const l = document.querySelector('label[for="' + CSS.escape(n.id) + '"]'); if (l) return l; }
                return n.closest('label');
              };
              const nameOf = (n) => {
                if (n.tagName === 'INPUT' && (n.type === 'checkbox' || n.type === 'radio')) {
                  const l = labelFor(n);
                  if (l) return norm(l.innerText || l.textContent);
                }
                return norm(n.getAttribute('aria-label') || n.title || n.value || n.innerText || n.textContent);
              };
              const buttonish = [...document.querySelectorAll('a,button,[role=button],[role=link],[role=menuitem],[role=tab],[role=option],input[type=submit],input[type=button],[onclick]')].filter(vis);
              const checkish = [...document.querySelectorAll('input[type=checkbox],input[type=radio],[role=checkbox],[role=radio],label')].filter(n => vis(n) || visInput(n));
              const cands = [...buttonish, ...checkish];
              let el = cands.find(n => nameOf(n) === want)
                    || cands.find(n => nameOf(n).startsWith(want))
                    || cands.find(n => nameOf(n).includes(want));
              if (!el) {
                // Last resort: many real sites (e.g. Flipkart's filter chips) render clickable options as
                // a plain div/span/li with a JS-attached click handler (React synthetic events — no real
                // onclick attribute, no ARIA role, no native input) — invisible to the selectors above.
                // Fall back to ANY visible, leaf-ish element (no element children, i.e. it IS the clickable
                // text node's wrapper) whose own text matches, filtering out large containers so we don't
                // click a whole card/section by accident.
                const generic = [...document.querySelectorAll('div,span,li,p')]
                  .filter(n => vis(n) && n.children.length === 0 && norm(n.textContent).length > 0 && norm(n.textContent).length < 60);
                el = generic.find(n => norm(n.textContent) === want)
                  || generic.find(n => norm(n.textContent).startsWith(want));
              }
              if (!el) return false;
              el.scrollIntoView({ block: 'center', inline: 'center' });
              el.click();
              return true;
            }
            """;
        var res = await EvaluateAsync(js, ct);
        return ResultIsTrue(res);
    }

    /// <summary>
    /// Ref-free typing by an input's human descriptor (label / placeholder / accessible name), located and
    /// filled in-page via JS (with input/change events dispatched). Resilient to stale refs like
    /// <see cref="ClickByDescriptorAsync"/>. Returns true if a matching visible field was filled.
    /// </summary>
    public async Task<bool> FillByDescriptorAsync(string descriptor, string value, CancellationToken ct = default)
    {
        var want = System.Text.Json.JsonSerializer.Serialize(descriptor ?? string.Empty);
        var val = System.Text.Json.JsonSerializer.Serialize(value ?? string.Empty);
        var js = $$"""
            () => {
              const want = ({{want}} || '').replace(/\s+/g, ' ').trim().toLowerCase();
              const value = {{val}};
              const norm = (s) => (s || '').replace(/\s+/g, ' ').trim().toLowerCase();
              const vis = (n) => { const r = n.getBoundingClientRect(); const s = getComputedStyle(n); return r.width > 0 && r.height > 0 && s.visibility !== 'hidden' && s.display !== 'none'; };
              const fields = [...document.querySelectorAll('input:not([type=hidden]):not([type=submit]):not([type=button]),textarea,[contenteditable=true],[role=searchbox],[role=textbox]')].filter(vis);
              const labelText = (n) => {
                let t = n.getAttribute('aria-label') || n.placeholder || n.name || n.title || '';
                if (n.id) { const l = document.querySelector('label[for="' + CSS.escape(n.id) + '"]'); if (l) t += ' ' + l.textContent; }
                return norm(t);
              };
              let el = !want ? fields[0]
                : (fields.find(n => labelText(n) === want) || fields.find(n => labelText(n).includes(want)) || fields[0]);
              if (!el) return false;
              el.focus();
              if (el.isContentEditable) { el.textContent = value; }
              else {
                const setter = Object.getOwnPropertyDescriptor(el.__proto__, 'value')?.set;
                setter ? setter.call(el, value) : (el.value = value);
              }
              el.dispatchEvent(new Event('input', { bubbles: true }));
              el.dispatchEvent(new Event('change', { bubbles: true }));
              return true;
            }
            """;
        var res = await EvaluateAsync(js, ct);
        return ResultIsTrue(res);
    }

    /// <summary>
    /// Stabilises a dynamic page for reliable automation: pauses CSS animations/transitions and disables
    /// smooth scrolling, so elements stop moving. Playwright refuses to click a non-"stable" (animating)
    /// element, so carousels/marquees otherwise cause click timeouts on real sites. Also forces every
    /// navigation to stay in the CURRENT tab. Idempotent per page; must be re-applied after navigations
    /// (the injected style and listener are lost on document replacement).
    /// </summary>
    public async Task StabilizePageAsync(CancellationToken ct = default)
    {
        const string js = """
            () => {
              try {
                if (!document.getElementById('__atip_freeze')) {
                  const st = document.createElement('style');
                  st.id = '__atip_freeze';
                  st.textContent = '*,*::before,*::after{animation-duration:0s !important;animation-delay:0s !important;animation-play-state:paused !important;transition-duration:0s !important;transition-delay:0s !important;scroll-behavior:auto !important;}';
                  (document.head || document.documentElement).appendChild(st);
                }
                // Real catalogues open detail pages in a NEW TAB (every Flipkart product card is
                // target="_blank"). The MCP server keeps snapshotting and clicking the ORIGINAL tab, so the
                // click looks like it "worked" while the agent stays on the results page and every later
                // step ("Add to cart") then fails on a page that never changed. Stripping the attribute is
                // preferable to intercepting the click: the anchor keeps its NATIVE behaviour, so the site's
                // own handlers and Playwright's navigation waiting both still work. A MutationObserver keeps
                // lazily rendered and re-rendered cards covered.
                const untarget = (root) => {
                  const list = root && root.querySelectorAll ? root.querySelectorAll('a[target]') : [];
                  for (const a of list) {
                    if (a.target && a.target !== '_self') { a.removeAttribute('target'); }
                  }
                };
                untarget(document);
                if (!window.__atipSameTab) {
                  window.__atipSameTab = true;
                  new MutationObserver(() => untarget(document)).observe(
                    document.documentElement, { childList: true, subtree: true });
                  // Popups opened from script bypass the anchor path entirely; send them to this tab too.
                  const nativeOpen = window.open;
                  window.open = function (u) {
                    if (u) {
                      window.__atipNavPending = true;
                      setTimeout(() => { window.__atipNavPending = false; }, 8000);
                      window.location.href = u;
                      return window;
                    }
                    return nativeOpen.apply(window, arguments);
                  };
                }
                return true;
              } catch { return false; }
            }
            """;
        await EvaluateAsync(js, ct);
    }

    // "- 0: (current) [Some Title](https://example.com/)" — the shape browser_tabs uses for each tab.
    private static readonly Regex TabLineRegex = new(
        @"^\s*-\s*(?<index>\d+):\s*(?<current>\(current\))?\s*\[(?<title>[^\]]*)\]\((?<url>[^)]*)\)",
        RegexOptions.Compiled | RegexOptions.Multiline);

    /// <summary>Last known open tabs, refreshed by <see cref="ListTabsAsync"/>.</summary>
    public IReadOnlyList<BrowserTabInfo> Tabs { get; private set; } = [];

    /// <summary>
    /// Lists every open tab, newest last, flagging which one MCP is currently driving.
    /// </summary>
    public async Task<IReadOnlyList<BrowserTabInfo>> ListTabsAsync(CancellationToken ct = default)
    {
        var (ok, text) = await CallAsync("browser_tabs",
            new Dictionary<string, object?> { ["action"] = "list" }, ct);
        if (!ok || string.IsNullOrWhiteSpace(text))
        {
            return Tabs;
        }

        Tabs = TabLineRegex.Matches(text)
            .Select(m => new BrowserTabInfo(
                int.Parse(m.Groups["index"].Value, CultureInfo.InvariantCulture),
                m.Groups["title"].Value,
                m.Groups["url"].Value,
                m.Groups["current"].Success))
            .OrderBy(t => t.Index)
            .ToList();

        return Tabs;
    }

    /// <summary>
    /// Switches to the most recently opened tab when the site opened one, and reports its URL.
    /// <para>
    /// Preferring same-tab navigation (see <see cref="StabilizePageAsync"/>) is not airtight: the
    /// injected guards die with the document on every navigation, they cannot reach into iframes, and a
    /// site can call <c>window.open</c> from a handler attached after the observer ran. When a popup does
    /// escape, MCP keeps snapshotting and clicking the ORIGINAL tab — so the click looks successful, the
    /// live view keeps showing the old page, and every later step runs against a page the user has
    /// visibly left. Following the newest tab makes the agent behave like a person: whatever the click
    /// actually surfaced is what gets acted on and screenshotted.
    /// </para>
    /// <para>
    /// Blank tabs are ignored — a popup that has not navigated yet carries no content to act on, and
    /// switching to it would strand the agent on an empty page. Nothing is closed, so a popup the site
    /// dismisses itself simply drops off the list and the next call falls back to the remaining tab.
    /// </para>
    /// </summary>
    /// <returns>The URL switched to, or <c>null</c> when the active tab was already the right one.</returns>
    public async Task<string?> FollowNewestTabAsync(CancellationToken ct = default)
    {
        var tabs = await ListTabsAsync(ct);
        if (tabs.Count < 2)
        {
            return null;
        }

        var newest = tabs
            .Where(t => !string.IsNullOrWhiteSpace(t.Url)
                && !t.Url.StartsWith("about:", StringComparison.OrdinalIgnoreCase)
                && !t.Url.StartsWith("chrome://", StringComparison.OrdinalIgnoreCase))
            .OrderByDescending(t => t.Index)
            .FirstOrDefault();

        if (newest is null || newest.IsActive)
        {
            return null;
        }

        var (selected, _) = await CallAsync("browser_tabs",
            new Dictionary<string, object?> { ["action"] = "select", ["index"] = newest.Index }, ct);
        if (!selected)
        {
            return null;
        }

        _logger.LogDebug("Followed popup tab {Index} to {Url}.", newest.Index, newest.Url);
        Tabs = tabs.Select(t => t with { IsActive = t.Index == newest.Index }).ToList();
        CurrentUrl = newest.Url;
        return newest.Url;
    }

    /// <summary>
    /// Waits for the page to settle after an action: polls document.readyState until "complete" (or a
    /// short cap), then polls the body text LENGTH until it stops changing (or a cap) so late async
    /// renders — e.g. a search page that first shows "0 results" then swaps in a fuzzy-match/fallback
    /// result set a few hundred ms later via client-side JS — are captured in their FINAL state instead
    /// of a transient one. Best-effort — never throws. Improves reliability of effect detection and QA
    /// verification on dynamic sites.
    /// </summary>
    public async Task WaitForPageSettledAsync(int maxWaitMs = 4000, CancellationToken ct = default)
    {
        const string readyStateJs = "() => { try { return document.readyState; } catch { return 'complete'; } }";
        const string bodyLengthJs = "() => { try { return String(document.body ? document.body.innerText.length : 0); } catch { return '0'; } }";
        const string navPendingJs = "() => { try { return window.__atipNavPending ? 'pending' : 'idle'; } catch { return 'idle'; } }";
        var waited = 0;
        try
        {
            // A click the same-tab guard rewrote into a location.href assignment leaves the OLD document
            // in place for a moment, and readyState on that document is already "complete" — so without
            // this the caller compares URLs before the navigation commits, concludes the click did nothing,
            // and records a genuine navigation as "no visible effect". The flag lives on the document being
            // replaced, so it disappears exactly when the new page takes over.
            var navWaited = 0;
            while (navWaited < 10000)
            {
                var pending = await EvaluateAsync(navPendingJs, ct);
                if (!pending.Contains("pending", StringComparison.OrdinalIgnoreCase))
                {
                    break;
                }

                await Task.Delay(250, ct);
                navWaited += 250;
            }

            while (waited < maxWaitMs)
            {
                var res = await EvaluateAsync(readyStateJs, ct);
                if (res.Contains("complete", StringComparison.OrdinalIgnoreCase))
                {
                    break;
                }

                await Task.Delay(250, ct);
                waited += 250;
            }

            // Content-stability poll: keep sampling body text length until it stops changing between two
            // consecutive reads (content has settled), capped so a permanently-changing page (e.g. a live
            // counter) can't hang the run.
            var previousLength = await EvaluateAsync(bodyLengthJs, ct);
            for (var i = 0; i < 4; i++)
            {
                await Task.Delay(350, ct);
                var currentLength = await EvaluateAsync(bodyLengthJs, ct);
                if (currentLength == previousLength)
                {
                    break;
                }

                previousLength = currentLength;
            }

            // A full navigation replaces the document, dropping the freeze style and the same-tab guard.
            // Every action is followed by a settle, so re-applying here keeps both invariants true for the
            // snapshot/verification that comes next, without sprinkling calls through every action path.
            await StabilizePageAsync(ct);
        }
        catch
        {
            // Best-effort only.
        }
    }

    /// <summary>
    /// Extracts concrete, QA-relevant data from the LIVE DOM that the ARIA snapshot omits: the page's
    /// visible text, any "N results/items/products" style counts, and the sizes of common list/item
    /// containers. This is the evidence the structured QA verifier reasons over to check quantitative
    /// expectations (limits, thresholds, counts) generically on any site. Returns a JSON string.
    /// </summary>
    public async Task<string> ExtractPageDataAsync(CancellationToken ct = default)
    {
        const string js = """
            () => {
              const norm = (s) => (s || '').replace(/\s+/g, ' ').trim();
              const bodyText = norm(document.body ? document.body.innerText : '');
              // Detect count/quantity phrases like "10,492 results", "of 10,492", "24 products".
              const counts = [];
              const rx = /([\d][\d.,]*)\s*(results?|items?|products?|records?|entries|matches|reviews?|ratings?|found)\b|\bof\s+([\d][\d.,]*)\b/gi;
              let m;
              while ((m = rx.exec(bodyText)) !== null && counts.length < 25) { counts.push(m[0]); }
              // Sizes of common repeated-item containers (helps count displayed results generically).
              const listCounts = [];
              const selectors = ['[role=listitem]', 'li', 'article', '[class*=product]', '[class*=card]', '[data-testid*=product]', '[data-testid*=item]', 'tr'];
              for (const sel of selectors) {
                let n = 0;
                try { n = document.querySelectorAll(sel).length; } catch { n = 0; }
                if (n > 0) { listCounts.push(sel + '=' + n); }
                if (listCounts.length >= 12) { break; }
              }
              return {
                url: location.href,
                title: document.title || '',
                counts,
                listCounts,
                text: bodyText.slice(0, 6000)
              };
            }
            """;
        try
        {
            return await EvaluateAsync(js, ct);
        }
        catch
        {
            return string.Empty;
        }
    }

    private static bool ResultIsTrue(string? evalResult) =>
        !string.IsNullOrEmpty(evalResult)
        && System.Text.RegularExpressions.Regex.IsMatch(evalResult, @"(^|\W)true(\W|$)", System.Text.RegularExpressions.RegexOptions.IgnoreCase);

    /// <summary>
    /// Evaluates a JavaScript function in the page and returns its serialized result text.
    /// <paramref name="jsFunction"/> must be a function expression, e.g. <c>() =&gt; { return document.title; }</c>.
    /// Used for generic, DOM-level operations the ARIA tool surface can't express (e.g. detecting and
    /// dismissing arbitrary blocking overlays at runtime).
    /// </summary>
    public async Task<string> EvaluateAsync(string jsFunction, CancellationToken ct = default) =>
        (await CallAsync("browser_evaluate", new Dictionary<string, object?> { ["function"] = jsFunction }, ct)).Text;

    /// <summary>
    /// Evaluates a JavaScript function against ONE snapshot element, which arrives as the function's
    /// single argument. Used to interrogate the element an action is about to touch while its snapshot
    /// handle is still valid.
    /// </summary>
    public async Task<string> EvaluateOnAsync(string element, string reference, string jsFunction, CancellationToken ct = default) =>
        (await CallAsync(
            "browser_evaluate",
            new Dictionary<string, object?>
            {
                ["function"] = jsFunction,
                ["element"] = element,
                ["target"] = reference,
            },
            ct)).Text;

    /// <summary>
    /// Runs a JS function that returns an object and deserializes it, or returns null if it did not.
    /// <para>
    /// The MCP server echoes the SOURCE of the evaluated function back alongside its result, so the
    /// response text contains the braces of our own code before it contains the braces of the answer.
    /// Scanning for the first <c>{</c> therefore parses the script instead of the result. The function
    /// concatenates its delimiter at runtime so that the token exists ONLY in the value MCP printed and
    /// never in the echoed source, which makes the answer unambiguous to find.
    /// </para>
    /// </summary>
    private async Task<JsonElement?> EvaluateJsonAsync(string jsBody, string? element, string? reference, CancellationToken ct)
    {
        // `body` must evaluate to the value being returned.
        var js = element is null
            ? $"() => {{ const D = 'ATIP' + '\\u00a7'; const v = (() => {{ {jsBody} }})(); return D + JSON.stringify(v) + D; }}"
            : $"(el) => {{ const D = 'ATIP' + '\\u00a7'; const v = ((el) => {{ {jsBody} }})(el); return D + JSON.stringify(v) + D; }}";

        string text;
        try
        {
            text = element is null
                ? await EvaluateAsync(js, ct)
                : await EvaluateOnAsync(element, reference!, js, ct);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            throw;
        }
        catch
        {
            return null;
        }

        if (string.IsNullOrEmpty(text))
        {
            return null;
        }

        // The MCP server's response is markdown: a "### Result" section holding the JSON-STRING-quoted
        // return value, followed by "### Ran Playwright code" which echoes our own source — and that
        // echo contains the literal text "ATIP" too (inside the quoted, backslash-escaped source), just
        // never the real delimiter character our function actually printed. Isolating the Result
        // section first, then unescaping its JSON-string quoting, leaves a plain string in which the
        // delimiter appears in exactly the two places we put it.
        const string resultMarker = "### Result";
        var resultStart = text.IndexOf(resultMarker, StringComparison.Ordinal);
        if (resultStart < 0)
        {
            return null;
        }

        resultStart += resultMarker.Length;
        var resultEnd = text.IndexOf("\n###", resultStart, StringComparison.Ordinal);
        var section = (resultEnd < 0 ? text[resultStart..] : text[resultStart..resultEnd]).Trim();

        string unescaped;
        try
        {
            unescaped = section.StartsWith('"') && section.EndsWith('"')
                ? JsonSerializer.Deserialize<string>(section) ?? section
                : section;
        }
        catch (JsonException)
        {
            return null;
        }

        const string delimiter = "ATIP\u00a7";
        var end = unescaped.LastIndexOf(delimiter, StringComparison.Ordinal);
        if (end <= 0)
        {
            return null;
        }

        var start = unescaped.LastIndexOf(delimiter, end - 1, StringComparison.Ordinal);
        if (start < 0)
        {
            return null;
        }

        var payload = unescaped[(start + delimiter.Length)..end];

        try
        {
            using var doc = JsonDocument.Parse(payload);
            return doc.RootElement.Clone();
        }
        catch (JsonException)
        {
            return null;
        }
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<string>?> ReadValuesAsync(
        string selector,
        string source = "text",
        int max = 200,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(selector))
        {
            return null;
        }

        var body = $$"""
            const selector = {{JsonSerializer.Serialize(selector)}};
            const source = {{JsonSerializer.Serialize(source)}};
            const max = {{max}};
            let nodes;
            try { nodes = document.querySelectorAll(selector); }
            catch { return { ok: false, values: [] }; }

            const rendered = (el) => {
              if (!(el instanceof Element)) return false;
              const style = window.getComputedStyle(el);
              if (style.visibility === 'hidden' || style.display === 'none') return false;
              return el.getClientRects().length > 0;
            };

            const out = [];
            for (const el of nodes) {
              if (out.length >= max) break;
              if (!rendered(el)) continue;
              if (source === 'value') {
                if (el.tagName === 'SELECT') {
                  const opt = el.selectedOptions && el.selectedOptions[0];
                  out.push(((opt ? (opt.textContent || opt.value) : el.value) || '').trim());
                } else if ('value' in el) {
                  out.push(String(el.value == null ? '' : el.value).trim());
                } else {
                  out.push((el.textContent || '').replace(/\s+/g, ' ').trim());
                }
              } else {
                out.push((el.textContent || '').replace(/\s+/g, ' ').trim());
              }
            }
            return { ok: true, values: out };
            """;

        var result = await EvaluateJsonAsync(body, null, null, ct);
        if (result is not { } root
            || !root.TryGetProperty("ok", out var ok)
            || ok.ValueKind != JsonValueKind.True)
        {
            return null;
        }

        return root.TryGetProperty("values", out var values) && values.ValueKind == JsonValueKind.Array
            ? values.EnumerateArray().Select(v => v.GetString() ?? string.Empty).ToList()
            : new List<string>();
    }

    /// <inheritdoc />
    public async Task<string> GetAssertionContextAsync(int maxGroups = 120, CancellationToken ct = default)
    {
        var body = $$"""
            const maxGroups = {{maxGroups}};
            const rendered = (el) => {
              const style = window.getComputedStyle(el);
              if (style.visibility === 'hidden' || style.display === 'none') return false;
              return el.getClientRects().length > 0;
            };

            // Prefer attributes a developer chose over ones a bundler generated: hashed class names
            // change on every deploy and would produce an assertion that rots immediately.
            const unstable = (s) => !s || s.length < 2 || /\d{3}/.test(s) || /^(css|sc|jsx)-/.test(s);

            const selectorFor = (el) => {
              for (const attr of ['data-test', 'data-testid', 'data-qa']) {
                const v = el.getAttribute && el.getAttribute(attr);
                if (v) return '[' + attr + '="' + v + '"]';
              }
              if (el.id && !unstable(el.id)) return '#' + el.id;
              // Prefer semantic attributes over classnames: short, framework-generated class hashes
              // (e.g. CSS-module suffixes like "nw1UBF") don't contain 3+ digits or a css-/sc-/jsx-
              // prefix, so they slip past the 'unstable' filter below despite regenerating on every
              // deploy or session — a compiled selector built from one would drift the next run.
              const name = el.getAttribute && el.getAttribute('name');
              if (name) return el.tagName.toLowerCase() + '[name="' + name + '"]';
              const placeholder = el.getAttribute && el.getAttribute('placeholder');
              if (placeholder) return el.tagName.toLowerCase() + '[placeholder="' + placeholder + '"]';
              const ariaLabel = el.getAttribute && el.getAttribute('aria-label');
              if (ariaLabel) return '[aria-label="' + ariaLabel + '"]';
              const cls = (typeof el.className === 'string' ? el.className : '')
                .trim().split(/\s+/).filter(c => !unstable(c)).slice(0, 2);
              return el.tagName.toLowerCase() + cls.map(c => '.' + c).join('');
            };

            const groups = new Map();
            for (const el of document.querySelectorAll('*')) {
              const tag = el.tagName;
              const isField = tag === 'SELECT' || tag === 'INPUT' || tag === 'TEXTAREA';
              // Leaf elements carry the page's real values; wrappers just repeat their children.
              if (!isField && el.children.length > 0) continue;
              if (!rendered(el)) continue;

              let value;
              if (tag === 'SELECT') {
                const opt = el.selectedOptions && el.selectedOptions[0];
                value = ((opt ? (opt.textContent || opt.value) : el.value) || '').trim();
              } else if (isField) {
                value = String(el.value == null ? '' : el.value).trim();
              } else {
                value = (el.textContent || '').replace(/\s+/g, ' ').trim();
              }
              if (!value) continue;

              const key = (isField ? 'value|' : 'text|') + selectorFor(el);
              if (!groups.has(key)) groups.set(key, []);
              groups.get(key).push(value.length > 40 ? value.slice(0, 40) + '\u2026' : value);
            }

            const out = [];
            // Repeated groups first: those are the lists ordering and count checks target.
            const sorted = [...groups.entries()].sort((a, b) => b[1].length - a[1].length);
            for (const [key, values] of sorted.slice(0, maxGroups)) {
              const parts = key.split('|');
              out.push(parts[0] + ' ' + parts[1] + ' (' + values.length + ' match' + (values.length === 1 ? '' : 'es') + ') -> ' + values.slice(0, 3).join(' | '));
            }
            return { lines: out };
            """;

        var result = await EvaluateJsonAsync(body, null, null, ct);
        if (result is not { } root || !root.TryGetProperty("lines", out var lines) || lines.ValueKind != JsonValueKind.Array)
        {
            return string.Empty;
        }

        return string.Join("\n", lines.EnumerateArray().Select(v => v.GetString() ?? string.Empty));
    }

    /// <summary>
    /// Derives durable ways to address the element behind a snapshot handle, ordered best-first, and
    /// keeps only those that are PROVEN to match that exact element and nothing else on the page.
    /// <para>
    /// This is what lets one exploration arm every later run. A snapshot <c>ref</c> is a handle into the
    /// current page render and is meaningless in a fresh browser, so an exploration that recorded only
    /// refs leaves the next run with nothing to aim at. Resolving each candidate back to the live DOM
    /// here — and discarding any that match zero or several nodes — means a locator is stored only once
    /// it has been demonstrated to be unambiguous, rather than because it looked plausible.
    /// </para>
    /// </summary>
    /// <returns>Proven (strategy, value) pairs, best-first. Empty when the element has no stable identity.</returns>
    public async Task<IReadOnlyList<(string Strategy, string Value)>> DescribeElementAsync(
        string element,
        string reference,
        CancellationToken ct = default)
    {
        const string body = """
            if (!el || !(el instanceof Element)) return { candidates: [] };

            const norm = (s) => (s || '').replace(/\s+/g, ' ').trim();
            const unstable = (s) => !s || s.length < 2 || /\d{3}/.test(s) || /^(css|sc|jsx)-/.test(s);
            const cssEscape = (s) => (window.CSS && CSS.escape) ? CSS.escape(s) : String(s).replace(/["\\]/g, '\\$&');

            // The control that actually carries the behaviour is often an ancestor of the node the
            // accessibility tree points at (the snapshot resolves to a <span> inside a <button>).
            // Storing the span yields a locator that resolves but does nothing when clicked.
            const interactive = el.closest('a,button,input,select,textarea,[role=button],[role=link],[role=checkbox],[role=radio],[role=tab],[role=menuitem],[onclick]') || el;

            const out = [];
            const add = (strategy, value) => { if (value && norm(value).length > 0) out.push({ strategy, value: String(value) }); };

            for (const node of [interactive, el]) {
              for (const attr of ['data-test', 'data-testid', 'data-qa']) {
                const v = node.getAttribute && node.getAttribute(attr);
                if (v) add('TestId', v);
              }
              if (node.id && !unstable(node.id)) add('CSS', '#' + cssEscape(node.id));
              const name = node.getAttribute && node.getAttribute('name');
              if (name && !unstable(name)) add('CSS', node.tagName.toLowerCase() + '[name="' + cssEscape(name) + '"]');
              const aria = node.getAttribute && node.getAttribute('aria-label');
              if (aria) add('ARIA', norm(aria));
              const ph = node.getAttribute && node.getAttribute('placeholder');
              if (ph) add('Placeholder', norm(ph));
              const title = node.getAttribute && node.getAttribute('title');
              if (title) add('Title', norm(title));
              const alt = node.getAttribute && node.getAttribute('alt');
              if (alt) add('AltText', norm(alt));
            }

            // Role + accessible name is the most portable semantic locator and survives restyling.
            const tag = interactive.tagName.toLowerCase();
            const explicitRole = interactive.getAttribute && interactive.getAttribute('role');
            const roleFor = () => {
              if (explicitRole) return explicitRole;
              if (tag === 'a') return 'link';
              if (tag === 'button') return 'button';
              if (tag === 'select') return 'combobox';
              if (tag === 'textarea') return 'textbox';
              if (tag === 'input') {
                const t = (interactive.getAttribute('type') || 'text').toLowerCase();
                if (t === 'submit' || t === 'button' || t === 'reset') return 'button';
                if (t === 'checkbox') return 'checkbox';
                if (t === 'radio') return 'radio';
                return 'textbox';
              }
              return null;
            };
            const role = roleFor();
            const accName = norm(
              (interactive.getAttribute && (interactive.getAttribute('aria-label') || interactive.getAttribute('title')))
              || interactive.innerText
              || interactive.textContent
              || (interactive.getAttribute && interactive.getAttribute('value'))
              || '');
            const shortName = accName.length > 80 ? accName.slice(0, 80) : accName;
            if (role && shortName) {
              add('Role', role.charAt(0).toUpperCase() + role.slice(1) + ':' + shortName);
            }

            // Prove each candidate before offering it: a locator that matches nothing, or matches many
            // things, is worse than none because it fails or acts on the wrong element later.
            const matches = (c) => {
              try {
                if (c.strategy === 'TestId') {
                  return document.querySelectorAll('[data-test="' + cssEscape(c.value) + '"],[data-testid="' + cssEscape(c.value) + '"],[data-qa="' + cssEscape(c.value) + '"]');
                }
                if (c.strategy === 'CSS') return document.querySelectorAll(c.value);
                if (c.strategy === 'ARIA') return document.querySelectorAll('[aria-label="' + cssEscape(c.value) + '"]');
                if (c.strategy === 'Placeholder') return document.querySelectorAll('[placeholder="' + cssEscape(c.value) + '"]');
                if (c.strategy === 'Title') return document.querySelectorAll('[title="' + cssEscape(c.value) + '"]');
                if (c.strategy === 'AltText') return document.querySelectorAll('[alt="' + cssEscape(c.value) + '"]');
                return null;
              } catch { return null; }
            };

            const proven = [];
            const seen = new Set();
            for (const c of out) {
              const sig = c.strategy + '|' + c.value;
              if (seen.has(sig)) continue;
              seen.add(sig);

              // Role locators are resolved by the run engine's own accessible-name matching rather
              // than by a CSS query, so they are carried through on the element's own evidence.
              if (c.strategy === 'Role') { proven.push(c); continue; }

              const found = matches(c);
              if (!found || found.length !== 1) continue;
              if (found[0] !== interactive && found[0] !== el) continue;
              proven.push(c);
            }

            return { candidates: proven, role: role || '', name: shortName };
            """;

        var result = await EvaluateJsonAsync(body, element, reference, ct);
        if (result is not { } root
            || !root.TryGetProperty("candidates", out var candidates)
            || candidates.ValueKind != JsonValueKind.Array)
        {
            return Array.Empty<(string, string)>();
        }

        var list = new List<(string, string)>();
        foreach (var candidate in candidates.EnumerateArray())
        {
            var strategy = candidate.TryGetProperty("strategy", out var s) ? s.GetString() : null;
            var value = candidate.TryGetProperty("value", out var v) ? v.GetString() : null;
            if (!string.IsNullOrWhiteSpace(strategy) && !string.IsNullOrWhiteSpace(value))
            {
                list.Add((strategy!, value!));
            }
        }

        return list;
    }

    /// <summary>
    /// Centres a snapshot element in the viewport, best-effort.
    /// <para>
    /// Step screenshots capture the VIEWPORT, not the full page, so a control far down a long document
    /// (a product page's "Add to cart" sits ~700 snapshot lines in) is missing from the very evidence
    /// meant to prove the step acted on it — the shot shows the page header instead. Playwright's own
    /// auto-scroll only guarantees an element is *reachable* at click time; it says nothing about where
    /// the page sits afterwards, once the click has re-rendered or collapsed content.
    /// </para>
    /// A ref that went stale (because the action navigated) simply fails here, which is correct: the
    /// natural top-of-new-page shot is then the honest evidence.
    /// </summary>
    public async Task<bool> ScrollIntoViewAsync(string element, string reference, CancellationToken ct = default)
    {
        const string js = """
            (element) => {
              if (!element || !element.getBoundingClientRect) return false;
              const r = element.getBoundingClientRect();
              const h = window.innerHeight || document.documentElement.clientHeight;
              // "Visible" is not good enough for evidence: a control resting 8px above the fold is
              // technically in view yet reads as a cropped sliver in the screenshot. Demand a margin,
              // and centre the element whenever it doesn't clear that bar.
              const pad = Math.min(80, h * 0.15);
              if (r.height <= h - 2 * pad && r.top >= pad && r.bottom <= h - pad) return true;
              // Centring an element taller than the viewport barely moves the page (its midpoint is
              // already near the middle), leaving the part that matters off-screen — align such an
              // element to the top instead, which is what a human would scroll to.
              element.scrollIntoView({ block: r.height > h * 0.8 ? 'start' : 'center', inline: 'center' });
              return true;
            }
            """;

        var res = await CallAsync("browser_evaluate", new Dictionary<string, object?>
        {
            ["element"] = element,
            ["target"] = reference,
            ["function"] = js,
        }, ct);
        return res.Ok;
    }

    /// <summary>Captures a JPEG screenshot and returns it as base64 for the live view.</summary>
    public async Task<string?> ScreenshotBase64Async(CancellationToken ct = default)
    {
        if (_client is null)
        {
            return null;
        }

        await _gate.WaitAsync(ct);
        try
        {
            var result = await _client.CallToolAsync(
                "browser_take_screenshot",
                new Dictionary<string, object?> { ["type"] = "jpeg" },
                cancellationToken: ct);
            var image = result.Content.OfType<ImageContentBlock>().FirstOrDefault();
            if (image is null)
            {
                return null;
            }

            // ImageContentBlock.Data is ALREADY the base64-encoded UTF-8 payload; hand it to the
            // frontend as-is. (Re-encoding image.Data would double base64-encode → broken <img>.)
            return System.Text.Encoding.UTF8.GetString(image.Data.Span);
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "MCP screenshot failed.");
            return null;
        }
        finally
        {
            _gate.Release();
        }
    }

    private async Task<string> CallTextAsync(string tool, IReadOnlyDictionary<string, object?>? args, CancellationToken ct) =>
        (await CallAsync(tool, args, ct)).Text;

    private async Task<(bool Ok, string Text)> CallAsync(string tool, IReadOnlyDictionary<string, object?>? args, CancellationToken ct)
    {
        if (_client is null)
        {
            return (false, string.Empty);
        }

        await _gate.WaitAsync(ct);
        try
        {
            var result = await _client.CallToolAsync(tool, args, cancellationToken: ct);
            var text = string.Join("\n", result.Content.OfType<TextContentBlock>().Select(t => t.Text));
            return (result.IsError != true, text);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            // A Stop request must surface as cancellation, not as "the tool failed". Swallowing it here
            // left the agent to finish the turn against a dead browser, logging bogus tool failures on
            // the way out; rethrowing unwinds straight to the agent's cancellation handler.
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "MCP tool '{Tool}' failed.", tool);
            return (false, string.Empty);
        }
        finally
        {
            _gate.Release();
        }
    }

    private void UpdateUrlFrom(string text)
    {
        if (string.IsNullOrEmpty(text))
        {
            return;
        }

        // Playwright MCP output includes a "Page URL: <url>" line.
        var match = Regex.Match(text, @"Page URL:\s*(\S+)");
        if (match.Success)
        {
            CurrentUrl = match.Groups[1].Value;
        }

        var title = Regex.Match(text, @"Page Title:\s*(.*)");
        if (title.Success)
        {
            CurrentTitle = title.Groups[1].Value.Trim();
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (_client is not null)
        {
            try
            {
                await _client.DisposeAsync();
            }
            catch
            {
                // best effort — the stdio server is terminated on dispose.
            }

            _client = null;
        }

        _gate.Dispose();
    }
}
