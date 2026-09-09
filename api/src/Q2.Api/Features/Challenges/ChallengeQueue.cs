using Q2.Api.Infrastructure.Time;

namespace Q2.Api.Features.Challenges;

/// <summary>Where the prompts come from, bound from <c>Q2:Challenges</c>.</summary>
/// <remarks>
/// Configuration <em>is</em> the editorial desk here, and that is the whole
/// point of the design. The alternative was an admin screen, which q2 has no
/// place for — there are no roles
/// ([0011](../../../../docs/adr/0011-authentication-with-identity.md)) — or a
/// job that invents a prompt every night, which is a daily editorial shift by
/// another name. Writing a week of prompts into a settings file and letting the
/// queue publish them one per day is the version somebody can actually keep up
/// with.
///
/// An empty list turns the feature off cleanly: no rows are written, the room
/// reports that nothing is running, and no screen breaks.
/// </remarks>
public sealed class ChallengeOptions
{
    public const string SectionName = "Q2:Challenges";

    /// <summary>
    /// How far ahead rows are written.
    /// </summary>
    /// <remarks>
    /// A week, because that is the unit a person writing prompts thinks in, and
    /// because it is long enough that a host down for a few days still comes
    /// back to a queue with something in it.
    /// </remarks>
    public const int DefaultHorizonDays = 7;

    /// <summary>
    /// The prompts, in rotation. Empty means no daily challenge at all.
    /// </summary>
    public string[] Prompts { get; init; } = [];

    /// <summary>How many days ahead to write rows for, including today.</summary>
    public int HorizonDays { get; init; } = DefaultHorizonDays;
}

/// <summary>A challenge that should exist but does not yet.</summary>
public sealed record PlannedChallenge(
    DateOnly Day,
    string Prompt,
    DateTimeOffset PublishedAt,
    DateTimeOffset ExpiresAt);

/// <summary>
/// Turns a list of prompts into the rows that should be queued.
/// </summary>
/// <remarks>
/// A pure function, for the same reason <see cref="Q2.Api.Features.Goals.PauseRules"/>
/// and <c>ProofVoting</c> are: it decides something while nobody is looking, so
/// it has to be testable without a host and without a database.
///
/// Two properties make it safe to run on a timer:
///
/// - **Idempotent.** It is told which days already have a row and proposes only
///   the others, so two passes are one pass.
/// - **Stable.** Which prompt a day gets is a function of the date alone, so
///   re-planning never rewrites a day, and adding a prompt to the list shifts
///   only days that have not been written yet.
///
/// It deliberately never fills in the past. A challenge nobody could have taken
/// part in is not worth a row, and back-filling one would put a prompt in the
/// archive that nobody was ever offered.
/// </remarks>
public static class ChallengeQueue
{
    /// <summary>
    /// The days from <paramref name="today"/> onwards that still need a row.
    /// </summary>
    /// <param name="calendar">
    /// The deployment's calendar, not a person's: a challenge is one row for
    /// everybody and turns over at one moment. See <see cref="Challenge"/>.
    /// </param>
    public static IReadOnlyList<PlannedChallenge> Plan(
        IReadOnlyList<string> prompts,
        IReadOnlyCollection<DateOnly> alreadyQueued,
        DateOnly today,
        int horizonDays,
        LocalCalendar calendar)
    {
        ArgumentNullException.ThrowIfNull(prompts);
        ArgumentNullException.ThrowIfNull(alreadyQueued);
        ArgumentNullException.ThrowIfNull(calendar);

        if (prompts.Count == 0 || horizonDays <= 0)
        {
            return [];
        }

        var planned = new List<PlannedChallenge>();

        for (var offset = 0; offset < horizonDays; offset++)
        {
            var day = today.AddDays(offset);

            if (alreadyQueued.Contains(day))
            {
                continue;
            }

            planned.Add(new PlannedChallenge(
                day,
                PromptFor(prompts, day),

                // Local midnight to local midnight, and the end is exclusive:
                // a day that is 23 or 25 hours long still ends exactly where
                // the next one begins, and no instant falls between two
                // challenges.
                calendar.StartOfDay(day),
                calendar.StartOfDay(day.AddDays(1))));
        }

        return planned;
    }

    /// <summary>
    /// Which prompt a given day gets.
    /// </summary>
    /// <remarks>
    /// By the date rather than by "the next one in the list", so the answer does
    /// not depend on what has already been written. That is what makes running
    /// this twice — or after a week of downtime — produce the same queue.
    /// </remarks>
    public static string PromptFor(IReadOnlyList<string> prompts, DateOnly day) =>
        prompts[day.DayNumber % prompts.Count];
}
