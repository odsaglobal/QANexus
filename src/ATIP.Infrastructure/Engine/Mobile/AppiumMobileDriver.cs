using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using ATIP.Application.Engine.Contracts;
using ATIP.Application.Engine.Model;
using ATIP.Application.Features.Explorer.Agent;
using ATIP.Domain.Enums;
using ATIP.Infrastructure.Exploration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace ATIP.Infrastructure.Engine.Mobile;

/// <summary>
/// Drives a native mobile app through an Appium server using the W3C WebDriver protocol.
/// </summary>
/// <remarks>
/// <para>
/// Spoken directly over HTTP rather than through a client library. The protocol surface a test
/// engine needs is a dozen endpoints, all stable and specified, whereas the Appium .NET client
/// pulls in Selenium and its own session lifecycle — a large dependency to own for very little.
/// </para>
/// <para>
/// The driver is complete but unwired: nothing creates mobile scenarios yet. It is written against
/// the real protocol rather than stubbed so that wiring it up is a configuration exercise, not a
/// second implementation effort.
/// </para>
/// </remarks>
public sealed class AppiumMobileDriver : ITestDriver
{
    /// <summary>The W3C element-reference key. A magic constant fixed by the specification.</summary>
    private const string ElementIdKey = "element-6066-11e4-a52e-4f735466cecc";

    private static readonly HashSet<TestActionKind> Supported =
    [
        TestActionKind.Click, TestActionKind.Type, TestActionKind.Clear, TestActionKind.Press,
        TestActionKind.Hover, TestActionKind.WaitFor, TestActionKind.Wait,
        TestActionKind.Navigate, TestActionKind.Back,
        TestActionKind.Screenshot, TestActionKind.Assert, TestActionKind.Capture
    ];

    private readonly HttpClient _http;
    private readonly MobileDriverOptions _options;
    private readonly ILogger<AppiumMobileDriver> _logger;

    private string? _sessionId;

    public AppiumMobileDriver(HttpClient http, IOptions<MobileDriverOptions> options, ILogger<AppiumMobileDriver> logger)
    {
        _http = http;
        _options = options.Value;
        _logger = logger;

        _http.BaseAddress = new Uri(_options.ServerUrl.TrimEnd('/') + "/");
        _http.Timeout = TimeSpan.FromSeconds(_options.RequestTimeoutSeconds);
    }

    public TestPlatform Platform => TestPlatform.Mobile;

    public bool Supports(TestActionKind kind) => Supported.Contains(kind);

    public async Task OpenAsync(RunContext context, CancellationToken cancellationToken)
    {
        if (_sessionId is not null)
        {
            return;
        }

        JsonElement capabilities;
        try
        {
            capabilities = JsonDocument.Parse(_options.CapabilitiesJson).RootElement.Clone();
        }
        catch (JsonException ex)
        {
            throw new InvalidOperationException("Mobile capabilities are not valid JSON.", ex);
        }

        var payload = new { capabilities = new { alwaysMatch = capabilities } };
        var response = await PostAsync("session", payload, cancellationToken);

        _sessionId = response?.TryGetProperty("sessionId", out var id) == true
            ? id.GetString()
            : null;

        if (_sessionId is null)
        {
            throw new InvalidOperationException($"Appium at {_options.ServerUrl} did not return a session.");
        }
    }

