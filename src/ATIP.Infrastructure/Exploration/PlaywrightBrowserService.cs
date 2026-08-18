using System.Diagnostics;
using System.Text;
using System.Text.Json;
using ATIP.Domain.Enums;
using Microsoft.Playwright;

namespace ATIP.Infrastructure.Exploration;

/// <summary>
/// Thin Playwright wrapper used exclusively by <see cref="ExplorerAgent"/>. One instance per
/// exploration session; disposed after the session completes.
/// </summary>
public sealed class PlaywrightBrowserService : IAsyncDisposable
{

    // JavaScript injected into every page to discover interactive elements.
    private const string ElementDiscoveryScript = """
        (() => {
          const sel = [
            'a[href]', 'button', 'input:not([type="hidden"])', 'select', 'textarea', 'label',
            '[role="button"]', '[role="link"]', '[role="checkbox"]', '[role="radio"]',
            '[role="combobox"]', '[role="listbox"]', '[role="menuitem"]', '[role="option"]',
            '[role="tab"]', '[role="switch"]', '[role="slider"]', '[tabindex="0"]'
          ].join(',');

          const getXPath = (el) => {
            if (!el || el === document) return '';
            if (el.id) return `//*[@id="${el.id}"]`;
            const siblings = Array.from(el.parentNode?.children || []).filter(s => s.tagName === el.tagName);
            const idx = siblings.indexOf(el) + 1;
            const tag = el.tagName.toLowerCase();
            return getXPath(el.parentNode) + '/' + (siblings.length > 1 ? `${tag}[${idx}]` : tag);
          };

          const getCss = (el) => {
            if (el.id) return '#' + CSS.escape(el.id);
            if (el.getAttribute('data-testid')) return `[data-testid="${el.getAttribute('data-testid')}"]`;
            const tag = el.tagName.toLowerCase();
            const cls = el.className ? '.' + el.className.trim().split(/\s+/).slice(0, 2).map(c => CSS.escape(c)).join('.') : '';
            return tag + cls;
          };

          const seen = new Set();
          const results = [];
          for (const el of document.querySelectorAll(sel)) {
            const rect = el.getBoundingClientRect();
            if (rect.width < 1 || rect.height < 1) continue;
            const key = el.tagName + ':' + (el.id || el.getAttribute('aria-label') || el.textContent?.trim().substring(0, 30));
            if (seen.has(key)) continue;
            seen.add(key);
            results.push({
              tagName: el.tagName.toLowerCase(),
              role: el.getAttribute('role') || el.tagName.toLowerCase(),
              ariaLabel: el.getAttribute('aria-label') || '',
              name: el.getAttribute('aria-label') || el.textContent?.trim().substring(0, 100) || el.getAttribute('placeholder') || '',
              placeholder: el.getAttribute('placeholder') || '',
              textContent: el.textContent?.trim().substring(0, 200) || '',
              dataTestId: el.getAttribute('data-testid') || el.getAttribute('data-test') || '',
              id: el.id || '',
              href: el instanceof HTMLAnchorElement ? el.href : '',
              inputType: el instanceof HTMLInputElement ? el.type : '',
              cssSelector: getCss(el),
              xpath: getXPath(el),
              rect: { x: Math.round(rect.x), y: Math.round(rect.y), w: Math.round(rect.width), h: Math.round(rect.height) },
              isInteractive: true
            });
            if (results.length >= 250) break;
          }
          return results;
        })()
        """;

