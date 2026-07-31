using Q2.Api.Features.Goals;
using Q2.Api.Features.People;
using Q2.Api.Infrastructure.Persistence.Seeding;

namespace Q2.Api.UnitTests.Persistence;

/// <summary>
/// The properties every seed profile has to have.
/// </summary>
/// <remarks>
/// These are not tests of "what is in the database" — that is the seed class's
/// own documentation. They are tests of the promises the seeding contract makes
/// to everything downstream: determinism, no id collisions between profiles,
/// exactly one signed-in person, and a graph with no dangling references.
///
/// Determinism is the one that matters most. Every other test in the repository
/// assumes it.
/// </remarks>
public class SeedDataTests
{
    private static readonly SeedContext Context =
        new(new DateTimeOffset(2026, 6, 15, 9, 30, 0, TimeSpan.Zero));

    public static TheoryData<ISeedDataSource> AllSources =>
    [
        new DevelopmentSeed(),
        new ManualTestingSeed(),
        new AutomatedTestSeed(),
        new E2ESeed(),
    ];

    [Theory]
    [MemberData(nameof(AllSources))]
    public void TheSameContextProducesTheSameWorldEveryTime(ISeedDataSource source)
    {
        var first = source.Create(Context);
        var second = source.Create(Context);

        Assert.Equal(
            first.People.Select(p => (p.Id, p.DisplayName, p.Handle, p.KudosReceived)),
            second.People.Select(p => (p.Id, p.DisplayName, p.Handle, p.KudosReceived)));

        Assert.Equal(
            first.Goals.Select(g => (g.Id, g.Title, g.CompletedSteps, g.TotalSteps, g.TargetDate)),
            second.Goals.Select(g => (g.Id, g.Title, g.CompletedSteps, g.TotalSteps, g.TargetDate)));

        Assert.Equal(
            first.Tasks.Select(t => (t.Id, t.Title, t.Rhythm, t.CompletedOn)),
            second.Tasks.Select(t => (t.Id, t.Title, t.Rhythm, t.CompletedOn)));

        Assert.Equal(
            first.Conversations.SelectMany(c => c.Messages).Select(m => (m.Id, m.Text, m.SentAt)),
            second.Conversations.SelectMany(c => c.Messages).Select(m => (m.Id, m.Text, m.SentAt)));
    }

    [Theory]
    [MemberData(nameof(AllSources))]
    public void ExactlyOnePersonIsTheSignedInOne(ISeedDataSource source)
    {
        // CurrentPerson refuses to guess, so a seed that got this wrong would
        // take every read down with it.
        Assert.Single(source.Create(Context).People, person => person.IsCurrentUser);
    }

    [Theory]
    [MemberData(nameof(AllSources))]
    public void EveryIdentifierIsUnique(ISeedDataSource source)
    {
        var data = source.Create(Context);

        var ids = data.People.Select(p => p.Id)
            .Concat(data.People.SelectMany(p => p.CheckIns).Select(c => c.Id))
            .Concat(data.People.SelectMany(p => p.Badges).Select(b => b.Id))
            .Concat(data.Friendships.Select(f => f.Id))
            .Concat(data.Goals.Select(g => g.Id))
            .Concat(data.Goals.SelectMany(g => g.Participants).Select(p => p.Id))
            .Concat(data.Goals.SelectMany(g => g.Contributions).Select(c => c.Id))
            .Concat(data.Tasks.Select(t => t.Id))
            .Concat(data.Activity.Select(a => a.Id))
            .Concat(data.Conversations.Select(c => c.Id))
            .Concat(data.Conversations.SelectMany(c => c.Messages).Select(m => m.Id))
            .Concat(data.Settings.Select(s => s.Id))
            .ToList();

        Assert.Equal(ids.Count, ids.Distinct().Count());
    }

    [Fact]
    public void TwoProfilesNeverShareAnIdentifier()
    {
        // Each profile owns a GUID prefix, so a row's origin is obvious and a
        // ManualTesting id can never collide with an E2E one.
        ISeedDataSource[] sources =
            [new DevelopmentSeed(), new ManualTestingSeed(), new AutomatedTestSeed(), new E2ESeed()];

        var everything = sources
            .SelectMany(source => source.Create(Context).Goals.Select(goal => goal.Id))
            .ToList();

        Assert.Equal(everything.Count, everything.Distinct().Count());
    }

