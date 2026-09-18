using ATIP.Application.Engine.Model;
using ATIP.Domain.Enums;

namespace ATIP.Application.Engine.Contracts;

/// <summary>
/// Creates the driver for a platform. Separate from the engine so a test can substitute a fake
/// driver without standing up Playwright, and so adding a platform is a registration change rather
/// than an edit to the orchestrator.
/// </summary>
public interface ITestDriverFactory
{
    TestPlatform Platform { get; }

    ITestDriver Create();
}

/// <summary>
/// Runs actions and steps against whichever platform each one names, owning driver lifetime,
/// variable flow and evidence for the duration of a run.
/// </summary>
/// <remarks>
/// Scoped per run. Drivers are created on first use and disposed by <see cref="IAsyncDisposable"/>,
/// so an aborted run cannot leak a browser process.
/// </remarks>
public interface ITestEngine : IAsyncDisposable
{
    /// <summary>
    /// Executes one action, routing it to the right driver, resolving <c>{{variables}}</c> in its
    /// payload and storing its output under <see cref="TestAction.SaveAs"/>.
    /// </summary>
    Task<ActionResult> ExecuteAsync(TestAction action, RunContext context, CancellationToken cancellationToken);

    /// <summary>
    /// Executes an ordered group of actions as a single step, stopping at the first one that does
    /// not perform, and aggregating the results into one verdict.
    /// </summary>
    Task<StepExecutionResult> ExecuteStepAsync(
        IReadOnlyList<TestAction> actions,
        RunContext context,
        CancellationToken cancellationToken);

    /// <summary>
    /// Gets (creating if needed) the live driver for a platform. Exposed so the existing explorer
    /// can keep reaching into the web driver for exploration work the action model does not cover.
    /// </summary>
    Task<ITestDriver> GetDriverAsync(TestPlatform platform, RunContext context, CancellationToken cancellationToken);

    /// <summary>Evidence from the driver that ran the most recent action.</summary>
    Task<EvidenceCapture?> CaptureEvidenceAsync(CancellationToken cancellationToken);
}
