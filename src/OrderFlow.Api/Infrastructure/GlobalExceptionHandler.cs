using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using OrderFlow.Application.Common;

namespace OrderFlow.Api.Infrastructure;

/// <summary>
/// Last line of defence: turns unhandled exceptions into RFC 9457 problem details. Expected
/// failures never get here — they are returned as <c>Result</c> values by the handlers.
/// </summary>
internal sealed partial class GlobalExceptionHandler(
    IProblemDetailsService problemDetailsService,
    ILogger<GlobalExceptionHandler> logger)
    : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(
        HttpContext httpContext,
        Exception exception,
        CancellationToken cancellationToken)
    {
        // Client-facing details are fixed strings: exception messages may contain internals
        // such as constraint names, so they only go to the log.
        var (statusCode, title, detail, code) = exception switch
        {
            ConcurrencyConflictException => (
                StatusCodes.Status409Conflict,
                "Concurrency conflict",
                "The data was changed by another request. Retry the request.",
                ProblemCodes.ConcurrencyConflict),
            UniqueConstraintViolationException => (
                StatusCodes.Status409Conflict,
                "Duplicate resource",
                "A resource with the same unique value already exists.",
                ProblemCodes.DuplicateResource),
            BadHttpRequestException badRequest => (
                badRequest.StatusCode,
                "Bad request",
                "The request could not be read. Check the route, query string and JSON body.",
                ProblemCodes.ForStatus(badRequest.StatusCode)),
            _ => (
                StatusCodes.Status500InternalServerError,
                "Server error",
                "An unexpected error occurred.",
                ProblemCodes.ServerError),
        };

        if (statusCode >= StatusCodes.Status500InternalServerError)
        {
            LogUnhandledException(logger, exception, httpContext.Request.Method, httpContext.Request.Path);
        }
        else
        {
            LogHandledException(logger, exception.GetType().Name, statusCode, exception.Message);
        }

        httpContext.Response.StatusCode = statusCode;
        return await problemDetailsService.TryWriteAsync(new ProblemDetailsContext
        {
            HttpContext = httpContext,
            Exception = exception,
            ProblemDetails = new ProblemDetails
            {
                Status = statusCode,
                Title = title,
                Detail = detail,
                Extensions = { ["code"] = code },
            },
        });
    }

    [LoggerMessage(Level = LogLevel.Error, Message = "Unhandled exception while processing {Method} {Path}")]
    private static partial void LogUnhandledException(ILogger logger, Exception exception, string method, string path);

    [LoggerMessage(Level = LogLevel.Warning, Message = "{ExceptionType} mapped to HTTP {StatusCode}: {Message}")]
    private static partial void LogHandledException(ILogger logger, string exceptionType, int statusCode, string message);
}
