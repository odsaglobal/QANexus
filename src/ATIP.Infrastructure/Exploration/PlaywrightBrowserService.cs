using System.Diagnostics;
using System.Text;
using System.Text.Json;
using ATIP.Application.Common.Interfaces;
using ATIP.Domain.Enums;
using Microsoft.Playwright;

namespace ATIP.Infrastructure.Exploration;

/// <summary>
/// Thin Playwright wrapper used exclusively by <see cref="ExplorerAgent"/>. One instance per
/// exploration session; disposed after the session completes.
/// </summary>
/// <remarks>
/// Split across two files: this one holds exploration and snapshotting; the capabilities file adds
/// the primitives the generic test engine drives (dialogs, frames, tabs, uploads, browser state).
/// </remarks>
public sealed partial class PlaywrightBrowserService : IAsyncDisposable
{

    // Playwright-MCP-style snapshot. Stamps every VISIBLE interactive element with a stable
    // data-atip-ref (e1, e2, …) and returns a compact, ref-annotated list the LLM selects from,
    // e.g.  - button "Sign in" [ref=e5]. Refs live on the DOM until the next snapshot/navigation,
    // so an action taken right after the snapshot resolves deterministically via [data-atip-ref='eN']
    // — exactly how Copilot/Claude drive the browser through Playwright MCP (no brittle text matching).
    private const string AgentSnapshotScript = """
        (max) => {
          // Natively interactive elements — always worth showing to the agent.
          const coreSel = [
            'a[href]', 'button', 'input:not([type="hidden"])', 'select', 'textarea',
            '[role="button"]', '[role="link"]', '[role="checkbox"]', '[role="radio"]',
            '[role="combobox"]', '[role="listbox"]', '[role="menuitem"]', '[role="option"]',
            '[role="tab"]', '[role="switch"]', '[role="slider"]', '[contenteditable="true"]', '[tabindex="0"]'
          ].join(',');
          // SPA controls that are NOT natively interactive: href-less anchors and clickable
          // div/span/li wired up with click handlers (e.g. SauceDemo's cart is
          // `<a class="shopping_cart_link" data-test="shopping-cart-link">` with NO href). Matching
          // only `a[href]` made such controls invisible to the agent, so it could never click them.
          // These are admitted only when they really look clickable (see the cursor check below).
          const looseSel = ['a', '[onclick]', '[data-test]', '[data-testid]'].join(',');
          const sel = coreSel + ',' + looseSel;
          const isVisible = (el) => {
            const r = el.getBoundingClientRect();
            if (r.width < 1 || r.height < 1) return false;
            const s = getComputedStyle(el);
            return s.visibility !== 'hidden' && s.display !== 'none' && s.opacity !== '0';
          };
          const roleOf = (el) => {
            const explicit = el.getAttribute('role');
            if (explicit) return explicit;
            const t = el.tagName;
            if (t === 'A') return 'link';
            if (t === 'BUTTON') return 'button';
            if (t === 'SELECT') return 'combobox';
            if (t === 'TEXTAREA') return 'textbox';
            if (t === 'INPUT') {
              const it = (el.getAttribute('type') || 'text').toLowerCase();
              if (it === 'checkbox') return 'checkbox';
              if (it === 'radio') return 'radio';
              if (it === 'button' || it === 'submit' || it === 'reset') return 'button';
              return 'textbox';
            }
            return 'clickable';
          };
          const testIdOf = (el) => el.getAttribute('data-test') || el.getAttribute('data-testid') || '';
          // An element's OWN text nodes only, so a wrapper never reports its children's text.
          const ownTextOf = (el) => {
            let t = '';
            for (const node of el.childNodes) {
              if (node.nodeType === 3) t += node.nodeValue;
            }
            return t.replace(/\s+/g, ' ').trim();
          };
          const nameOf = (el) => {
            let n = el.getAttribute('aria-label') || '';
            if (!n) n = (el.textContent || '').replace(/\s+/g, ' ').trim();
            if (!n) n = el.getAttribute('placeholder') || el.getAttribute('name') || el.getAttribute('title') || (el.value || '');
            // Icon-only controls (cart, menu, close) carry no accessible text, or carry a meaningless
            // one such as a badge count ("1"). A stable test id is far more useful to the agent and is
            // directly replayable, so prefer it whenever the visible name is empty or too weak to act on.
            const weak = !n || n.length <= 2 || /^\d+$/.test(n);
            if (weak) n = testIdOf(el) || el.getAttribute('id') || n || '';
            return (n || '').replace(/\s+/g, ' ').trim().slice(0, 80);
          };
          document.querySelectorAll('[data-atip-ref]').forEach(e => e.removeAttribute('data-atip-ref'));
          // The visual affordances a framework-rendered control still carries when it has no semantics
          // (no role, no href, no onclick attribute — React attaches handlers by delegation):
          //   - `cursor: pointer`   — the classic hover affordance (sort options, facet rows).
          //   - `user-select: none` — set on TAPPABLE controls so their label isn't selected on tap,
          //     while real content text stays selectable. React-Native-Web buttons (Flipkart's
          //     "Add to cart"/"Buy now") set ONLY this and keep `cursor: auto`, so the pointer test
          //     alone missed them entirely. Require a short, button-sized label for this weaker signal
          //     so ordinary copy inside a non-selectable wrapper isn't mistaken for a control.
          const looksClickableStyle = (el, text) => {
            const s = getComputedStyle(el);
            return s.cursor === 'pointer'
              || (s.userSelect === 'none' && text.length >= 2 && text.length <= 30);
          };
          const lines = [];
          let n = 0;
          const seen = new Set();
          const emitted = new Set();
          const emit = (el, isCore) => {
            if (!isCore) {
              // Only admit a loosely-matched element when it actually behaves like a control. An
              // anchor always counts (browsers only apply cursor:pointer to `a[href]`, so an href-less
              // SPA anchor like SauceDemo's cart would otherwise be filtered out here); anything else
              // must declare a click handler or carry a control's visual affordance.
              const looksClickable = el.tagName === 'A'
                || el.hasAttribute('onclick')
                || looksClickableStyle(el, ownTextOf(el));
              if (!looksClickable) return false;
              // Skip layout wrappers that merely CONTAIN a real control — click the control itself.
              if (el.querySelector(coreSel)) return false;
              // querySelectorAll yields document order, so an ancestor is emitted before its children;
              // skip descendants of an already-emitted control to avoid duplicate targets.
              let p = el.parentElement, nested = false;
              while (p) { if (emitted.has(p)) { nested = true; break; } p = p.parentElement; }
              if (nested) return false;
            }

            const role = roleOf(el);
            // For a semantic control, nameOf's textContent fallback is the accessible name. For a
            // framework-rendered <div> it is NOT: textContent concatenates every descendant, so a
            // wrapper came back as "Location not setSelect delivery location" — a label that exists
            // nowhere on screen and that the replay engine can never locate again. Such an element is
            // only admitted because of its OWN text, so use exactly that as its name.
            const name = isCore ? nameOf(el) : (ownTextOf(el) || nameOf(el));
            if (!name) return false;
            const key = role + '|' + name + '|' + Math.round(el.getBoundingClientRect().y);
            if (seen.has(key)) return false;
            seen.add(key);
            emitted.add(el);
            n++;
            const ref = 'e' + n;
            el.setAttribute('data-atip-ref', ref);
            const ph = el.getAttribute('placeholder');
            const type = el.tagName === 'INPUT' ? (el.getAttribute('type') || 'text') : '';
            let line = '- ' + role + ' "' + name + '" [ref=' + ref + ']';
            if (ph) line += ' (placeholder: "' + ph + '")';
            if (type) line += ' (type: ' + type + ')';
            // The CURRENT VALUE of a field is page state, not markup: without it neither the agent
            // nor outcome verification can tell whether text was actually entered. Password values
            // are reported as a masked length so a secret never reaches the model or the logs.
            if (el.tagName === 'INPUT' || el.tagName === 'TEXTAREA') {
              const val = el.value || '';
              if (val) {
                line += type === 'password'
                  ? ' (value: ' + '*'.repeat(Math.min(val.length, 20)) + ')'
                  : ' (value: "' + val.slice(0, 80) + '")';
              }
            }
            // A stable test id is the best descriptor to record: it survives copy changes and the
            // replay engine can resolve it directly via [data-test=...].
            const testId = testIdOf(el);
            if (testId && testId !== name) line += ' (testid: "' + testId + '")';
            // A badge/counter is short text INSIDE a control (cart count, unread count). nameOf drops
            // it as a weak name, but it is often the very thing an expected result asserts on, so
            // report it separately rather than losing it.
            const inner = (el.textContent || '').replace(/\s+/g, ' ').trim();
            if (inner && inner.length <= 20 && inner !== name && !name.includes(inner)) {
              line += ' (text: "' + inner + '")';
            }
            // Without its options a native dropdown is unusable to the agent.
            if (el.tagName === 'SELECT' && el.options) {
              const opts = Array.from(el.options).map(o => (o.text || '').trim()).filter(Boolean).slice(0, 12);
              if (opts.length) line += ' (options: ' + opts.map(o => '"' + o + '"').join(', ') + ')';
            }
            lines.push(line);
            return true;
          };

          for (const el of document.querySelectorAll(sel)) {
            if (n >= max) break;
            if (!isVisible(el)) continue;
            emit(el, el.matches(coreSel));
          }

          // ── PLAIN CLICKABLE ELEMENTS ───────────────────────────────────────────────────────
          // React / React-Native-Web sites (Flipkart, and most design-system apps) render controls as
          // bare `<div>`s: no href, no role, no onclick attribute (handlers are attached by the
          // framework) and no test id. Nothing above matches them, so sort options, filter rows,
          // in-page tabs and even "Add to cart" were completely invisible to the agent — it reported
          // "no actionable element in the snapshot" and burned its whole turn budget. Scan for the
          // affordances they do carry (see looksClickableStyle) in a SECOND pass with its own budget:
          // doing it in the first pass would let header chrome consume `max` before the real controls.
          if (n < max * 2) {
            let scanned = 0;
            for (const el of document.querySelectorAll('div,span,li,td,label,p')) {
              if (n >= max * 2) break;
              // Bound the work on very large pages; getComputedStyle is the expensive part, so the
              // cheap text and visibility tests run first.
              if (++scanned > 6000) break;
              const t = ownTextOf(el);
              if (t.length < 2 || t.length > 60) continue;
              if (!isVisible(el)) continue;
              if (!looksClickableStyle(el, t)) continue;
              emit(el, false);
            }
          }

          // ── VISIBLE TEXT ───────────────────────────────────────────────────────────────────
          // Controls alone are not enough: most expected results are assertions about TEXT
          // ("the Checkout page is displayed", "Error: First Name is required", "Thank you for your
          // order!"). Headings, banners and totals are not interactive, so without this section the
          // agent is blind to the very outcome it is trying to confirm and burns its whole turn
          // budget re-clicking. Only an element's OWN text nodes are read, so parents don't repeat
          // their children's text, and anything inside a control already listed above is skipped.
          const ownText = ownTextOf;
          const maxTextLines = 140;
          const texts = [];
          // Counts, not a plain set: two products can legitimately cost the same, and dropping the
          // repeat would silently remove a row from an ordered list. Repeats are capped so that
          // boilerplate still cannot flood the snapshot.
          const textCounts = new Map();
          const collectText = (nodes) => {
            for (const el of nodes) {
              if (texts.length >= maxTextLines) return;
              const t = ownText(el);
              if (t.length < 2 || t.length > 200) continue;
              if (!/[a-z0-9]/i.test(t)) continue;
              // Text belonging to a button already appears as that button's name above.
              if (el.closest('button,select,textarea,[role="button"]')) continue;
              if ((textCounts.get(t) || 0) >= 2) continue;
              if (!isVisible(el)) continue;
              textCounts.set(t, (textCounts.get(t) || 0) + 1);
              texts.push(t);
            }
          };
          // Outcome-bearing text first — headings, alerts and error banners carry the result of the
          // last action, so they must survive the line cap even on a long page.
          collectText(document.querySelectorAll(
            'h1,h2,h3,h4,h5,h6,[role="alert"],[role="status"],[aria-live],[data-test*="error"],[data-testid*="error"],[class*="error"],[class*="title"],[class*="message"]'));
          // Then everything else in DOCUMENT ORDER. Order matters: assertions such as "products are
          // sorted by price ascending" or "the cheapest item is first" can only be judged if the
          // text is reported in the order it appears on the page. Link text is included for the same
          // reason — item names live inside links, and a price list without its names proves nothing.
          collectText(document.querySelectorAll('p,span,div,li,td,th,label,legend,strong,dt,dd,figcaption,a'));

          if (texts.length) {
            lines.push('');
            lines.push('Visible text on the page:');
            for (const t of texts) lines.push('  ' + t);
          }
          return lines.join('\n');
        }
        """;

