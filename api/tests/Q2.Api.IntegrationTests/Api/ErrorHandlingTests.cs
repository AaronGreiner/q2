using System.Net;
using Q2.Api.Infrastructure.Errors;
using Q2.Api.IntegrationTests.Infrastructure;

namespace Q2.Api.IntegrationTests.Api;

/// <summary>
/// What a caller sees when something goes wrong — and what they must never see.
/// </summary>
[Trait("Category", "Integration")]
public class ErrorHandlingTests(Q2ApiFactory factory) : ApiTestBase(factory)
{
    [Fact]
    public async Task AnUnexpectedErrorBecomesA500ProblemDetails()
    {
        var response = await Client.GetAsync("/api/diagnostics/boom", TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);

        var problem = await response.ReadAsync<ProblemDocument>();
        Assert.Equal(500, problem.Status);
        Assert.Equal(GlobalExceptionHandler.GenericErrorDetail, problem.Detail);
    }

    [Fact]
    public async Task AnUnexpectedErrorLeaksNoInternalDetail()
    {
        var response = await Client.GetAsync("/api/diagnostics/boom", TestContext.Current.CancellationToken);
        var body = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);

        // No exception type, no message, no stack frame, no file path.
        Assert.DoesNotContain("DiagnosticsTestException", body);
        Assert.DoesNotContain("Synthetic diagnostics error", body);
        Assert.DoesNotContain("at Q2.Api", body);
        Assert.DoesNotContain(".cs:line", body);
    }

    [Fact]
    public async Task AnUnexpectedErrorCarriesCorrelationIdsTheUserCanQuote()
    {
        var response = await Client.GetAsync("/api/diagnostics/boom", TestContext.Current.CancellationToken);
        var problem = await response.ReadAsync<ProblemDocument>();

        Assert.False(string.IsNullOrWhiteSpace(problem.TraceId));

        // errorId is the Sentry event id, so a support request maps onto an issue.
        Assert.False(string.IsNullOrWhiteSpace(problem.ErrorId));

        var recorded = Factory.SentryEvents.Events.Single();
        Assert.Equal(recorded.EventId, problem.ErrorId);
    }

    [Fact]
    public async Task AValidationFailureIsNotTreatedAsAServerError()
    {
        var response = await Client.PostJsonAsync("/api/goals", new { title = "" });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

        // Expected failures never become Sentry issues.
        Assert.Empty(Factory.SentryEvents.Events);
    }

    [Fact]
    public async Task AMissingRouteReturnsAProblemDetailsRatherThanAnEmptyBody()
    {
        var response = await Client.GetAsync("/api/does-not-exist", TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
    }

    [Fact]
    public async Task TheApiKeepsServingAfterAnUnexpectedError()
    {
        await Client.GetAsync("/api/diagnostics/boom", TestContext.Current.CancellationToken);

        var afterwards = await Client.GetAsync("/api/goals", TestContext.Current.CancellationToken);

        afterwards.EnsureSuccessStatusCode();
    }

    private sealed record ProblemDocument(
        string? Title,
        int? Status,
        string? Detail,
        string? TraceId,
        string? ErrorId);
}
