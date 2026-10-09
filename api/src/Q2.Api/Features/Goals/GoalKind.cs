namespace Q2.Api.Features.Goals;

/// <summary>Whether a goal is something to do or something to stop doing.</summary>
/// <remarks>
/// Both are a <see cref="Goal"/>, with the same schedule, windows, friends,
/// conversation, pause and archive (docs/adr/0035-dos-and-donts.md). What
/// differs is how a window is decided: a Do is kept by photographs friends
/// believe, a Don't by a limit that held (docs/adr/0037-donts.md). Stored as
/// text; goals from before Don'ts are Dos.
/// </remarks>
public enum GoalKind
{
    Do,
    Dont,
}

/// <summary>What a Don't's friends get to judge, chosen when it is made.</summary>
/// <remarks>
/// An abstinence cannot be photographed, so each mode is a different answer to
/// "how would anybody know": a live photograph at a moment the owner cannot
/// plan for, a photograph at the end of the period (the screen-time screenshot),
/// or nothing but the owner's word and the relapse rule.
/// </remarks>
public enum DontProofMode
{
    /// <summary>Random live checks during the window (#63).</summary>
    SpotCheck,

    /// <summary>One photograph before the window ends, voted on like any proof.</summary>
    ProofAtEnd,

    /// <summary>The limit and the relapse rule, and nothing to vote on.</summary>
    HonestOnly,
}
