using System.Text.RegularExpressions;
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
public sealed class PlaywrightMcpBrowser : IAsyncDisposable
{
    private readonly ILogger _logger;
    private readonly string _command;
    private readonly IList<string> _args;
    private readonly SemaphoreSlim _gate = new(1, 1);
    private McpClient? _client;

    /// <summary>Best-effort current page URL, parsed from the latest navigate/snapshot output.</summary>
    public string CurrentUrl { get; private set; } = string.Empty;

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

    public async Task<bool> ClickAsync(string element, string reference, CancellationToken ct = default) =>
        (await CallAsync("browser_click", new Dictionary<string, object?> { ["element"] = element, ["ref"] = reference }, ct)).Ok;

    public async Task<bool> TypeAsync(string element, string reference, string text, CancellationToken ct = default) =>
        (await CallAsync("browser_type", new Dictionary<string, object?> { ["element"] = element, ["ref"] = reference, ["text"] = text }, ct)).Ok;

    public async Task<bool> PressKeyAsync(string key, CancellationToken ct = default) =>
        (await CallAsync("browser_press_key", new Dictionary<string, object?> { ["key"] = key }, ct)).Ok;

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
              const nameOf = (n) => norm(n.getAttribute('aria-label') || n.title || n.value || n.innerText || n.textContent);
              const cands = [...document.querySelectorAll('a,button,[role=button],[role=link],[role=menuitem],[role=tab],[role=option],input[type=submit],input[type=button],[onclick]')].filter(vis);
              let el = cands.find(n => nameOf(n) === want)
                    || cands.find(n => nameOf(n).startsWith(want))
                    || cands.find(n => nameOf(n).includes(want));
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
    /// element, so carousels/marquees otherwise cause click timeouts on real sites. Idempotent per page;
    /// must be re-applied after navigations (the injected style is lost on document replacement).
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
                return true;
              } catch { return false; }
            }
            """;
        await EvaluateAsync(js, ct);
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
