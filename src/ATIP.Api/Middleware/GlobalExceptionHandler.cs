using ATIP.Application.Common.Exceptions;
using ATIP.Domain.Exceptions;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using ValidationException = ATIP.Application.Common.Exceptions.ValidationException;

namespace ATIP.Api.Middleware;

/// <summary>
/// Central translation of exceptions into RFC 7807 problem responses. Known application
/// exceptions map to specific 4xx codes; everything else becomes a 500 without leaking internals.
/// </summary>
public sealed class GlobalExceptionHandler : IExceptionHandler
{
    private readonly IProblemDetailsService _problemDetailsService;
    private readonly ILogger<GlobalExceptionHandler> _logger;

    public GlobalExceptionHandler(
        IProblemDetailsService problemDetailsService,
        ILogger<GlobalExceptionHandler> logger)
    {
        _problemDetailsService = problemDetailsService;
        _logger = logger;
    }

    public async ValueTask<bool> TryHandleAsync(
        HttpContext httpContext,
        Exception exception,
        CancellationToken cancellationToken)
    {
        var (statusCode, title) = Map(exception);

        if (statusCode >= StatusCodes.Status500InternalServerError)
        {
            _logger.LogError(exception, "Unhandled exception: {Message}", exception.Message);
        }
        else
        {
            _logger.LogInformation("Request failed ({StatusCode}): {Message}", statusCode, exception.Message);
        }

        httpContext.Response.StatusCode = statusCode;

        var problemDetails = new ProblemDetails
        {
            Status = statusCode,
            Title = title,
            Detail = statusCode >= StatusCodes.Status500InternalServerError
                ? "An unexpected error occurred."
                : exception.Message
        };

        if (exception is ValidationException validationException)
        {
            problemDetails.Extensions["errors"] = validationException.Errors;
        }

        return await _problemDetailsService.TryWriteAsync(new ProblemDetailsContext
        {
            HttpContext = httpContext,
            Exception = exception,
            ProblemDetails = problemDetails
        });
    }

    private static (int StatusCode, string Title) Map(Exception exception) => exception switch
    {
        ValidationException => (StatusCodes.Status400BadRequest, "Validation failed"),
        NotFoundException => (StatusCodes.Status404NotFound, "Resource not found"),
        ForbiddenAccessException => (StatusCodes.Status403Forbidden, "Forbidden"),
        UnauthorizedAccessException => (StatusCodes.Status401Unauthorized, "Unauthorized"),
        DomainException => (StatusCodes.Status409Conflict, "Domain rule violation"),
        _ => (StatusCodes.Status500InternalServerError, "Server error")
    };
}
