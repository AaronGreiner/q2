using System.Net;
using Q2.Api.Features.Moderation;
using Q2.Api.Infrastructure.Persistence.Seeding;
using Q2.Api.IntegrationTests.Infrastructure;

namespace Q2.Api.IntegrationTests.Api;

/// <summary>
/// Asking for something to be looked at.
/// </summary>
/// <remarks>
/// Two things are worth a pipeline. The first is that a report cannot see
/// further than the person filing it can — otherwise the endpoint would be a
/// way to ask whether a given id exists, which every other read here answers
/// with 404. The second is that it actually reaches somebody: the row is not
/// the delivery, and a test that only checked the row would be checking exactly
/// the half that privacy.md calls worse than nothing.
/// </remarks>
[Trait("Category", "Integration")]
public class ModerationEndpointTests(Q2ApiFactory factory) : ApiTestBase(factory)
{
    private sealed record ReceiptDocument(Guid Id, DateTimeOffset CreatedAt);

    private static Task<HttpResponseMessage> ReportAsync(
        HttpClient client,
        ReportTargetKind kind,
        Guid targetId,
        ReportReason reason = ReportReason.Inappropriate,
        string? note = null) =>
        client.PostJsonAsync("/api/reports", new { targetKind = kind, targetId, reason, note });

    [Fact]
    public async Task ReportingAPersonIsReceivedAndReachesSomebody()
    {
        var response = await ReportAsync(Client, ReportTargetKind.Person, AutomatedTestSeed.FriendPersonId);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        var receipt = await response.ReadAsync<ReceiptDocument>();
        Assert.NotEqual(Guid.Empty, receipt.Id);

        // The row is not the delivery. This is the assertion that the bell
        // actually rings — see ObservabilityReportSink.
        var events = await Factory.RecordedEventsAsync();

        Assert.Contains(events, recorded =>
            recorded.Tags.TryGetValue("q2.report.kind", out var kind)
            && kind == nameof(ReportTargetKind.Person));
    }

    /// <summary>
    /// The alert carries ids, a kind and a reason. Never the note, which is
    /// free text somebody typed, and never the reporter.
    /// </summary>
    [Fact]
    public async Task TheAlertCarriesNoNoteAndNoReporter()
    {
        await ReportAsync(
            Client,
            ReportTargetKind.Person,
            AutomatedTestSeed.FriendPersonId,
            ReportReason.Harassment,
            "Er schreibt mir jeden Abend, obwohl ich nicht antworte.");

        var alert = Assert.Single(
            await Factory.RecordedEventsAsync(),
            recorded => recorded.Tags.ContainsKey("q2.report.kind"));

        Assert.Equal(nameof(ReportReason.Harassment), alert.Tags["q2.report.reason"]);

        // The note is free text somebody typed, and the reporter is nobody's
        // business. Neither is anywhere in the payload.
        Assert.False(alert.Contains("jeden Abend"));
        Assert.False(alert.Contains(AutomatedTestSeed.CurrentPersonId.ToString()));

        // What is there is enough to act on: the target, by id.
        Assert.True(alert.Contains(AutomatedTestSeed.FriendPersonId.ToString()));
    }

    [Fact]
    public async Task ReportingTheSameThingTwiceIsOneReport()
    {
        var first = await (await ReportAsync(Client, ReportTargetKind.Person, AutomatedTestSeed.FriendPersonId))
            .ReadAsync<ReceiptDocument>();

        var second = await (await ReportAsync(Client, ReportTargetKind.Person, AutomatedTestSeed.FriendPersonId))
            .ReadAsync<ReceiptDocument>();

        // Not an error for a person who tapped twice, and not a second alert
        // for a complaint that has not changed.
        Assert.Equal(first.Id, second.Id);
    }

    /// <summary>
    /// The rule that stops this becoming an existence oracle: you may only
    /// report what you can already see.
    /// </summary>
    [Fact]
    public async Task SomethingYouCannotSeeCannotBeReported()
    {
        var invented = await ReportAsync(Client, ReportTargetKind.Proof, Guid.CreateVersion7());
        Assert.Equal(HttpStatusCode.NotFound, invented.StatusCode);

        // A real photograph, on a goal this person is not on.
        var proof = await Client.DeliverAcceptedProofAsync(AutomatedTestSeed.ActiveGoalId);
        var stranger = await ClientForAsync(AutomatedTestSeed.RequesterEmail);

        var refused = await ReportAsync(stranger, ReportTargetKind.Proof, proof.Id);
        Assert.Equal(HttpStatusCode.NotFound, refused.StatusCode);
    }

    [Fact]
    public async Task AFriendOnTheGoalMayReportThePhotograph()
    {
        var proof = await Client.DeliverAcceptedProofAsync(AutomatedTestSeed.ActiveGoalId);
        var friend = await ClientForAsync(AutomatedTestSeed.FriendEmail);

        var response = await ReportAsync(friend, ReportTargetKind.Proof, proof.Id, ReportReason.Faked);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
    }

    [Fact]
    public async Task ABlockedPersonCannotBeReported()
    {
        (await Client.PostJsonAsync($"/api/blocks/{AutomatedTestSeed.FriendPersonId}", new { }))
            .EnsureSuccessStatusCode();

        // Hidden is hidden: reporting must not be the one route that still
        // confirms somebody is there.
        var response = await ReportAsync(Client, ReportTargetKind.Person, AutomatedTestSeed.FriendPersonId);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task ARequestWithoutATargetOrAReasonIsRefused()
    {
        var noTarget = await Client.PostJsonAsync("/api/reports", new { reason = ReportReason.Spam });
        var noReason = await Client.PostJsonAsync(
            "/api/reports",
            new { targetKind = ReportTargetKind.Person, targetId = AutomatedTestSeed.FriendPersonId });

        Assert.Equal(HttpStatusCode.BadRequest, noTarget.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, noReason.StatusCode);
    }

    [Fact]
    public async Task ANoteHasALimit()
    {
        var response = await ReportAsync(
            Client,
            ReportTargetKind.Person,
            AutomatedTestSeed.FriendPersonId,
            ReportReason.Other,
            new string('x', Report.MaxNoteLength + 1));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task YouCannotReportYourself()
    {
        var response = await ReportAsync(Client, ReportTargetKind.Person, AutomatedTestSeed.CurrentPersonId);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task ReportingNeedsASession()
    {
        var response = await AnonymousClient.PostJsonAsync(
            "/api/reports",
            new { targetKind = ReportTargetKind.Person, targetId = AutomatedTestSeed.FriendPersonId, reason = ReportReason.Spam });

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }
}
