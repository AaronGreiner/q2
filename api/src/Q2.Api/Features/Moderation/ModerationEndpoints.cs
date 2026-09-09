using Microsoft.AspNetCore.Http.HttpResults;

namespace Q2.Api.Features.Moderation;

/// <summary>
/// HTTP surface for reporting something.
/// </summary>
/// <remarks>
/// One endpoint, and there is deliberately no way to read a report back. A
/// person cannot list their own reports, cannot see a status and cannot be told
/// the outcome — telling somebody what happened to their report means telling
/// them what happened to another person's account.
/// </remarks>
public static class ModerationEndpoints
{
    public static IEndpointRouteBuilder MapModerationEndpoints(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapPost("/api/reports", FileReport)
            .RequireAuthorization()
            .WithTags("Moderation")
            .WithName("FileReport")
            .WithSummary("Asks for a person, a photograph or a challenge contribution to be looked at.")
            .Produces<ReportReceiptResponse>(StatusCodes.Status201Created)
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status404NotFound);

        return endpoints;
    }

    private static async Task<Created<ReportReceiptResponse>> FileReport(
        ReportService reports,
        CreateReportRequest request,
        CancellationToken cancellationToken)
    {
        var receipt = await reports.FileAsync(request, cancellationToken);

        // No Location: there is nothing at the other end of it, on purpose.
        return TypedResults.Created((string?)null, receipt);
    }
}
