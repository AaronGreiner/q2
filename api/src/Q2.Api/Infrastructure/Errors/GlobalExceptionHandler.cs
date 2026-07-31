using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using Q2.Api.Infrastructure.Persistence;
using Sentry;

namespace Q2.Api.Infrastructure.Errors;

/// <summary>
/// Turns every unhandled exception into a standard Problem Details response,
/// and decides what reaches Sentry.
/// </summary>
/// <remarks>
/// This is the only place that reports an exception to Sentry. Sentry's own
/// ASP.NET Core middleware sits <em>outside</em> the exception handler, so an
/// exception handled here never reaches it — one exception, one event.
/// <see cref="ExceptionCapture"/> additionally marks the exception instance, so
/// a retry or a nested handler cannot produce a duplicate.
///
/// Expected failures (<see cref="IExpectedFailure"/>) become 4xx responses and
/// are never reported: a user typing an empty title is not an incident.
/// Unexpected ones become a 500 whose body carries a correlation id but no
/// internal detail.
/// </remarks>
public sealed class GlobalExceptionHandler(
    IProblemDetailsService problemDetailsService,
    IHub sentryHub,
    ILogger<GlobalExceptionHandler> logger) : IExceptionHandler
{
    public const string GenericErrorDetail =
        "An unexpected error occurred. The incident has been recorded; please try again.";

    public async ValueTask<bool> TryHandleAsync(
        HttpContext httpContext,
        Exception exception,
        CancellationToken cancellationToken)
    {
        // The caller went away. Nothing to report, nothing to write.
        if (exception is OperationCanceledException && httpContext.RequestAborted.IsCancellationRequested)
        {
            logger.LogDebug("Request {Method} {Path} was aborted by the client.", httpContext.Request.Method, httpContext.Request.Path);
            return true;
        }

        var problemDetails = exception switch
        {
            DomainValidationException validation => ValidationProblem(validation),
            ResourceNotFoundException notFound => NotFoundProblem(notFound),
            AuthenticationRequiredException unauthenticated => UnauthenticatedProblem(unauthenticated),
            AccessDeniedException denied => ForbiddenProblem(denied.Message),
            DatabaseResetNotAllowedException => ForbiddenProblem(
                "This operation is not permitted in the current environment."),
            BadHttpRequestException badRequest => MalformedRequestProblem(badRequest),
            _ => UnexpectedProblem(exception, httpContext),
        };

        httpContext.Response.StatusCode = problemDetails.Status ?? StatusCodes.Status500InternalServerError;

        return await problemDetailsService.TryWriteAsync(new ProblemDetailsContext
        {
            HttpContext = httpContext,
            Exception = exception,
            ProblemDetails = problemDetails,
        });
    }

    private ProblemDetails ValidationProblem(DomainValidationException exception)
    {
        logger.LogInformation("Request rejected by domain validation: {FieldCount} field(s) invalid.", exception.Errors.Count);

        return new ValidationProblemDetails(exception.Errors.ToDictionary(e => e.Key, e => e.Value))
        {
            Title = "One or more validation errors occurred.",
            Status = StatusCodes.Status400BadRequest,
            Type = "https://datatracker.ietf.org/doc/html/rfc9110#section-15.5.1",
        };
    }

    private ProblemDetails NotFoundProblem(ResourceNotFoundException exception)
    {
        logger.LogInformation("{Resource} was not found.", exception.Resource);

        return new ProblemDetails
        {
            Title = "Not found",
            Detail = exception.Message,
            Status = StatusCodes.Status404NotFound,
            Type = "https://datatracker.ietf.org/doc/html/rfc9110#section-15.5.5",
        };
    }

    /// <remarks>
    /// Logged at information level, like every other expected failure: an
    /// expired session is a normal thing for a phone to have.
    /// </remarks>
    private ProblemDetails UnauthenticatedProblem(AuthenticationRequiredException exception)
    {
        // The reason, never the email address that was tried: a failed sign-in
        // is not a reason to write somebody's address into a log file.
        logger.LogInformation("Authentication failed: {AuthenticationFailure}.", exception.Reason);

        var problem = new ProblemDetails
        {
            Title = "Not signed in",
            Detail = exception.Message,
            Status = StatusCodes.Status401Unauthorized,
            Type = "https://datatracker.ietf.org/doc/html/rfc9110#section-15.5.2",
        };

        // Structure rather than a sentence, so the client can say it in the
        // language it is currently speaking.
        problem.Extensions["reason"] = exception.Reason;

        return problem;
    }

    private ProblemDetails ForbiddenProblem(string detail) => new()
    {
        Title = "Operation not permitted",
        Detail = detail,
        Status = StatusCodes.Status403Forbidden,
        Type = "https://datatracker.ietf.org/doc/html/rfc9110#section-15.5.4",
    };

    private ProblemDetails MalformedRequestProblem(BadHttpRequestException exception)
    {
        logger.LogInformation("Malformed request body rejected.");

        return new ProblemDetails
        {
            Title = "Malformed request",
            // The framework message describes the payload shape, not its content.
            Detail = exception.Message,
            Status = StatusCodes.Status400BadRequest,
            Type = "https://datatracker.ietf.org/doc/html/rfc9110#section-15.5.1",
        };
    }

    private ProblemDetails UnexpectedProblem(Exception exception, HttpContext httpContext)
    {
        // Report first, then log. The order matters: the Sentry logging
        // integration turns LogError into an event too, and whichever call
        // happens first wins — the SDK's duplicate detection drops the second.
        // Capturing first means the id we hand back to the caller is the id of
        // the event that actually exists, and the log line can quote it.
        var eventId = ExceptionCapture.CaptureOnce(sentryHub, exception, logger);

        // The exception object keeps the stack trace in the logs; the message
        // itself never carries user content.
        logger.LogError(
            exception,
            "Unhandled exception for {Method} {Path} (sentryEventId={SentryEventId})",
            httpContext.Request.Method,
            httpContext.Request.Path,
            eventId?.ToString() ?? "none");

        var problem = new ProblemDetails
        {
            Title = "Unexpected error",
            // Never the exception message: it can contain connection strings,
            // file paths or user input.
            Detail = GenericErrorDetail,
            Status = StatusCodes.Status500InternalServerError,
            Type = "https://datatracker.ietf.org/doc/html/rfc9110#section-15.6.1",
        };

        problem.Extensions["traceId"] = httpContext.TraceIdentifier;

        if (eventId is { } id && id != SentryId.Empty)
        {
            // Lets a user quote a reference that maps to the Sentry issue.
            problem.Extensions["errorId"] = id.ToString();
        }

        return problem;
    }
}

/// <summary>
/// Captures an exception at most once, no matter how many layers see it.
/// </summary>
public static class ExceptionCapture
{
    private const string MarkerKey = "q2.sentry.captured";

    /// <summary>
    /// Marks <paramref name="exception"/> as reported and says whether this
    /// caller is the first to do so. Separated from the capture itself so the
    /// deduplication rule can be tested without standing up an SDK hub.
    /// </summary>
    public static bool TryMarkForCapture(Exception exception)
    {
        if (exception.Data.Contains(MarkerKey))
        {
            return false;
        }

        exception.Data[MarkerKey] = true;
        return true;
    }

    public static SentryId? CaptureOnce(IHub hub, Exception exception, ILogger logger)
    {
        if (!TryMarkForCapture(exception))
        {
            logger.LogDebug("Exception already reported to Sentry; skipping duplicate capture.");
            return null;
        }

        try
        {
            return hub.CaptureException(exception);
        }
        catch (Exception captureFailure)
        {
            // Reporting must never be the reason a request fails.
            logger.LogWarning(captureFailure, "Reporting an exception to Sentry failed.");
            return null;
        }
    }

    public static bool WasCaptured(Exception exception) => exception.Data.Contains(MarkerKey);
}