    private IPlaywright? _playwright;
    private IBrowser? _browser;
    private IPage? _page;
    private ICDPSession? _cdp;
    private ICDPSessionEvent? _screencastFrameEvent;
    private Func<string, Task>? _onFrame;

    public string CurrentUrl => _page?.Url ?? string.Empty;
    public string CurrentTitle { get; private set; } = string.Empty;

    /// <summary>
    /// Every open tab across the browser's contexts, with the one currently being driven marked
    /// active. Titles are read live, so this is a snapshot rather than a cached list.
    /// </summary>
    public async Task<IReadOnlyList<BrowserTabInfo>> ListTabsAsync()
    {
        var tabs = new List<BrowserTabInfo>();
        if (_browser is null)
        {
            return tabs;
        }

        var pages = _browser.Contexts.SelectMany(c => c.Pages).Where(p => !p.IsClosed).ToList();
        for (var i = 0; i < pages.Count; i++)
        {
            var page = pages[i];
            string title;
            try
            {
                // A tab mid-navigation can throw here; its URL is still worth showing.
                title = await page.TitleAsync();
            }
            catch
            {
                title = string.Empty;
            }

            tabs.Add(new BrowserTabInfo(i, title, page.Url, ReferenceEquals(page, _page)));
        }

        return tabs;
    }

