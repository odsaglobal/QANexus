using ATIP.Application.Engine.Contracts;
using ATIP.Application.Engine.Model;
using ATIP.Application.Features.Explorer.Agent;
using ATIP.Domain.Enums;
using Microsoft.Extensions.Logging;

namespace ATIP.Infrastructure.Engine;

/// <summary>
/// Routes actions to the driver for their platform, carries values between them, and owns the
/// lifetime of every driver a run touches.
/// </summary>
/// <remarks>
/// <para>
/// This is the only place that knows a run can span platforms. Drivers stay ignorant of each other:
/// the engine resolves <c>{{variables}}</c> before handing an action over and stores the result
/// afterwards, which is what allows a database step to assert on an id a web step scraped off a
/// confirmation page.
/// </para>
/// <para>
/// Drivers are created lazily and cached per run, so a purely web scenario never starts an Appium
/// session, and a scenario that does touch four platforms starts each exactly once.
/// </para>
/// </remarks>
public sealed class TestEngine : ITestEngine
{
    private readonly Dictionary<TestPlatform, ITestDriverFactory> _factories;
    private readonly Dictionary<TestPlatform, ITestDriver> _drivers = [];
    private readonly ILogger<TestEngine> _logger;

    private ITestDriver? _lastUsed;

    public TestEngine(IEnumerable<ITestDriverFactory> factories, ILogger<TestEngine> logger)
    {
        // Last registration wins, so a host can substitute a platform's driver without removing
        // the default one first.
        _factories = factories.ToDictionary(f => f.Platform, f => f);
        _logger = logger;
    }

    public async Task<ActionResult> ExecuteAsync(TestAction action, RunContext context, CancellationToken cancellationToken)
    {
        ITestDriver driver;
        try
        {
            driver = await GetDriverAsync(action.Platform, context, cancellationToken);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Could not open the {Platform} driver.", action.Platform);
            return ActionResult.NotSupported($"The {action.Platform} driver could not be started: {ex.Message}");
        }

        if (!driver.Supports(action.Kind))
        {
            // Reported rather than attempted: a silent no-op here is how a suite goes green while
            // testing nothing, which is worse than a red step naming the gap.
            return ActionResult.NotSupported($"The {action.Platform} driver does not support '{action.Kind}'.");
        }

        _lastUsed = driver;

        var result = await driver.ExecuteAsync(action, context, cancellationToken);

        if (!string.IsNullOrWhiteSpace(action.SaveAs) && result.Performed)
        {
            context.SetVariable(action.SaveAs!, result.Output);
        }

        return result;
    }

    public async Task<StepExecutionResult> ExecuteStepAsync(
        IReadOnlyList<TestAction> actions,
        RunContext context,
        CancellationToken cancellationToken)
    {
        if (actions.Count == 0)
        {
            return new StepExecutionResult
            {
                Status = StepRunStatus.Skipped,
                Detail = "The step has no actions to execute."
            };
        }

        var results = new List<ActionResult>(actions.Count);
        var checks = new List<StepCheckResult>();
        var healed = false;

        foreach (var action in actions)
        {
            var result = await ExecuteAsync(action, context, cancellationToken);
            results.Add(result);
            checks.AddRange(result.Checks);
            healed |= result.Healed;

            // An action that did not happen invalidates everything after it: the page, the
            // response, or the row the next action assumes simply is not there.
            if (!result.Performed)
            {
                return new StepExecutionResult
                {
                    Status = StepRunStatus.Failed,
                    Detail = result.Detail,
                    Checks = checks,
                    Actions = results
                };
            }

            // A failed assertion is a real defect, but the remaining actions can still run and
            // often reveal more, so only the verdict is recorded here.
            if (!result.Passed)
            {
                return new StepExecutionResult
                {
                    Status = StepRunStatus.Failed,
                    Detail = result.Detail,
                    Checks = checks,
                    Actions = results
                };
            }
        }

        return new StepExecutionResult
        {
            Status = healed ? StepRunStatus.Healed : StepRunStatus.Passed,
            Detail = results[^1].Detail,
            Checks = checks,
            Actions = results
        };
    }

    public async Task<ITestDriver> GetDriverAsync(TestPlatform platform, RunContext context, CancellationToken cancellationToken)
    {
        if (_drivers.TryGetValue(platform, out var existing))
        {
            return existing;
        }

        if (!_factories.TryGetValue(platform, out var factory))
        {
            throw new InvalidOperationException($"No driver is registered for the {platform} platform.");
        }

        var driver = factory.Create();
        await driver.OpenAsync(context, cancellationToken);
        _drivers[platform] = driver;
        return driver;
    }

    public Task<EvidenceCapture?> CaptureEvidenceAsync(CancellationToken cancellationToken) =>
        _lastUsed?.CaptureEvidenceAsync(cancellationToken) ?? Task.FromResult<EvidenceCapture?>(null);

    public async ValueTask DisposeAsync()
    {
        foreach (var driver in _drivers.Values)
        {
            try
            {
                await driver.DisposeAsync();
            }
            catch (Exception ex)
            {
                // One driver failing to close must not strand the others — a leaked browser
                // process outlives the run and eventually exhausts the host.
                _logger.LogWarning(ex, "Failed to dispose the {Platform} driver.", driver.Platform);
            }
        }

        _drivers.Clear();
        _lastUsed = null;
    }
}
