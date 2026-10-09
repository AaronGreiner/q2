namespace Q2.Api.Features.Proofs;

/// <summary>Which way a photograph came into the app.</summary>
/// <remarks>
/// Persisted as text, like every other enum here.
///
/// **This says which route the app used, not that the picture is fresh.** The
/// server receives bytes and cannot tell a photograph taken a second ago from
/// one taken last year, so <see cref="Live"/> means "the app's camera was used",
/// and a modified client could claim it falsely. Friends are told which route
/// it was so they can weigh it; they are never told it is proven
/// (docs/adr/0036-live-or-screenshot.md).
/// </remarks>
public enum ProofSource
{
    /// <summary>
    /// Delivered before the route was recorded, or by a client that did not
    /// say.
    /// </summary>
    /// <remarks>
    /// Not <see cref="Live"/> by default. The old <c>CapturedInApp</c> flag was
    /// sent as true for every proof, gallery picks included, so it says nothing,
    /// and calling those pictures live would be a claim nobody can back.
    /// </remarks>
    Unknown,

    /// <summary>Taken with the app's camera while delivering it.</summary>
    Live,

    /// <summary>
    /// Chosen from the phone's pictures — typically a screenshot of screen
    /// time, a finished course or an app's streak.
    /// </summary>
    Screenshot,
}