    [Theory]
    [MemberData(nameof(AllSources))]
    public void EveryReferenceInTheGraphPointsAtSomethingInIt(ISeedDataSource source)
    {
        var data = source.Create(Context);
        var people = data.People.Select(p => p.Id).ToHashSet();
        var goals = data.Goals.Select(g => g.Id).ToHashSet();

        Assert.All(data.Friendships, link => Assert.Contains(link.PersonId, people));
        Assert.All(data.Goals.SelectMany(g => g.Participants), p => Assert.Contains(p.PersonId, people));
        Assert.All(data.Activity, a => Assert.Contains(a.ActorPersonId, people));
        Assert.All(data.Settings, s => Assert.Contains(s.PersonId, people));

        Assert.All(
            data.Tasks.Where(task => task.GoalId is not null),
            task => Assert.Contains(task.GoalId!.Value, goals));

        Assert.All(
            data.Conversations.Where(c => c.GoalId is not null),
            c => Assert.Contains(c.GoalId!.Value, goals));

        Assert.All(
            data.Conversations.SelectMany(c => c.Participants),
            p => Assert.Contains(p.PersonId, people));

        Assert.All(
            data.Conversations.SelectMany(c => c.Messages),
            m => Assert.Contains(m.SenderPersonId, people));
    }

    [Theory]
    [MemberData(nameof(AllSources))]
    public void NobodyIsAFriendOfThemselves(ISeedDataSource source)
    {
        var data = source.Create(Context);
        var me = data.People.Single(person => person.IsCurrentUser);

        Assert.DoesNotContain(data.Friendships, link => link.PersonId == me.Id);
    }

    [Theory]
    [MemberData(nameof(AllSources))]
    public void EveryGoalUsesAnIconTheFrontendCanDraw(ISeedDataSource source)
    {
        Assert.All(source.Create(Context).Goals, goal => Assert.Contains(goal.Icon, GoalIcons.All));
    }

    [Theory]
    [MemberData(nameof(AllSources))]
    public void EveryAvatarColourComesFromThePalette(ISeedDataSource source)
    {
        Assert.All(source.Create(Context).People, person => Assert.Contains(person.AvatarColor, AvatarColors.All));
    }

    [Theory]
    [MemberData(nameof(AllSources))]
    public void DatesAreRelativeToTheSeedInstantRatherThanFixed(ISeedDataSource source)
    {
        // A goal that is meant to be upcoming has to stay upcoming as the
        // calendar moves on, or every "is this overdue?" test would rot.
        var later = new SeedContext(Context.SeededAt.AddDays(30));

        var before = source.Create(Context).Goals.Select(g => g.TargetDate).ToList();
        var after = source.Create(later).Goals.Select(g => g.TargetDate).ToList();

        Assert.All(
            before.Zip(after),
            pair => Assert.Equal(pair.First is null, pair.Second is null));

        Assert.Contains(before.Zip(after), pair => pair.First != pair.Second);
    }

    [Fact]
    public void TheAutomatedTestSeedHasNothingScheduledOnAWeekendOnly()
    {
        // Otherwise "how many tasks are on today's list" would depend on the day
        // the suite runs, and a test that passes on Tuesday and fails on
        // Saturday is worse than no test.
        var tasks = new AutomatedTestSeed().Create(Context).Tasks;

        Assert.All(
            tasks,
            task => Assert.True(
                task.Rhythm is GoalRhythm.Daily or GoalRhythm.Once,
                $"'{task.Title}' has rhythm {task.Rhythm}, which makes today's list depend on the weekday."));
    }

    [Fact]
    public void TheE2ESeedHasStableIdsPlaywrightCanNavigateTo()
    {
        var goals = new E2ESeed().Create(Context).Goals;

        Assert.Equal(new Guid("e2e00000-0000-4000-8000-000000000001"), goals[0].Id);
    }

    [Fact]
    public void TheE2ESeedTitlesAreUniqueAndNotSubstringsOfOneAnother()
    {
        // Playwright matches on text; two titles where one contains the other
        // make a locator ambiguous the moment a test creates its own rows.
        var titles = new E2ESeed().Create(Context).Goals.Select(goal => goal.Title).ToList();

        Assert.Equal(titles.Count, titles.Distinct().Count());

        Assert.All(
            titles,
            title => Assert.DoesNotContain(titles, other => other != title && other.Contains(title)));
    }
}