    public async Task InitializeAsync(string browserType = "chromium", bool headless = true)
    {
        var normalizedType = browserType.ToLowerInvariant();

        try
        {
            _playwright = await Microsoft.Playwright.Playwright.CreateAsync();
        }
        catch (Exception ex) when (HasMissingBrowserException(ex))
        {
            var installSucceeded = await TryInstallBrowserAsync(normalizedType);
            if (!installSucceeded)
            {
                throw new InvalidOperationException($"Playwright browser '{normalizedType}' could not be installed automatically.", ex);
            }

            _playwright = await Microsoft.Playwright.Playwright.CreateAsync();
        }

        _browser = normalizedType switch
        {
            "firefox" => await _playwright.Firefox.LaunchAsync(new BrowserTypeLaunchOptions { Headless = headless }),
            "webkit" => await _playwright.Webkit.LaunchAsync(new BrowserTypeLaunchOptions { Headless = headless }),
            _ => await _playwright.Chromium.LaunchAsync(new BrowserTypeLaunchOptions { Headless = headless })
        };
        _page = await _browser.NewPageAsync(new BrowserNewPageOptions
        {
            ViewportSize = new ViewportSize { Width = 1280, Height = 720 },
        });

        // Suppress most console noise; keep errors surfaced through exceptions.
        _page.PageError += (_, err) => { /* ignore page errors during exploration */ };

        // Native JS dialogs (alert/confirm/prompt/beforeunload) block the page until answered, so
        // one must always be handled. The policy decides how; the default dismisses, which is what
        // exploration wants, while a step can opt into accepting or into typing a prompt answer.
        _page.Dialog += HandleDialogAsync;

        // Real catalogues open detail pages in a NEW TAB — every Flipkart product card is
        // target="_blank". Playwright keeps driving the ORIGINAL tab, so a card click looked like it
        // did nothing: the agent clicked, then reported "the product details page is not displayed;
        // the user is still on the search results page". Adopt any newly opened tab as the active page
        // so snapshots, assertions and later actions all target what a real user would be looking at.
        _page.Context.Page += (_, opened) => AdoptPage(opened);

        // Pre-grant geolocation/notification permissions so the browser never shows a permission prompt.
        try
        {
            await _page.Context.GrantPermissionsAsync(new[] { "geolocation", "notifications" });
        }
        catch { /* permission grant is best-effort */ }
    }

    /// <summary>
    /// Makes a newly opened tab the active page, wiring it with the same noise-suppression and dialog
    /// handling as the original. When it closes, falls back to the last surviving tab so the session
    /// never ends up pointing at a dead page.
    /// </summary>
    private void AdoptPage(IPage opened)
    {
        try
        {
            opened.PageError += (_, _) => { /* ignore page errors during exploration */ };
            opened.Dialog += HandleDialogAsync;
            opened.Close += (_, _) =>
            {
                if (ReferenceEquals(_page, opened))
                {
                    _page = _browser?.Contexts
                        .SelectMany(c => c.Pages)
                        .LastOrDefault(p => !p.IsClosed);

                    if (_page is not null && _onFrame is not null)
                    {
                        _ = FollowScreencastAsync(_page);
                    }
                }
            };

            _page = opened;
            // The new tab has its own frame tree, so any frame we had switched into is gone.
            _frameSelectors.Clear();

            if (_onFrame is not null)
            {
                _ = FollowScreencastAsync(opened);
            }
        }
        catch { /* adopting a popup is best-effort — never break the run over it */ }
    }

    /// <summary>Moves the live screencast to <paramref name="page"/>, swallowing any failure.</summary>
    private async Task FollowScreencastAsync(IPage page)
    {
        try
        {
            await AttachScreencastAsync(page);
        }
        catch
        {
            // Losing the live view must never abort the run.
        }
    }

