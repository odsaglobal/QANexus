using System.Diagnostics;
using MediatR;
using Microsoft.Extensions.Logging;

namespace ATIP.Application.Common.Behaviors;

/// <summary>
/// Emits a structured start/stop log with elapsed milliseconds for every request passing
/// through the mediator, giving uniform observability without touching individual handlers.
/// </summary>
public sealed class LoggingBehavior<TRequest, TResponse> : IPipelineBehavior<TRequest, TResponse>
    where TRequest : notnull
{
    private readonly ILogger<LoggingBehavior<TRequest, TResponse>> _logger;

    public LoggingBehavior(ILogger<LoggingBehavior<TRequest, TResponse>> logger) => _logger = logger;

    public async Task<TResponse> Handle(
        TRequest request,
        RequestHandlerDelegate<TResponse> next,
        CancellationToken cancellationToken)
    {
        var requestName = typeof(TRequest).Name;
        var stopwatch = Stopwatch.StartNew();

        _logger.LogInformation("Handling {RequestName}", requestName);
        try
        {
            var response = await next();
            stopwatch.Stop();
            _logger.LogInformation(
                "Handled {RequestName} in {ElapsedMilliseconds}ms",
                requestName,
                stopwatch.ElapsedMilliseconds);
            return response;
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            _logger.LogWarning(
                ex,
                "{RequestName} failed after {ElapsedMilliseconds}ms: {Message}",
                requestName,
                stopwatch.ElapsedMilliseconds,
                ex.Message);
            throw;
        }
    }
}