    public async Task<ActionResult> ExecuteAsync(TestAction action, RunContext context, CancellationToken cancellationToken)
    {
        if (!Supports(action.Kind))
        {
            return ActionResult.NotSupported($"The mobile driver cannot perform '{action.Kind}'.");
        }

        if (_sessionId is null)
        {
            return ActionResult.Fail("No mobile session is open.");
        }

        var value = context.Resolve(action.Value);
        var timeout = action.OptionAsInt(ActionOptionNames.TimeoutMs, context.DefaultTimeoutMs);

        try
        {
            switch (action.Kind)
            {
                case TestActionKind.Wait:
                    await Task.Delay(Math.Clamp(action.OptionAsInt(ActionOptionNames.TimeoutMs, 800), 50, 60_000), cancellationToken);
                    return ActionResult.Ok("Waited");

                case TestActionKind.Navigate:
                    await PostAsync($"session/{_sessionId}/url", new { url = value }, cancellationToken);
                    return ActionResult.Ok($"Opened {value}");

                case TestActionKind.Back:
                    await PostAsync($"session/{_sessionId}/back", new { }, cancellationToken);
                    return ActionResult.Ok("Navigated back");

                case TestActionKind.Screenshot:
                    var shot = await GetStringAsync($"session/{_sessionId}/screenshot", cancellationToken);
                    return ActionResult.Ok("Screenshot captured", shot);

                case TestActionKind.Assert:
                    return await AssertAsync(action, timeout, cancellationToken);
            }

            var resolved = await FindElementAsync(action.Target, timeout, cancellationToken);
            if (resolved.ElementId is null)
            {
                return ActionResult.Fail($"Could not find {action.Target} in the app.");
            }

            switch (action.Kind)
            {
                case TestActionKind.Click:
                case TestActionKind.Hover:
                    await PostAsync($"session/{_sessionId}/element/{resolved.ElementId}/click", new { }, cancellationToken);
                    break;

                case TestActionKind.Type:
                    await PostAsync($"session/{_sessionId}/element/{resolved.ElementId}/value", new { text = value ?? string.Empty }, cancellationToken);
                    break;

                case TestActionKind.Clear:
                    await PostAsync($"session/{_sessionId}/element/{resolved.ElementId}/clear", new { }, cancellationToken);
                    break;

                case TestActionKind.Press:
                    // The W3C actions payload is the only portable way to send a key to a device.
                    await PostAsync($"session/{_sessionId}/actions", KeyActions(value ?? "Enter"), cancellationToken);
                    break;

                case TestActionKind.WaitFor:
                    // FindElementAsync already polled until it appeared.
                    break;

                case TestActionKind.Capture:
                    var text = await GetStringAsync($"session/{_sessionId}/element/{resolved.ElementId}/text", cancellationToken);
                    return ActionResult.Ok($"Captured '{text}'", text, resolved.Winner);

                default:
                    return ActionResult.NotSupported($"The mobile driver cannot perform '{action.Kind}'.");
            }

            return ActionResult.Ok($"{action.Kind} on {action.Target}", null, resolved.Winner);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Mobile action {Kind} failed.", action.Kind);
            return ActionResult.Fail($"{action.Kind} failed: {ex.Message}");
        }
    }

