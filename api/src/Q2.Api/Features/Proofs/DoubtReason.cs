namespace Q2.Api.Features.Proofs;

/// <summary>
/// What a doubter can say was missing from a photograph (#111).
/// </summary>
/// <remarks>
/// A few fixed answers and no free text, on purpose. The owner of a refused
/// photograph gets one more try and should not have to guess what would
/// convince; but a field anybody may type into anonymously is a field for
/// insults, and a fixed answer cannot be one. Optional: doubting without a
/// reason stays one tap. Never shown with a name, like the doubt itself
/// (docs/adr/0018-proof-and-vote.md, docs/adr/0055-why-a-photograph-was-doubted.md).
/// Stored as text.
/// </remarks>
public enum DoubtReason
{
    /// <summary>It does not show what the goal promised.</summary>
    NotWhatWasPromised,

    /// <summary>It looks like an old picture or a screenshot, not today's.</summary>
    OldPicture,

    /// <summary>Nothing on it can be made out.</summary>
    Unrecognisable,
}
