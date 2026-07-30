using Microsoft.EntityFrameworkCore;
using Q2.Api.Infrastructure;
using Q2.Api.Infrastructure.Errors;
using Q2.Api.Infrastructure.Observability;
using Q2.Api.Infrastructure.Persistence;

namespace Q2.Api.Features.Diagnostics;

/// <summary>
/// Health check plus a deliberate failure trigger.
/// </summary>
/// <remarks>
/// <c>/health</c> is always available and returns no internal detail.
///
/// <c>/api/diagnostics/*</c> is only mapped outside Staging and Production.
/// It exists because "is Sentry actually wired up?" has to be answerable
/// without waiting for a real incident, and a documented local trigger beats
/// everyone inventing their own. It is not a production endpoint, and it does
/// not become one by accident: the routes simply do not exist there.
/// </remarks>
public static class DiagnosticsEndpoints
{
    public static IEndpointRouteBuilder MapHealthEndpoint(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapGet("/health", async (Q2DbContext database, CancellationToken cancellationToken) =>
            {
                var canConnect = await database.Database.CanConnectAsync(cancellationToken);

                return canConnect
                    ? Results.Ok(new HealthResponse("healthy", "q2-api"))
                    : Results.Json(new HealthResponse("unhealthy", "q2-api"), statusCode: StatusCodes.Status503ServiceUnavailable);
            })
            .WithName("Health")
            .WithTags("Diagnostics")
            .WithSummary("Liveness and database connectivity.")
            .Produces<HealthResponse>()
            .ExcludeFromDescription();

        return endpoints;
    }

    public static IEndpointRouteBuilder MapDiagnosticsEndpoints(this IEndpointRouteBuilder endpoints, IHostEnvironment environment)
    {
        if (ApplicationEnvironments.Protected.Contains(environment.EnvironmentName))
        {
            return endpoints;
        }

        // Kept out of the OpenAPI document: these routes only exist in some
        // environments, and a committed contract that changes depending on
        // where it was exported from is worse than no contract at all.
        var group = endpoints.MapGroup("/api/diagnostics")
            .WithTags("Diagnostics")
            .ExcludeFromDescription();

        group.MapGet("/sentry", (SentrySettings settings) => Results.Ok(new SentryStatusResponse(
                settings.Enabled && !string.IsNullOrWhiteSpace(settings.Dsn),
                settings.Environment,
                settings.Release,
                settings.UseRecordingTransport)))
            .WithName("SentryStatus")
            .WithSummary("Reports whether Sentry would send an event, without revealing the DSN.")
            .Produces<SentryStatusResponse>();

        group.MapGet("/boom", () =>
            {
                throw new DiagnosticsTestException(
                    "Synthetic diagnostics error from the q2 API. This is not a real incident.");
            })
            .WithName("TriggerDiagnosticsError")
            .WithSummary("Throws on purpose, to verify error handling and Sentry capture end to end.")
            .ProducesProblem(StatusCodes.Status500InternalServerError);

        return endpoints;
    }
}

/// <param name="Status">"healthy" or "unhealthy".</param>
public sealed record HealthResponse(string Status, string Service);

/// <param name="Enabled">True when a DSN is configured and sending is on.</param>
/// <param name="RecordingTransport">True when events stay in-process.</param>
public sealed record SentryStatusResponse(bool Enabled, string Environment, string Release, bool RecordingTransport);
