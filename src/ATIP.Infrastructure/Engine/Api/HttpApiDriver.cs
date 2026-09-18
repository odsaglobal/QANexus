using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using ATIP.Application.Engine.Contracts;
using ATIP.Application.Engine.Model;
using ATIP.Application.Features.Explorer.Agent;
using ATIP.Domain.Enums;
using ATIP.Infrastructure.Exploration;
using Microsoft.Extensions.Logging;

namespace ATIP.Infrastructure.Engine.Api;

/// <summary>
/// Executes HTTP requests as test steps, so a scenario can assert on a service directly instead of
/// inferring its behaviour from what the UI happened to render.
/// </summary>
/// <remarks>
/// The driver keeps the last response around because assertions are separate actions: a step says
/// "call the orders endpoint", the next says "the response status is 201 and the body has an id".
/// Re-issuing the request for each assertion would be both slow and wrong — a POST is not safe to
/// repeat.
/// </remarks>
public sealed class HttpApiDriver : ITestDriver
{
    private static readonly HashSet<TestActionKind> Supported =
    [
        TestActionKind.HttpRequest, TestActionKind.Assert, TestActionKind.Capture, TestActionKind.Wait
    ];

    private readonly HttpClient _http;
    private readonly ILogger<HttpApiDriver> _logger;

    private string? _lastBody;
    private int _lastStatus;
    private string? _lastUrl;
    private HttpResponseHeaders? _lastHeaders;

    public HttpApiDriver(HttpClient http, ILogger<HttpApiDriver> logger)
    {
        _http = http;
        _logger = logger;
    }

    public TestPlatform Platform => TestPlatform.Api;

    public bool Supports(TestActionKind kind) => Supported.Contains(kind);

    public Task OpenAsync(RunContext context, CancellationToken cancellationToken) => Task.CompletedTask;