    private static bool HasMissingBrowserException(Exception ex)
    {
        var message = ex.Message ?? string.Empty;
        return message.Contains("Executable doesn't exist", StringComparison.OrdinalIgnoreCase)
            || message.Contains("browser executable doesn't exist", StringComparison.OrdinalIgnoreCase)
            || message.Contains("browser not found", StringComparison.OrdinalIgnoreCase)
            || message.Contains("Failed to launch browser", StringComparison.OrdinalIgnoreCase)
            || message.Contains("playwright install chromium", StringComparison.OrdinalIgnoreCase);
    }

    private static async Task<bool> TryInstallBrowserAsync(string browserType)
    {
        var pwsh = ResolvePowerShellExecutable();
        if (pwsh is null)
        {
            return false;
        }

        var scriptPath = Path.Combine(AppContext.BaseDirectory, "playwright.ps1");
        if (!File.Exists(scriptPath))
        {
            return false;
        }

        var startInfo = new ProcessStartInfo
        {
            FileName = pwsh,
            Arguments = $"-NoLogo -NoProfile -File \"{scriptPath}\" install {browserType}",
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
        };

        using var process = Process.Start(startInfo);
        if (process is null)
        {
            return false;
        }

        await process.WaitForExitAsync();
        var stdout = await process.StandardOutput.ReadToEndAsync();
        var stderr = await process.StandardError.ReadToEndAsync();

        return process.ExitCode == 0 || (!string.IsNullOrWhiteSpace(stdout) && stdout.Contains("browser", StringComparison.OrdinalIgnoreCase) && stdout.Contains("installed", StringComparison.OrdinalIgnoreCase));
    }