    public async Task<EvidenceCapture?> CaptureEvidenceAsync(CancellationToken cancellationToken)
    {
        if (_sessionId is null)
        {
            return null;
        }

        try
        {
            return new EvidenceCapture
            {
                ScreenshotBase64 = await GetStringAsync($"session/{_sessionId}/screenshot", cancellationToken),
                Location = await GetStringAsync($"session/{_sessionId}/url", cancellationToken),
                Text = Truncate(await GetStringAsync($"session/{_sessionId}/source", cancellationToken), 8000)
            };
        }
        catch
        {
            return null;
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (_sessionId is null)
        {
            return;
        }

        try
        {
            await _http.DeleteAsync($"session/{_sessionId}", CancellationToken.None);
        }
        catch
        {
            // The server may already have reaped the session; there is nothing to recover.
        }
        finally
        {
            _sessionId = null;
        }
    }

    // ── Elements ────────────────────────────────────────────────────────────────────────────

    private readonly record struct MobileResolution(string? ElementId, LocatorCandidate? Winner);

    private async Task<MobileResolution> FindElementAsync(ElementTarget? target, int timeoutMs, CancellationToken ct)
    {
        if (target is null || target.IsEmpty)
        {
            return new MobileResolution(null, null);
        }

        var candidates = target.Candidates.OrderBy(c => c.Rank).ToList();
        if (candidates.Count == 0 && !string.IsNullOrWhiteSpace(target.Descriptor))
        {
            // Accessibility id first: it is the mobile equivalent of a test hook, and unlike an
            // XPath over the view hierarchy it survives layout changes.
            candidates.Add(LocatorCandidate.Of(LocatorStrategy.AccessibilityId, target.Descriptor!));
            candidates.Add(LocatorCandidate.Of(LocatorStrategy.XPath, $"//*[@text={Escape(target.Descriptor!)}]", 1));
            candidates.Add(LocatorCandidate.Of(LocatorStrategy.XPath, $"//*[@label={Escape(target.Descriptor!)}]", 2));
        }

        var deadline = DateTimeOffset.UtcNow.AddMilliseconds(timeoutMs);

        // Poll rather than fire once: a native screen is frequently still animating in when the
        // previous action returns, and a single lookup would race it.
        do
        {
            foreach (var candidate in candidates)
            {
                var using_ = ToW3CStrategy(candidate.Strategy);
                if (using_ is null)
                {
                    continue;
                }

                var response = await PostAsync(
                    $"session/{_sessionId}/element",
                    new { @using = using_, value = candidate.Value },
                    ct,
                    throwOnError: false);

                if (response?.TryGetProperty("value", out var element) == true
                    && element.ValueKind == JsonValueKind.Object
                    && element.TryGetProperty(ElementIdKey, out var id))
                {
                    return new MobileResolution(id.GetString(), candidate);
                }
            }

            await Task.Delay(300, ct);
        }
        while (DateTimeOffset.UtcNow < deadline);

        return new MobileResolution(null, null);
    }

    private static string? ToW3CStrategy(LocatorStrategy strategy) => strategy switch
    {
        LocatorStrategy.AccessibilityId => "accessibility id",
        LocatorStrategy.AndroidUiAutomator => "-android uiautomator",
        LocatorStrategy.IosPredicate => "-ios predicate string",
        LocatorStrategy.IosClassChain => "-ios class chain",
        LocatorStrategy.XPath => "xpath",
        LocatorStrategy.CSS or LocatorStrategy.TestId => "css selector",
        // Text/role/placeholder are web notions with no native equivalent; skipping them is better
        // than translating them into an XPath that quietly matches the wrong control.
        _ => null
    };

    private async Task<ActionResult> AssertAsync(TestAction action, int timeout, CancellationToken ct)
    {
        if (action.Assertion is null)
        {
            return ActionResult.Fail("Assert action has no compiled assertion.");
        }

        var assertion = action.Assertion;
        var label = string.IsNullOrWhiteSpace(assertion.Label) ? "Mobile assertion" : assertion.Label.Trim();

        var target = new ElementTarget { Descriptor = assertion.Selector };
        var resolved = await FindElementAsync(target, Math.Min(timeout, 5000), ct);

        var op = (assertion.Operator ?? "contains").Trim().ToLowerInvariant();

        if (op is "exists" or "not_exists")
        {
            var present = resolved.ElementId is not null;
            var existsCheck = new StepCheckResult
            {
                Label = label,
                Passed = op == "exists" ? present : !present,
                Expected = op == "exists" ? "element present" : "element absent",
                Actual = present ? "present" : "absent"
            };

            return ActionResult.Assertion(existsCheck.Passed, $"{label}: {existsCheck.Actual}", [existsCheck]);
        }

        if (resolved.ElementId is null)
        {
            var miss = new StepCheckResult
            {
                Label = label,
                Passed = false,
                Unresolved = true,
                Expected = assertion.Expected ?? string.Empty,
                Actual = $"'{assertion.Selector}' was not found on screen"
            };

            return ActionResult.Assertion(false, $"{label}: {miss.Actual}", [miss]);
        }

        var text = await GetStringAsync($"session/{_sessionId}/element/{resolved.ElementId}/text", ct);
        var (passed, actual, unresolved) = AssertionEvaluator.Compare(op, [text ?? string.Empty], assertion.Expected);

        var check = new StepCheckResult
        {
            Label = label,
            Passed = passed,
            Unresolved = unresolved,
            Expected = assertion.Expected ?? string.Empty,
            Actual = actual
        };

        return ActionResult.Assertion(passed, passed ? $"{label}: passed" : $"{label}: {actual}", [check]);
    }

    private static object KeyActions(string key) => new
    {
        actions = new[]
        {
            new
            {
                type = "key",
                id = "keyboard",
                actions = new object[]
                {
                    new { type = "keyDown", value = key },
                    new { type = "keyUp", value = key }
                }
            }
        }
    };

    // ── Transport ───────────────────────────────────────────────────────────────────────────

    private async Task<JsonElement?> PostAsync(string path, object payload, CancellationToken ct, bool throwOnError = true)
    {
        using var response = await _http.PostAsJsonAsync(path, payload, ct);
        var body = await response.Content.ReadAsStringAsync(ct);

        if (!response.IsSuccessStatusCode)
        {
            if (!throwOnError)
            {
                return null;
            }

            throw new InvalidOperationException($"Appium {path} returned {(int)response.StatusCode}: {Truncate(body, 500)}");
        }

        try
        {
            return JsonDocument.Parse(body).RootElement.Clone();
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private async Task<string?> GetStringAsync(string path, CancellationToken ct)
    {
        using var response = await _http.GetAsync(path, ct);
        if (!response.IsSuccessStatusCode)
        {
            return null;
        }

        var body = await response.Content.ReadAsStringAsync(ct);

        try
        {
            var root = JsonDocument.Parse(body).RootElement;
            return root.TryGetProperty("value", out var value)
                ? value.ValueKind == JsonValueKind.String ? value.GetString() : value.ToString()
                : body;
        }
        catch (JsonException)
        {
            return body;
        }
    }

    private static string Escape(string value) => "'" + value.Replace("'", "\\'") + "'";

    private static string? Truncate(string? value, int max) =>
        value is null || value.Length <= max ? value : value[..max] + "…";
}
