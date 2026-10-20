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
    public ChallengePrompt[] Prompts { get; init; } = [];

    /// <summary>How many days ahead to write rows for, including today.</summary>
    public int HorizonDays { get; init; } = DefaultHorizonDays;

    /// <summary>
    /// Whether every prompt can be published in both languages. Checked when
    /// the host starts, so a prompt with one half missing stops a deployment
    /// rather than a queue pass at night (#105).
    /// </summary>
    public bool IsComplete() =>
        Prompts.All(prompt => Challenge.IsUsablePrompt(prompt.German) && Challenge.IsUsablePrompt(prompt.English));
}

/// <summary>
/// One task, in both languages the interface speaks.
/// </summary>
/// <remarks>
/// Written by whoever keeps the queue, never by a machine: a prompt is short
/// and has a tone, and a translation that misses it reads worse than none. So
/// both halves are required, and each reader is shown the one in their own
/// language (#105).
/// </remarks>
public sealed class ChallengePrompt
{
    public string German { get; init; } = string.Empty;

    public string English { get; init; } = string.Empty;
}

/// <summary>A challenge that should exist but does not yet.</summary>
/// <param name="PublishedAt">The moment it appears, the same for everybody (#136).</param>
/// <param name="MomentEndsAt">The end of the moment; contributions after it are late.</param>
public sealed record PlannedChallenge(
    DateOnly Day,
    ChallengePrompt Prompt,
    DateTimeOffset PublishedAt,
    DateTimeOffset ExpiresAt,
    DateTimeOffset MomentEndsAt);

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
    /// The earliest a challenge appears, on the deployment's clock (#136): late
    /// enough that nobody is woken by it, and the whole window lies outside the
    /// default quiet hours (22:00–07:00), so the defaults let its push through.
    /// </summary>
    public static readonly TimeOnly EarliestMoment = new(10, 0);

    /// <summary>How long the window it may appear in is, in minutes: until 20:00.</summary>
    public const int MomentWindowMinutes = 10 * 60;

    /// <summary>
    /// How long "the moment" lasts once it has appeared. Two hours: long
    /// enough to answer from a meeting or a train, short enough to be now.
    /// </summary>
    public static readonly TimeSpan MomentLength = TimeSpan.FromHours(2);

    /// <summary>
    /// The days from <paramref name="today"/> onwards that still need a row.
    /// </summary>
    /// <param name="calendar">
    /// The deployment's calendar, not a person's: a challenge is one row for
    /// everybody and turns over at one moment. See <see cref="Challenge"/>.
    /// </param>
    public static IReadOnlyList<PlannedChallenge> Plan(
        IReadOnlyList<ChallengePrompt> prompts,
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

            // It appears at its moment and runs to local midnight, the end
            // exclusive: a day that is 23 or 25 hours long still ends exactly
            // where the next one begins.
            var appears = calendar.At(day, MomentFor(day))
                ?? calendar.StartOfDay(day).Add(MomentFor(day).ToTimeSpan());

            planned.Add(new PlannedChallenge(
                day,
                PromptFor(prompts, day),
                appears,
                calendar.StartOfDay(day.AddDays(1)),
                appears + MomentLength));
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
    public static ChallengePrompt PromptFor(IReadOnlyList<ChallengePrompt> prompts, DateOnly day) =>
        prompts[day.DayNumber % prompts.Count];

    /// <summary>
    /// When a day's challenge appears: a moment between 10:00 and 20:00 that
    /// nobody can plan for, the same for everybody (#136).
    /// </summary>
    /// <remarks>
    /// Drawn from the date and nothing else, never from a random number
    /// generator, so the queue stays a pure function: planning a day twice — or
    /// after a week down — gives it the same moment. The mixing is a fixed
    /// integer hash (SplitMix64's finaliser), not <c>GetHashCode</c>, which
    /// .NET randomises per process. To the minute: nobody needs seconds.
    /// </remarks>
    public static TimeOnly MomentFor(DateOnly day)
    {
        var mixed = unchecked((ulong)day.DayNumber + 0x9E3779B97F4A7C15UL);
        mixed = unchecked((mixed ^ (mixed >> 30)) * 0xBF58476D1CE4E5B9UL);
        mixed = unchecked((mixed ^ (mixed >> 27)) * 0x94D049BB133111EBUL);
        mixed ^= mixed >> 31;

        return EarliestMoment.AddMinutes((int)(mixed % MomentWindowMinutes));
    }
}
