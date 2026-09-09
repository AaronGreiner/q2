namespace Q2.Api.Features.Goals;

/// <summary>
/// What a pause costs, and what it takes to overturn one.
/// </summary>
/// <remarks>
/// The way out that q2 did not have. Until now every elapsed deadline was
/// punished the same, whether somebody could not be bothered or was in bed with
/// the flu — and an accountability app that cannot tell those apart is one
/// people leave.
///
/// A pause must not become a skip button, so three costs keep it honest, in
/// this order of effectiveness:
///
/// 1. **Scarcity.** <see cref="MaxPerMonth"/> per goal and calendar month. This
///    is the real brake, and it works silently: nobody has to accuse a friend
///    of anything for it to bite.
/// 2. **A reason.** Compulsory, and every invited friend reads it. Having to
///    write an excuse down costs more than thinking one.
/// 3. **An objection.** For the cases that get through anyway.
///
/// The objection comes last deliberately. Doubting somebody's illness is
/// socially expensive, and a mechanism nobody wants to use carries little
/// weight. The scarcity does the larger part of the work.
///
/// Ported from the source project's <c>domain/pause.ts</c> and pure for the
/// same reason <see cref="Proofs.ProofVoting"/> is: the maintenance job decides
/// it while nobody is looking, and it has to be testable without a database
/// (section 7e of the migration plan).
/// </remarks>
public static class PauseRules
{
    /// <summary>Longest a single pause may run, in whole local days.</summary>
    public const int MaxDays = 7;

    /// <summary>Shortest one. A pause of no days is not a pause.</summary>
    public const int MinDays = 1;

    /// <summary>
    /// Pauses allowed per goal and calendar month.
    /// </summary>
    /// <remarks>
    /// The most important number in this file: while pauses are scarce, nobody
    /// has to appoint themselves the invigilator.
    /// </remarks>
    public const int MaxPerMonth = 2;

    /// <summary>Shortest reason that counts as one — a sentence, not a character.</summary>
    public const int MinReasonLength = 10;

    /// <summary>Longest reason stored. It is read on a banner, not in an essay.</summary>
    public const int MaxReasonLength = 280;

    /// <summary>Objections it takes before the share means anything.</summary>
    public const int MinVetoes = 2;

    /// <summary>The share of the invited that has to be exceeded.</summary>
    public const double VetoThreshold = 1.0 / 3.0;

    /// <summary>
    /// How many objections bring a pause down.
    /// </summary>
    /// <remarks>
    /// Deliberately the same construction as the vote on a photograph: at least
    /// two, and more than a third of the people invited. One person can
    /// therefore never overturn a pause alone, exactly as they can never sink a
    /// photograph alone.
    ///
    /// The consequence with a single invited friend is that the pause cannot be
    /// challenged at all. That is the same gap the vote has there, accepted for
    /// the same reason: a verdict between two people with no second opinion
    /// would be worse than none.
    ///
    /// Unlike the vote, the share counts against <em>everybody invited</em>
    /// rather than against the objections cast. There is no counter-objection —
    /// silence means consent.
    /// </remarks>
    public static int VetoesRequired(int participantCount) =>
        Math.Max(MinVetoes, (int)Math.Floor(participantCount * VetoThreshold) + 1);

    /// <summary>True when the objections raised are enough to lift the pause.</summary>
    public static bool IsOverturned(int vetoCount, int participantCount) =>
        vetoCount >= VetoesRequired(participantCount);

    /// <summary>True when a reason passes as one.</summary>
    public static bool IsReasonValid(string? reason) =>
        reason is not null && reason.Trim().Length >= MinReasonLength;
}