    public async Task<ActionResult> ExecuteAsync(TestAction action, RunContext context, CancellationToken cancellationToken)
    {
        if (!Supports(action.Kind))
        {
            return ActionResult.NotSupported($"The API driver cannot perform '{action.Kind}'.");
        }

        try
        {
            return action.Kind switch
            {
                TestActionKind.HttpRequest => await SendAsync(action, context, cancellationToken),
                TestActionKind.Assert => Assert(action),
                TestActionKind.Capture => Capture(action),
                TestActionKind.Wait => await WaitAsync(action, context, cancellationToken),
                _ => ActionResult.NotSupported($"The API driver cannot perform '{action.Kind}'.")
            };
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "API action {Kind} failed.", action.Kind);
            return ActionResult.Fail($"{action.Kind} failed: {ex.Message}");
        }
    }

    public Task<EvidenceCapture?> CaptureEvidenceAsync(CancellationToken cancellationToken) =>
        Task.FromResult<EvidenceCapture?>(_lastUrl is null
            ? null
            : new EvidenceCapture
            {
                Location = _lastUrl,
                Title = $"HTTP {_lastStatus}",
                Text = Truncate(_lastBody, 8000)
            });

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;

    // ── Request ─────────────────────────────────────────────────────────────────────────────

    private async Task<ActionResult> SendAsync(TestAction action, RunContext context, CancellationToken ct)
    {
        var rawUrl = action.Option(ActionOptionNames.HttpUrl) ?? action.Target?.Descriptor;
        var url = BuildUrl(context.Resolve(rawUrl), action, context);
        if (string.IsNullOrWhiteSpace(url))
        {
            return ActionResult.Fail("HTTP request needs a URL.");
        }

        var method = new HttpMethod((action.Option(ActionOptionNames.HttpMethod) ?? "GET").Trim().ToUpperInvariant());
        using var request = new HttpRequestMessage(method, url);

        var body = context.Resolve(action.Value);
        if (!string.IsNullOrWhiteSpace(body) && method != HttpMethod.Get && method != HttpMethod.Head)
        {
            var contentType = action.Option(ActionOptionNames.HttpContentType) ?? "application/json";
            request.Content = new StringContent(body!, Encoding.UTF8, contentType);
        }

        foreach (var (name, headerValue) in ParseJsonObject(context.Resolve(action.Option(ActionOptionNames.HttpHeaders))))
        {
            // Content headers are rejected on the request message, so they go on the body instead.
            if (!request.Headers.TryAddWithoutValidation(name, headerValue))
            {
                request.Content?.Headers.TryAddWithoutValidation(name, headerValue);
            }
        }

        var timeout = action.OptionAsInt(ActionOptionNames.TimeoutMs, context.DefaultTimeoutMs);
        using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        timeoutCts.CancelAfter(timeout);

        using var response = await _http.SendAsync(request, HttpCompletionOption.ResponseContentRead, timeoutCts.Token);

        _lastStatus = (int)response.StatusCode;
        _lastBody = await response.Content.ReadAsStringAsync(ct);
        _lastHeaders = response.Headers;
        _lastUrl = url;

        var expected = action.OptionAsInt(ActionOptionNames.ExpectedStatus, 0);
        if (expected > 0 && expected != _lastStatus)
        {
            return new ActionResult
            {
                Performed = true,
                Passed = false,
                Detail = $"{method} {url} returned {_lastStatus}, expected {expected}. Body: {Truncate(_lastBody, 500)}",
                Output = _lastBody
            };
        }

        return ActionResult.Ok($"{method} {url} → {_lastStatus}", _lastBody);
    }

    private static string? BuildUrl(string? url, TestAction action, RunContext context)
    {
        if (string.IsNullOrWhiteSpace(url))
        {
            return null;
        }

        if (!url.StartsWith("http://", StringComparison.OrdinalIgnoreCase)
            && !url.StartsWith("https://", StringComparison.OrdinalIgnoreCase)
            && !string.IsNullOrWhiteSpace(context.BaseUrl)
            && Uri.TryCreate(new Uri(context.BaseUrl!), url, out var combined))
        {
            url = combined.ToString();
        }

        var query = ParseJsonObject(context.Resolve(action.Option(ActionOptionNames.HttpQuery))).ToList();
        if (query.Count == 0)
        {
            return url;
        }

        var separator = url.Contains('?') ? '&' : '?';
        var encoded = string.Join('&', query.Select(kv => $"{Uri.EscapeDataString(kv.Key)}={Uri.EscapeDataString(kv.Value ?? string.Empty)}"));
        return url + separator + encoded;
    }

    private static async Task<ActionResult> WaitAsync(TestAction action, RunContext context, CancellationToken ct)
    {
        var ms = action.OptionAsInt(ActionOptionNames.TimeoutMs, 0);
        if (ms <= 0 && int.TryParse(context.Resolve(action.Value), out var parsed))
        {
            ms = parsed;
        }

        ms = Math.Clamp(ms <= 0 ? 500 : ms, 50, 60_000);
        await Task.Delay(ms, ct);
        return ActionResult.Ok($"Waited {ms}ms");
    }

    // ── Assertions and capture ──────────────────────────────────────────────────────────────

    private ActionResult Assert(TestAction action)
    {
        if (action.Assertion is null)
        {
            return ActionResult.Fail("Assert action has no compiled assertion.");
        }

        if (_lastUrl is null)
        {
            return ActionResult.Fail("There is no HTTP response to assert on — send a request first.");
        }

        var assertion = action.Assertion;
        var label = string.IsNullOrWhiteSpace(assertion.Label) ? "API assertion" : assertion.Label.Trim();
        var values = ReadValues(assertion.Source, assertion.Selector, out var unresolved);

        if (unresolved)
        {
            var miss = new StepCheckResult
            {
                Label = label,
                Passed = false,
                Unresolved = true,
                Expected = assertion.Expected ?? string.Empty,
                Actual = $"could not read '{assertion.Selector}' from the response"
            };

            return ActionResult.Assertion(false, $"{label}: {miss.Actual}", [miss]);
        }

        // Reuses the same comparison semantics as the web assertions, so "contains" or
        // "greater_than" mean exactly one thing across the whole product.
        var (passed, actual, brokenAssertion) = AssertionEvaluator.Compare(
            (assertion.Operator ?? "contains").Trim().ToLowerInvariant(),
            values,
            assertion.Expected);

        var check = new StepCheckResult
        {
            Label = label,
            Passed = passed,
            Unresolved = brokenAssertion,
            Expected = assertion.Expected ?? string.Empty,
            Actual = actual
        };

        return ActionResult.Assertion(passed, passed ? $"{label}: passed" : $"{label}: {actual}", [check]);
    }

    private ActionResult Capture(TestAction action)
    {
        var source = action.Option(ActionOptionNames.CaptureSource) ?? "json";
        var expression = action.Option(ActionOptionNames.CaptureExpression) ?? action.Value;

        var values = ReadValues(source, expression, out var unresolved);
        if (unresolved || values.Count == 0)
        {
            return ActionResult.Fail($"Could not capture '{expression}' from the response.");
        }

        return ActionResult.Ok($"Captured '{values[0]}'", values[0]);
    }

    private IReadOnlyList<string> ReadValues(string? source, string? selector, out bool unresolved)
    {
        unresolved = false;

        switch ((source ?? "json").Trim().ToLowerInvariant())
        {
            case "status":
                return [_lastStatus.ToString()];

            case "body":
            case "text":
                return [_lastBody ?? string.Empty];

            case "header":
                if (string.IsNullOrWhiteSpace(selector)
                    || _lastHeaders is null
                    || !_lastHeaders.TryGetValues(selector, out var headerValues))
                {
                    unresolved = true;
                    return [];
                }

                return headerValues.ToList();

            default:
                var extracted = JsonPath.Read(_lastBody, selector);
                if (extracted is null)
                {
                    unresolved = true;
                    return [];
                }

                return extracted;
        }
    }

    private static IEnumerable<KeyValuePair<string, string?>> ParseJsonObject(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            yield break;
        }

        JsonElement root;
        try
        {
            root = JsonDocument.Parse(json).RootElement;
        }
        catch (JsonException)
        {
            yield break;
        }

        if (root.ValueKind != JsonValueKind.Object)
        {
            yield break;
        }

        foreach (var property in root.EnumerateObject())
        {
            yield return new KeyValuePair<string, string?>(
                property.Name,
                property.Value.ValueKind == JsonValueKind.String ? property.Value.GetString() : property.Value.ToString());
        }
    }

    private static string? Truncate(string? value, int max) =>
        value is null || value.Length <= max ? value : value[..max] + "…";
}