    private static string? ResolvePowerShellExecutable()
    {
        string[] candidatePaths =
        [
            "pwsh",
            "/opt/homebrew/bin/pwsh",
            "/usr/local/bin/pwsh",
            "/opt/homebrew/bin/powershell",
            "/usr/local/bin/powershell",
            "/usr/local/microsoft/powershell/7/pwsh",
            "/usr/local/microsoft/powershell/7/powershell",
        ];

        foreach (var candidate in candidatePaths)
        {
            if (File.Exists(candidate))
            {
                return candidate;
            }
        }

        var path = Environment.GetEnvironmentVariable("PATH") ?? string.Empty;
        var segments = path.Split(':', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        foreach (var segment in segments)
        {
            foreach (var candidate in candidatePaths)
            {
                var normalized = candidate.StartsWith('/') ? candidate : Path.Combine(segment, candidate);
                if (File.Exists(normalized))
                {
                    return normalized;
                }
            }
        }

        return null;
    }

    public async Task<(string Title, string Url, int StatusCode)> NavigateAsync(string url, CancellationToken ct = default)
    {
        IResponse? response = null;
        try
        {
            response = await _page!.GotoAsync(url, new PageGotoOptions
            {
                WaitUntil = WaitUntilState.DOMContentLoaded,
                Timeout = 30_000,
            });

            // Allow JS rendering to settle.
            await _page.WaitForTimeoutAsync(1_000);
        }
        catch (TimeoutException)
        {
            // Partial loads are acceptable; we capture what's there.
        }

        CurrentTitle = await _page!.TitleAsync();
        return (CurrentTitle, _page.Url, response?.Status ?? 200);
    }

    // Builds a DEPTH-BOUNDED role/name/text outline of the page entirely in-page (JS).
    // See GetAccessibilityTreeAsync for why page.Accessibility.SnapshotAsync() is avoided.
    private const string AccessibilityOutlineScript = """
        ({ maxDepth, maxNodes }) => {
          const roleMap = { A:'link', BUTTON:'button', SELECT:'combobox', TEXTAREA:'textbox',
            NAV:'navigation', MAIN:'main', HEADER:'banner', FOOTER:'contentinfo',
            H1:'heading', H2:'heading', H3:'heading', H4:'heading', H5:'heading', H6:'heading',
            UL:'list', OL:'list', LI:'listitem', TABLE:'table', FORM:'form', IMG:'img',
            LABEL:'label', DIALOG:'dialog', SECTION:'region', ARTICLE:'article' };
          const isVisible = (el) => {
            const s = window.getComputedStyle(el);
            if (s.display === 'none' || s.visibility === 'hidden' || parseFloat(s.opacity) === 0) return false;
            const r = el.getBoundingClientRect();
            return r.width > 0 && r.height > 0;
          };
          const nameOf = (el) => {
            let n = el.getAttribute('aria-label') || el.getAttribute('placeholder') || el.getAttribute('title') || '';
            if (!n && el.tagName === 'INPUT') n = el.getAttribute('name') || el.getAttribute('type') || '';
            if (!n) {
              n = Array.from(el.childNodes).filter(c => c.nodeType === 3).map(c => c.textContent).join(' ');
            }
            return (n || '').replace(/\s+/g, ' ').trim().slice(0, 80);
          };
          const roleOf = (el) => {
            const explicit = el.getAttribute('role');
            if (explicit) return explicit;
            if (el.tagName === 'INPUT') {
              const t = (el.getAttribute('type') || 'text').toLowerCase();
              if (t === 'checkbox') return 'checkbox';
              if (t === 'radio') return 'radio';
              if (t === 'button' || t === 'submit' || t === 'reset') return 'button';
              if (t === 'hidden') return '';
              return 'textbox';
            }
            return roleMap[el.tagName] || '';
          };
          const lines = [];
          let count = 0;
          const walk = (el, depth) => {
            if (count >= maxNodes || depth > maxDepth || el.nodeType !== 1) return;
            if (!isVisible(el)) return;
            const role = roleOf(el);
            let nextDepth = depth;
            if (role) {
              const name = nameOf(el);
              const id = el.id ? ('#' + el.id) : '';
              const testid = el.getAttribute('data-testid');
              lines.push('  '.repeat(Math.min(depth, maxDepth)) + role +
                (name ? (' "' + name + '"') : '') + id +
                (testid ? (' [data-testid=' + testid + ']') : ''));
              count++;
              nextDepth = depth + 1;
            }
            for (const child of el.children) walk(child, nextDepth);
          };
          walk(document.body, 0);
          return lines.join('\n');
        }
        """;

    public async Task<string> GetAccessibilityTreeAsync(CancellationToken ct = default)
    {
        // IMPORTANT: We intentionally avoid page.Accessibility.SnapshotAsync().
        // Playwright's .NET driver deserializes the snapshot response (JSON under "$.result")
        // with System.Text.Json's DEFAULT depth limit of 64. On real-world apps the accessibility
        // tree nests deeper than 64 levels, so Playwright itself throws:
        //   "The maximum configured depth of 64 has been exceeded. ... Path: $.result"
        // which fails the whole exploration and can break the browser connection. There is no
        // public API to raise that limit. Instead we build a depth-bounded role/name/text outline
        // INSIDE the page via JS and return it as a plain string (Playwright deserializes a shallow
        // value only). The outline still gives the LLM the same role/name/text context.
        try
        {
            var outline = await _page!.EvaluateAsync<string>(
                AccessibilityOutlineScript,
                new { maxDepth = 25, maxNodes = 600 });
            return string.IsNullOrWhiteSpace(outline) ? "{}" : outline;
        }
        catch
        {
            return "{}";
        }
    }

    /// <summary>
    /// The page's full visible text, for VERIFYING an expected result — deliberately separate from
    /// <see cref="SnapshotForAgentAsync"/>.
    /// <para>
    /// The agent snapshot is budgeted (a capped element list plus ~140 text lines) so the LLM prompt
    /// stays small and cheap. That budget is right for DECIDING what to do next, but wrong for JUDGING
    /// whether something is on screen: on a long page the cap is spent long before the bottom is
    /// reached — on a Flipkart product page it ran out inside the specification table, so "Add to cart"
    /// never made it into the evidence and a perfectly correct step was reported as failed. The
    /// accessibility outline does not rescue it either, because framework-rendered controls
    /// (React-Native-Web and similar) are plain role-less &lt;div&gt;s. Verification therefore reads the
    /// whole rendered text, which <c>innerText</c> already limits to what is actually visible.
    /// </para>
    /// </summary>
    public async Task<string> GetVisibleTextAsync(int maxChars = 40_000, CancellationToken ct = default)
    {
        try
        {
            var text = await _page!.EvaluateAsync<string>(
                @"() => {
                    const t = document.body ? document.body.innerText : '';
                    return t.split('\n').map(l => l.trim()).filter(Boolean).join('\n');
                }");

            if (string.IsNullOrWhiteSpace(text))
            {
                return string.Empty;
            }

            return text.Length > maxChars ? text[..maxChars] : text;
        }
        catch
        {
            return string.Empty;
        }
    }

    /// <summary>
    /// Reads the concrete value(s) behind a CSS selector so a step's assertion can be checked by
    /// comparison rather than by asking a language model what the page "looks like".
    /// <para>
    /// Only rendered elements are returned: a hidden template row or an off-screen duplicate carries
    /// no meaning for a user, but it would silently corrupt an ordering or count assertion. Results
    /// come back in document order, which is exactly the order the ordering operators compare.
    /// </para>
    /// </summary>
    /// <param name="source">
    /// <c>value</c> reads form state (for a &lt;select&gt;, the selected option's visible label);
    /// anything else reads visible text.
    /// </param>
    /// <returns>
    /// The matched values, or <c>null</c> if the selector itself is invalid — a broken assertion is a
    /// different problem from one that legitimately matched nothing, and the report says so.
    /// </returns>
    public async Task<IReadOnlyList<string>?> ReadValuesAsync(
        string selector,
        string source = "text",
        int max = 200,
        CancellationToken ct = default)
    {
        if (_page is null || string.IsNullOrWhiteSpace(selector))
        {
            return null;
        }

        try
        {
            var values = await _page.EvaluateAsync<string[]?>(
                @"([selector, source, max]) => {
                    let nodes;
                    try { nodes = document.querySelectorAll(selector); }
                    catch { return null; }

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
                                out.push(String(el.value ?? '').trim());
                            } else {
                                out.push((el.textContent || '').replace(/\s+/g, ' ').trim());
                            }
                        } else {
                            out.push((el.textContent || '').replace(/\s+/g, ' ').trim());
                        }
                    }
                    return out;
                }",
                new object[] { selector, source, max });

            return values;
        }
        catch
        {
            return null;
        }
    }

    /// <summary>
    /// Builds a digest of the page's selectable, value-bearing elements: for each stable CSS selector,
    /// how many rendered elements it matches and a few of their actual values.
    /// <para>
    /// This exists so an expectation can be compiled into a real assertion. Raw HTML is the wrong input
    /// for that — it is mostly layout wrappers and it hides how many times a selector actually matches,
    /// which is precisely what an ordering or count assertion depends on. Reporting "(24 matches)"
    /// alongside three sample prices lets a selector be chosen on evidence rather than on guesswork.
    /// </para>
    /// </summary>
    public async Task<string> GetAssertionContextAsync(int maxGroups = 120, CancellationToken ct = default)
    {
        if (_page is null)
        {
            return string.Empty;
        }

        try
        {
            var lines = await _page.EvaluateAsync<string[]>(
                @"(maxGroups) => {
                    const rendered = (el) => {
                        const style = window.getComputedStyle(el);
                        if (style.visibility === 'hidden' || style.display === 'none') return false;
                        return el.getClientRects().length > 0;
                    };

                    // Prefer attributes a developer chose over ones a bundler generated: hashed class
                    // names change on every deploy and would produce an assertion that rots immediately.
                    const unstable = (s) => !s || s.length < 2 || /\d{3}/.test(s) || /^(css|sc|jsx)-/.test(s);

                    const selectorFor = (el) => {
                        for (const attr of ['data-test', 'data-testid', 'data-qa']) {
                            const v = el.getAttribute && el.getAttribute(attr);
                            if (v) return '[' + attr + '=""' + v + '""]';
                        }
                        if (el.id && !unstable(el.id)) return '#' + el.id;
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
                    // Repeated groups first: those are the lists that ordering and count checks target.
                    const sorted = [...groups.entries()].sort((a, b) => b[1].length - a[1].length);
                    for (const [key, values] of sorted.slice(0, maxGroups)) {
                        const parts = key.split('|');
                        out.push(parts[0] + ' ' + parts[1] + ' (' + values.length + ' match' + (values.length === 1 ? '' : 'es') + ') -> ' + values.slice(0, 3).join(' | '));
                    }
                    return out;
                }",
                maxGroups);

            return string.Join("\n", lines);
        }
        catch
        {
            return string.Empty;
        }
    }

    /// <summary>
    /// Waits for the page to stop changing before its state is read.
    /// <para>
    /// Waiting on load state alone is NOT enough: a click that submits a form returns before the
    /// browser has begun navigating, so the old document is still "loaded" and the page would be
    /// read — and the step judged — against the page it is navigating away from. So this polls until
    /// the URL and ready state have been stable across several consecutive samples, which lets a
    /// navigation that starts a moment after the click be observed. Every wait is best-effort and
    /// bounded: a page that never goes quiet must not stall the run.
    /// </para>
    /// </summary>
    public async Task WaitForPageSettledAsync(int timeoutMs = 5000, CancellationToken ct = default)
    {
        if (_page is null)
        {
            return;
        }

        const int pollMs = 150;
        const int requiredStableSamples = 3;

        var deadline = DateTime.UtcNow.AddMilliseconds(timeoutMs);
        string? lastUrl = null;
        var stable = 0;

        while (stable < requiredStableSamples && DateTime.UtcNow < deadline)
        {
            await Task.Delay(pollMs, ct);

            string url;
            string readyState;
            try
            {
                url = _page.Url;
                readyState = await _page.EvaluateAsync<string>("() => document.readyState");
            }
            catch
            {
                // The document is being replaced mid-navigation: not settled, keep waiting.
                stable = 0;
                lastUrl = null;
                continue;
            }

            if (readyState == "complete" && url == lastUrl)
            {
                stable++;
            }
            else
            {
                stable = 0;
                lastUrl = url;
            }
        }

        try
        {
            await _page.WaitForLoadStateAsync(
                LoadState.NetworkIdle,
                new PageWaitForLoadStateOptions { Timeout = 2000 });
        }
        catch
        {
            // Best-effort: read whatever has rendered so far.
        }
    }

    public async Task<string> GetPageHtmlAsync(CancellationToken ct = default)
    {
        try
        {
            try
            {
                await _page!.WaitForLoadStateAsync(
                    LoadState.DOMContentLoaded,
                    new PageWaitForLoadStateOptions { Timeout = 3000 });
            }
            catch (TimeoutException)
            {
                // Continue with whatever content is currently available.
            }

            return await _page!.ContentAsync();
        }
        catch (PlaywrightException)
        {
            // The page may be mid-navigation ("content is changing"); returning empty keeps step
            // execution and auto-healing resilient rather than aborting the whole run.
            return string.Empty;
        }
    }

    public async Task<byte[]> TakeScreenshotAsync(bool fullPage = true, CancellationToken ct = default)
    {
        return await _page!.ScreenshotAsync(new PageScreenshotOptions
        {
            FullPage = fullPage,
            Type = ScreenshotType.Png,
        });
    }

    /// <summary>
    /// Starts a live CDP screencast. Each frame invokes <paramref name="onFrame"/> with a
    /// base64-encoded JPEG. Frames are acknowledged immediately so the stream keeps flowing.
    /// Safe to call once per session; a no-op if the page is not initialized.
    /// </summary>
    public async Task StartScreencastAsync(Func<string, Task> onFrame, CancellationToken ct = default)
    {
        if (_page is null || _cdp is not null)
        {
            return;
        }

        _onFrame = onFrame;
        await AttachScreencastAsync(_page);
    }

    /// <summary>
    /// Points the CDP screencast at <paramref name="page"/>, tearing down any previous attachment.
    /// A CDP session is bound to one page for life, so following a popup means rebuilding it —
    /// otherwise the live view keeps showing the tab the run has already navigated away from.
    /// </summary>
    private async Task AttachScreencastAsync(IPage page)
    {
        if (_screencastFrameEvent is not null)
        {
            _screencastFrameEvent.OnEvent -= HandleScreencastFrame;
            _screencastFrameEvent = null;
        }

        if (_cdp is not null)
        {
            try { await _cdp.SendAsync("Page.stopScreencast"); } catch { /* the old page may be gone */ }
            try { await _cdp.DetachAsync(); } catch { /* best-effort */ }
            _cdp = null;
        }

        _cdp = await page.Context.NewCDPSessionAsync(page);
        _screencastFrameEvent = _cdp.Event("Page.screencastFrame");
        _screencastFrameEvent.OnEvent += HandleScreencastFrame;

        await _cdp.SendAsync("Page.startScreencast", new Dictionary<string, object>
        {
            ["format"] = "jpeg",
            ["quality"] = 50,
            ["maxWidth"] = 1024,
            ["maxHeight"] = 640,
            ["everyNthFrame"] = 2,
        });
    }

    private async void HandleScreencastFrame(object? sender, JsonElement? evt)
    {
        if (evt is not { } e)
        {
            return;
        }

        try
        {
            // Ack first so Chromium continues emitting frames.
            if (_cdp is not null && e.TryGetProperty("sessionId", out var sessionIdProp))
            {
                await _cdp.SendAsync("Page.screencastFrameAck", new Dictionary<string, object>
                {
                    ["sessionId"] = sessionIdProp.GetInt32(),
                });
            }

            if (_onFrame is not null && e.TryGetProperty("data", out var dataProp))
            {
                var data = dataProp.GetString();
                if (!string.IsNullOrEmpty(data))
                {
                    await _onFrame(data);
                }
            }
        }
        catch
        {
            // Ignore individual frame errors — the crawl should never fail because of streaming.
        }
    }

    /// <summary>Stops the screencast and detaches the CDP session. Idempotent.</summary>
    public async Task StopScreencastAsync()
    {
        if (_cdp is null)
        {
            return;
        }

        try
        {
            if (_screencastFrameEvent is not null)
            {
                _screencastFrameEvent.OnEvent -= HandleScreencastFrame;
            }

            await _cdp.SendAsync("Page.stopScreencast");
            await _cdp.DetachAsync();
        }
        catch
        {
            // Best-effort teardown.
        }
        finally
        {
            _screencastFrameEvent = null;
            _cdp = null;
            _onFrame = null;
        }
    }

    public async Task<IReadOnlyList<string>> GetSameOriginLinksAsync(string baseUrl, CancellationToken ct = default)
    {
        var uri = new Uri(baseUrl);
        var origin = $"{uri.Scheme}://{uri.Host}:{(uri.Port == 80 || uri.Port == 443 ? uri.Port : uri.Port)}";

        try
        {
            var links = await _page!.EvaluateAsync<string[]>(@"(origin) => {
                return Array.from(document.querySelectorAll('a[href]'))
                    .map(a => { try { return new URL(a.href, location.href); } catch { return null; } })
                    .filter(u => u && u.origin === origin && !u.hash && !u.pathname.match(/\.(pdf|jpg|jpeg|png|gif|svg|zip|xml|ico|woff|woff2|ttf|css|js)$/i))
                    .map(u => u.href.split('#')[0].split('?')[0])
                    .filter((v, i, a) => v && a.indexOf(v) === i)
                    .slice(0, 60);
            }", origin);
            return links ?? [];
        }
        catch
        {
            return [];
        }
    }

    // JS that finds and closes blocking modals/popups/overlays/consent-banners via their dismiss controls.
    private const string OverlayDismissScript = """
        (() => {
          const isVisible = (el) => {
            if (!el || !el.getBoundingClientRect) return false;
            const r = el.getBoundingClientRect();
            const s = getComputedStyle(el);
            return r.width > 0 && r.height > 0 && s.visibility !== 'hidden' && s.display !== 'none' && s.opacity !== '0';
          };
          const norm = (t) => (t || '').trim().toLowerCase();
          const closeWords = ['close','not now','no thanks','maybe later','skip','dismiss','cancel','later','x'];
          const consentWords = ['accept all','accept','i accept','agree','i agree','got it','allow all','allow','continue','ok','okay'];
          const isCloseControl = (el) => {
            const label = norm(el.getAttribute && el.getAttribute('aria-label'));
            const title = norm(el.getAttribute && el.getAttribute('title'));
            const cls = norm(el.className && el.className.toString ? el.className.toString() : '');
            const txt = norm(el.textContent);
            if (txt === '✕' || txt === '×' || txt === '✖' || txt === 'x') return true;
            if (label.includes('close') || title.includes('close') || cls.includes('close')) return true;
            if (closeWords.includes(txt)) return true;
            return false;
          };
          const isConsentControl = (el) => {
            const txt = norm(el.textContent);
            if (!txt || txt.length > 24) return false;
            return consentWords.some(w => txt === w || txt.startsWith(w));
          };
          const clickables = 'button,[role="button"],a,input[type="button"],input[type="submit"],span,svg,i,div';
          const modalSel = '[role="dialog"],[role="alertdialog"],[aria-modal="true"],[class*="modal" i],[class*="popup" i],[class*="drawer" i],[class*="overlay" i],[class*="Modal"],[class*="Popup"]';
          const consentSel = '[id*="cookie" i],[class*="cookie" i],[id*="consent" i],[class*="consent" i],[id*="gdpr" i],[class*="gdpr" i],[class*="banner" i],[aria-label*="cookie" i],[aria-label*="consent" i]';
          let dismissed = 0;

          // 1) Cookie / consent banners: prefer the accept/agree control, else a close control.
          for (const c of Array.from(document.querySelectorAll(consentSel)).filter(isVisible)) {
            const controls = Array.from(c.querySelectorAll(clickables)).filter(isVisible);
            const target = controls.find(isConsentControl) || controls.find(isCloseControl);
            if (target) { try { target.click(); dismissed++; } catch(e){} }
          }

          // 2) Generic modals/popups/overlays: click their close control.
          for (const m of Array.from(document.querySelectorAll(modalSel)).filter(isVisible)) {
            const controls = Array.from(m.querySelectorAll(clickables)).filter(isVisible);
            const target = controls.find(isCloseControl);
            if (target) { try { target.click(); dismissed++; } catch(e){} }
          }

          // 3) Fallback: a lone floating close control anywhere on the page.
          if (dismissed === 0) {
            const globals = Array.from(document.querySelectorAll(clickables)).filter(isVisible);
            const target = globals.find(isCloseControl);
            if (target) { try { target.click(); dismissed++; } catch(e){} }
          }

          return dismissed;
        })()
        """;

    /// <summary>
    /// Best-effort dismissal of unexpected popups/modals/overlays that block interaction. Tries the
    /// Escape key first, then clicks a detected close control. Returns how many overlays were dismissed.
    /// </summary>
    public async Task<int> TryDismissOverlaysAsync(CancellationToken ct = default)
    {
        if (_page is null)
        {
            return 0;
        }

        var total = 0;
        try
        {
            // Multiple passes clear stacked obstacles (e.g. a consent banner revealed after a modal closes).
            for (var pass = 0; pass < 3; pass++)
            {
                try
                {
                    await _page.Keyboard.PressAsync("Escape");
                    await _page.WaitForTimeoutAsync(150);
                }
                catch { /* Escape not always supported; continue */ }

                var dismissed = await _page.EvaluateAsync<int>(OverlayDismissScript);
                if (dismissed == 0)
                {
                    break;
                }

                total += dismissed;
                await _page.WaitForTimeoutAsync(400);
            }

            return total;
        }
        catch
        {
            return total;
        }
    }

    /// <summary>
    /// Playwright-MCP-style page snapshot for the LLM: every visible interactive element is
    /// stamped with a stable ref (e1, e2, …) and returned as a compact list, e.g.
    ///   - button "Sign in" [ref=e5]
    /// The agent selects an element by ref, and the ref is turned into a durable locator by
    /// <see cref="DescribeRefAsync"/> before the engine acts on it — so the element the AI chose is
    /// the element that gets recorded, without any brittle text matching in between.
    /// </summary>
    public async Task<string> SnapshotForAgentAsync(int max = 60, CancellationToken ct = default)
    {
        if (_page is null)
        {
            return "(no page)";
        }

        try
        {
            var snapshot = await _page.EvaluateAsync<string>(AgentSnapshotScript, max);
            return string.IsNullOrWhiteSpace(snapshot) ? "(no interactive elements detected)" : snapshot;
        }
        catch
        {
            return "(snapshot unavailable)";
        }
    }

    private ILocator? ResolveRefLocator(string? reference)
    {
        if (_page is null)
        {
            return null;
        }

        var refId = NormalizeRef(reference);
        return refId is null ? null : _page.Locator($"[data-atip-ref='{refId}']").First;
    }

    /// <summary>Best-effort accessible name for a ref'd element (aria-label, text, placeholder, name)
    /// so a successful action can be recorded with a stable, human-meaningful descriptor.</summary>
    public async Task<string?> GetRefAccessibleNameAsync(string reference, CancellationToken ct = default)
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

            var name = await locator.EvaluateAsync<string?>(
                "el => (el.getAttribute('aria-label') || el.getAttribute('placeholder') || el.getAttribute('name') || (el.innerText || el.value || '').trim()).slice(0, 120)");
            return string.IsNullOrWhiteSpace(name) ? null : name.Trim();
        }
        catch
        {
            return null;
        }
    }

    /// <summary>Extracts a bare ref token (e.g. "e12") from any of: "e12", "ref=e12", "[ref=e12]".</summary>
    private static string? NormalizeRef(string? reference)
    {
        if (string.IsNullOrWhiteSpace(reference))
        {
            return null;
        }

        var match = System.Text.RegularExpressions.Regex.Match(reference, "e\\d+");
        return match.Success ? match.Value : null;
    }

    /// <summary>Presses a keyboard key (e.g. "Enter", "Tab", "Escape"). Best-effort.</summary>
    public async Task<bool> PressKeyAsync(string key, CancellationToken ct = default)
    {
        if (_page is null || string.IsNullOrWhiteSpace(key))
        {
            return false;
        }

        try
        {
            await _page.Keyboard.PressAsync(key.Trim());
            await SettleActivePageAsync(400);
            return true;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// Waits out the usual post-action settle, then — if that action spawned a popup that
    /// <see cref="AdoptPage"/> just made active — waits for the new tab to actually be usable.
    /// The popup event fires the instant the tab is created, when it is still blank, so without this
    /// the next recorded action would run against an empty page and fail to find its element.
    /// </summary>
    private async Task SettleActivePageAsync(int quietMs)
    {
        var before = _page;
        if (before is null)
        {
            return;
        }

        try { await before.WaitForTimeoutAsync(quietMs); }
        catch { /* the page can close mid-wait when the click navigated away */ }

        var active = _page;
        if (active is null || ReferenceEquals(active, before))
        {
            return;
        }

        try { await active.WaitForLoadStateAsync(LoadState.DOMContentLoaded, new() { Timeout = 15_000 }); }
        catch { /* slow third-party frames must not fail the step */ }

        // Headless Chromium still honours focus/visibility; a background tab can throttle timers and
        // skip animations, which makes elements flaky to click.
        try { await active.BringToFrontAsync(); }
        catch { /* best-effort */ }
    }

    /// <summary>Lets the page settle for a short, bounded interval before the agent observes again.</summary>
    public async Task WaitAsync(int milliseconds = 800, CancellationToken ct = default)    {
        if (_page is null)
        {
            return;
        }

        try
        {
            await _page.WaitForTimeoutAsync(Math.Clamp(milliseconds, 100, 5_000));
        }
        catch
        {
            // Best-effort settle.
        }
    }

    /// <summary>Counts elements matching a stored locator (by strategy). Used to verify and self-heal locators.</summary>
    public async Task<int> CountLocatorMatchesAsync(LocatorStrategy strategy, string value, CancellationToken ct = default)
    {
        if (_page is null || string.IsNullOrWhiteSpace(value))
        {
            return 0;
        }

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

    /// <summary>Counts elements matching a raw CSS or XPath selector (used for LLM-synthesized locators).</summary>
    public async Task<int> CountSelectorMatchesAsync(string value, CancellationToken ct = default)
    {
        if (_page is null || string.IsNullOrWhiteSpace(value))
        {
            return 0;
        }

        try
        {
            var selector = LooksLikeXPath(value) ? (value.StartsWith("xpath=") ? value : $"xpath={value}") : value;
            return await _page.Locator(selector).CountAsync();
        }
        catch
        {
            return 0;
        }
    }

    private static bool LooksLikeXPath(string value) =>
        value.StartsWith("xpath=") || value.StartsWith("/") || value.StartsWith("(") || value.StartsWith("./");

    private static string Quote(string value) => "\"" + value.Replace("\"", "\\\"") + "\"";

    /// <summary>Attempts to click an element by its aria-label or text. Used for dynamic expansion.</summary>
    public async Task<bool> TryClickAsync(string selector, CancellationToken ct = default)
    {
        try
        {
            await _page!.ClickAsync(selector, new PageClickOptions { Timeout = 5_000 });
            await _page.WaitForTimeoutAsync(800);
            return true;
        }
        catch
        {
            return false;
        }
    }

    public async ValueTask DisposeAsync()
    {
        await StopScreencastAsync();
        if (_page is not null) await _page.CloseAsync();
        if (_browser is not null) await _browser.CloseAsync();
        _playwright?.Dispose();
    }
}