    // Playwright-MCP-style snapshot. Stamps every VISIBLE interactive element with a stable
    // data-atip-ref (e1, e2, …) and returns a compact, ref-annotated list the LLM selects from,
    // e.g.  - button "Sign in" [ref=e5]. Refs live on the DOM until the next snapshot/navigation,
    // so an action taken right after the snapshot resolves deterministically via [data-atip-ref='eN']
    // — exactly how Copilot/Claude drive the browser through Playwright MCP (no brittle text matching).
    private const string AgentSnapshotScript = """
        (max) => {
          const sel = [
            'a[href]', 'button', 'input:not([type="hidden"])', 'select', 'textarea',
            '[role="button"]', '[role="link"]', '[role="checkbox"]', '[role="radio"]',
            '[role="combobox"]', '[role="listbox"]', '[role="menuitem"]', '[role="option"]',
            '[role="tab"]', '[role="switch"]', '[role="slider"]', '[contenteditable="true"]', '[tabindex="0"]'
          ].join(',');
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
            return t.toLowerCase();
          };
          const nameOf = (el) => {
            let n = el.getAttribute('aria-label') || '';
            if (!n) n = (el.textContent || '').replace(/\s+/g, ' ').trim();
            if (!n) n = el.getAttribute('placeholder') || el.getAttribute('name') || el.getAttribute('title') || (el.value || '');
            return (n || '').replace(/\s+/g, ' ').trim().slice(0, 80);
          };
          document.querySelectorAll('[data-atip-ref]').forEach(e => e.removeAttribute('data-atip-ref'));
          const lines = [];
          let n = 0;
          const seen = new Set();
          for (const el of document.querySelectorAll(sel)) {
            if (n >= max) break;
            if (!isVisible(el)) continue;
            const role = roleOf(el);
            const name = nameOf(el);
            const key = role + '|' + name + '|' + Math.round(el.getBoundingClientRect().y);
            if (seen.has(key)) continue;
            seen.add(key);
            n++;
            const ref = 'e' + n;
            el.setAttribute('data-atip-ref', ref);
            const ph = el.getAttribute('placeholder');
            const type = el.tagName === 'INPUT' ? (el.getAttribute('type') || 'text') : '';
            let line = '- ' + role + ' "' + name + '" [ref=' + ref + ']';
            if (ph) line += ' (placeholder: "' + ph + '")';
            if (type) line += ' (type: ' + type + ')';
            lines.push(line);
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

        // Auto-dismiss native JS dialogs (alert/confirm/prompt/beforeunload) so they never block the run.
        _page.Dialog += async (_, dialog) =>
        {
            try { await dialog.DismissAsync(); } catch { /* dialog may already be handled */ }
        };

        // Pre-grant geolocation/notification permissions so the browser never shows a permission prompt.
        try
        {
            await _page.Context.GrantPermissionsAsync(new[] { "geolocation", "notifications" });
        }
        catch { /* permission grant is best-effort */ }
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
        _cdp = await _page.Context.NewCDPSessionAsync(_page);
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

    public async Task<IReadOnlyList<ElementDiscoveryInfo>> DiscoverElementsAsync(CancellationToken ct = default)
    {
        try
        {
            var result = await _page!.EvaluateAsync<ElementDiscoveryInfo[]>(ElementDiscoveryScript);
            return result ?? [];
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
    /// Returns a compact, numbered list of the currently visible interactive elements (role + name)
    /// so the AI can choose a real, resolvable target rather than guessing a selector.
    /// </summary>
    public async Task<string> GetInteractiveSummaryAsync(int max = 50, CancellationToken ct = default)
    {
        var elements = await DiscoverElementsAsync(ct);
        if (elements.Count == 0)
        {
            return "(no interactive elements detected)";
        }

        var sb = new StringBuilder();
        var i = 0;
        foreach (var el in elements)
        {
            var name = el.Name ?? el.AriaLabel ?? el.TextContent ?? el.Placeholder;
            if (string.IsNullOrWhiteSpace(name))
            {
                continue;
            }

            name = name.Trim();
            if (name.Length > 60)
            {
                name = name[..60];
            }

            sb.Append('[').Append(el.Role ?? el.TagName ?? "el").Append("] \"").Append(name).Append('"');
            if (!string.IsNullOrWhiteSpace(el.Placeholder))
            {
                sb.Append(" (placeholder: ").Append(el.Placeholder.Trim()).Append(')');
            }
            sb.Append('\n');

            if (++i >= max)
            {
                break;
            }
        }

        return sb.Length == 0 ? "(no named interactive elements detected)" : sb.ToString();
    }

    /// <summary>
    /// Playwright-MCP-style page snapshot for the LLM: every visible interactive element is
    /// stamped with a stable ref (e1, e2, …) and returned as a compact list, e.g.
    ///   - button "Sign in" [ref=e5]
    /// The agent selects an element by ref, and the ref is resolved deterministically via
    /// <see cref="TryClickByRefAsync"/> / <see cref="TryFillByRefAsync"/> — no brittle text matching.
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

    /// <summary>Clicks the element carrying the given ref from the most recent agent snapshot.</summary>
    public async Task<bool> TryClickByRefAsync(string reference, CancellationToken ct = default)
    {
        var locator = ResolveRefLocator(reference);
        if (locator is null)
        {
            return false;
        }

        try
        {
            if (await locator.CountAsync() == 0)
            {
                return false;
            }

            await locator.ScrollIntoViewIfNeededAsync(new() { Timeout = 3_000 });
            await locator.ClickAsync(new LocatorClickOptions { Timeout = 5_000 });
            await _page!.WaitForTimeoutAsync(600);
            return true;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>Fills the element carrying the given ref from the most recent agent snapshot.</summary>
    public async Task<bool> TryFillByRefAsync(string reference, string value, CancellationToken ct = default)
    {
        var locator = ResolveRefLocator(reference);
        if (locator is null)
        {
            return false;
        }

        try
        {
            if (await locator.CountAsync() == 0)
            {
                return false;
            }

            await locator.ScrollIntoViewIfNeededAsync(new() { Timeout = 3_000 });
            await locator.FillAsync(value, new LocatorFillOptions { Timeout = 5_000 });
            await _page!.WaitForTimeoutAsync(300);
            return true;
        }
        catch
        {
            return false;
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
            await _page.WaitForTimeoutAsync(400);
            return true;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>Lets the page settle for a short, bounded interval before the agent observes again.</summary>
    public async Task WaitAsync(int milliseconds = 800, CancellationToken ct = default)
    {
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

    /// <summary>
    /// Clicks an element identified by a human hint (the exact visible text, label, or aria-label
    /// chosen by the AI), trying a sequence of resolution strategies. Returns the index of the
    /// strategy that resolved it (0 = primary strategy, &gt;0 = a healed/fallback strategy), or -1
    /// if no strategy matched.
    /// </summary>
    public async Task<int> TryClickByHintAsync(string hint, CancellationToken ct = default)
    {
        if (_page is null || string.IsNullOrWhiteSpace(hint))
        {
            return -1;
        }

        var candidates = new Func<ILocator>[]
        {
            () => _page.GetByRole(AriaRole.Button, new() { Name = hint, Exact = false }),
            () => _page.GetByRole(AriaRole.Link, new() { Name = hint, Exact = false }),
            () => _page.GetByRole(AriaRole.Menuitem, new() { Name = hint, Exact = false }),
            () => _page.GetByRole(AriaRole.Tab, new() { Name = hint, Exact = false }),
            () => _page.GetByText(hint, new() { Exact = true }),
            () => _page.GetByLabel(hint),
            () => _page.Locator($"[aria-label={Quote(hint)}]"),
            () => _page.GetByText(hint, new() { Exact = false }),
            () => _page.GetByTitle(hint),
        };

        for (var i = 0; i < candidates.Length; i++)
        {
            try
            {
                var locator = candidates[i]().First;
                if (await locator.CountAsync() == 0)
                {
                    continue;
                }

                await locator.ScrollIntoViewIfNeededAsync(new() { Timeout = 3_000 });
                await locator.ClickAsync(new LocatorClickOptions { Timeout = 5_000 });
                await _page.WaitForTimeoutAsync(600);
                return i;
            }
            catch
            {
                // Strategy failed — try the next fallback.
            }
        }

        return -1;
    }

    /// <summary>
    /// Fills a field identified by a human hint (label, placeholder, or aria-label chosen by the AI),
    /// trying a sequence of resolution strategies. Returns the index of the strategy that resolved it
    /// (0 = primary, &gt;0 = a healed/fallback strategy), or -1 if no field matched.
    /// </summary>
    public async Task<int> TryFillAsync(string target, string value, CancellationToken ct = default)
    {
        if (_page is null || string.IsNullOrWhiteSpace(target))
        {
            return -1;
        }

        var candidates = new Func<ILocator>[]
        {
            () => _page.GetByLabel(target),
            () => _page.GetByPlaceholder(target),
            () => _page.GetByRole(AriaRole.Textbox, new() { Name = target, Exact = false }),
            () => _page.Locator($"[aria-label={Quote(target)}]"),
            () => _page.GetByPlaceholder(target, new() { Exact = false }),
            () => _page.Locator($"input[name={Quote(target)}],input[id={Quote(target)}],textarea[name={Quote(target)}]"),
        };

        for (var i = 0; i < candidates.Length; i++)
        {
            try
            {
                var locator = candidates[i]().First;
                if (await locator.CountAsync() == 0)
                {
                    continue;
                }

                await locator.FillAsync(value, new LocatorFillOptions { Timeout = 5_000 });
                await _page.WaitForTimeoutAsync(300);
                return i;
            }
            catch
            {
                // Strategy failed — try the next fallback.
            }
        }

        return -1;
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

    /// <summary>Builds a Playwright locator from a stored <see cref="LocatorStrategy"/> and value.</summary>
    private ILocator? BuildLocator(LocatorStrategy strategy, string value)
    {
        if (_page is null)
        {
            return null;
        }

        switch (strategy)
        {
            case LocatorStrategy.CSS:
            case LocatorStrategy.DataAttribute:
                return _page.Locator(value);
            case LocatorStrategy.XPath:
                return _page.Locator(value.StartsWith("xpath=") ? value : $"xpath={value}");
            case LocatorStrategy.ARIA:
            case LocatorStrategy.NearbyLabel:
                return _page.Locator($"[aria-label={Quote(value)}]");
            case LocatorStrategy.Placeholder:
                return _page.GetByPlaceholder(value);
            case LocatorStrategy.Text:
                return _page.GetByText(value);
            case LocatorStrategy.Role:
                var parts = value.Split(':', 2);
                if (parts.Length == 2 && Enum.TryParse<AriaRole>(parts[0], ignoreCase: true, out var role))
                {
                    return _page.GetByRole(role, new() { Name = parts[1], Exact = false });
                }
                return _page.GetByText(parts[^1]);
            default:
                return null;
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
