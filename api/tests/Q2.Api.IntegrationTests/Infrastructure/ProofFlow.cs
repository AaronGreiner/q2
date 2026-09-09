using System.Net.Http.Headers;
using Q2.Api.Features.Images;
using Q2.Api.Features.Proofs;

namespace Q2.Api.IntegrationTests.Infrastructure;

/// <summary>
/// Delivering a photograph, in the two steps a real client takes.
/// </summary>
/// <remarks>
/// The bytes go up on their own and only the id is delivered, so a slow upload
/// and a refused window are never the same request. Tests go the same way
/// rather than through a shortcut: "the image has to be yours and has to be a
/// proof" is one of the rules worth exercising on every proof test, not only on
/// the one that names it.
/// </remarks>
public static class ProofFlow
{
    /// <summary>Uploads a photograph and returns its id.</summary>
    public static async Task<Guid> UploadProofImageAsync(this HttpClient client)
    {
        var content = new ByteArrayContent(TestImages.Jpeg(640, 640));
        content.Headers.ContentType = new MediaTypeHeaderValue(ImageFormatReader.Jpeg);

        var response = await client.PostAsync(
            $"/api/images?purpose={ImagePurpose.Proof}",
            content,
            TestContext.Current.CancellationToken);

        response.EnsureSuccessStatusCode();

        return (await response.ReadAsync<UploadedImage>()).Id;
    }

    /// <summary>
    /// Delivers a fresh photograph against a goal. The response is the caller's
    /// to assert on, because "this was refused" is what half of these tests are
    /// about.
    /// </summary>
    public static async Task<HttpResponseMessage> DeliverProofAsync(
        this HttpClient client,
        Guid goalId,
        bool capturedInApp = true) =>
        await client.PostJsonAsync(
            $"/api/goals/{goalId}/proof",
            new { imageId = await client.UploadProofImageAsync(), capturedInApp });

    /// <summary>Delivers one and insists it worked.</summary>
    public static async Task<ProofDocument> DeliverAcceptedProofAsync(this HttpClient client, Guid goalId)
    {
        var response = await client.DeliverProofAsync(goalId);

        Assert.True(
            response.IsSuccessStatusCode,
            $"{(int)response.StatusCode}: {await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken)}");

        return await response.ReadAsync<ProofDocument>();
    }

    private sealed record UploadedImage(Guid Id);
}

/// <summary>A photograph as the API describes it, for tests to assert on.</summary>
public sealed record ProofDocument(
    Guid Id,
    Guid GoalId,
    Guid GoalInstanceId,
    ProofPersonDocument Uploader,
    Guid ImageId,
    ProofStatus Status,
    int Attempt,
    int AttemptsLeft,
    bool CapturedInApp,
    DateTimeOffset CreatedAt,
    DateTimeOffset ExpiresAt,
    VoteSummaryDocument Votes,
    IReadOnlyList<ReactionSummaryDocument> Reactions);

public sealed record ProofPersonDocument(Guid Id, string DisplayName, string Handle);

public sealed record VoteSummaryDocument(
    int ConfirmCount,
    int DoubtCount,
    IReadOnlyList<ProofPersonDocument> ConfirmedBy,
    VoteValue? MyVote,
    bool CanIVote);

public sealed record ReactionSummaryDocument(string Kind, int Count, bool IsMine);
