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
            first.Goals.Select(g => (g.Id, g.Title, g.Schedule.Kind, g.Schedule.WeekdayList, g.TargetDate)),
            second.Goals.Select(g => (g.Id, g.Title, g.Schedule.Kind, g.Schedule.WeekdayList, g.TargetDate)));

        Assert.Equal(
            first.Goals.SelectMany(g => g.Instances).Select(i => (i.Id, i.StartsOn, i.DueOn, i.Status)),
            second.Goals.SelectMany(g => g.Instances).Select(i => (i.Id, i.StartsOn, i.DueOn, i.Status)));

        Assert.Equal(
            first.Conversations.SelectMany(c => c.Messages).Select(m => (m.Id, m.Text, m.SentAt)),
            second.Conversations.SelectMany(c => c.Messages).Select(m => (m.Id, m.Text, m.SentAt)));
    }

    [Theory]
    [MemberData(nameof(AllSources))]
    public void EverySeededPersonCanSignIn(ISeedDataSource source)
    {
        // The property that replaced "exactly one person is the signed-in one".
        // A person without an account cannot be logged in as, and being able to
        // be *any* of them is what makes it possible to check a friendship or a
        // group chat from both ends.
        var data = source.Create(Context);

        Assert.NotEmpty(data.People);
        Assert.Equal(data.People.Count, data.Accounts.Count);

        Assert.All(data.Accounts, account =>
        {
            Assert.Contains(data.People, person => person.Id == account.PersonId);
            Assert.False(string.IsNullOrWhiteSpace(account.PasswordHash));
            Assert.Equal(account.Email?.ToUpperInvariant(), account.NormalizedEmail);
            Assert.Equal(account.UserName, account.Email);
        });

        // One account per person, and one address per account: FindByEmail
        // would otherwise be a coin toss.
        Assert.Equal(data.Accounts.Count, data.Accounts.Select(a => a.PersonId).Distinct().Count());
        Assert.Equal(data.Accounts.Count, data.Accounts.Select(a => a.NormalizedEmail).Distinct().Count());
    }

    [Theory]
    [MemberData(nameof(AllSources))]
    public void EveryAddressIsInTheReservedSeedDomain(ISeedDataSource source)
    {
        // No seeded address may be one that could reach a real inbox
        // (docs/privacy.md). RFC 2606 reserves ".example" for exactly this.
        Assert.All(
            source.Create(Context).Accounts,
            account => Assert.EndsWith("@" + SeedAccounts.EmailDomain, account.Email!, StringComparison.Ordinal));
    }

    [Theory]
    [MemberData(nameof(AllSources))]
    public void EveryIdentifierIsUnique(ISeedDataSource source)
    {
        var data = source.Create(Context);

        var ids = data.People.Select(p => p.Id)
            .Concat(data.Accounts.Select(a => a.Id))
            .Concat(data.People.SelectMany(p => p.CheckIns).Select(c => c.Id))
            .Concat(data.People.SelectMany(p => p.Badges).Select(b => b.Id))
            .Concat(data.Friendships.Select(f => f.Id))
            .Concat(data.Goals.Select(g => g.Id))
            .Concat(data.Goals.SelectMany(g => g.Participants).Select(p => p.Id))
            .Concat(data.Goals.SelectMany(g => g.Instances).Select(i => i.Id))
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

        Assert.All(data.Friendships, link => Assert.Contains(link.RequesterId, people));
        Assert.All(data.Friendships, link => Assert.Contains(link.AddresseeId, people));
        Assert.All(data.Accounts, account => Assert.Contains(account.PersonId, people));
        Assert.All(data.Goals.SelectMany(g => g.Participants), p => Assert.Contains(p.PersonId, people));
        Assert.All(data.Activity, a => Assert.Contains(a.ActorPersonId, people));
        Assert.All(data.Settings, s => Assert.Contains(s.PersonId, people));

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
        Assert.DoesNotContain(
            source.Create(Context).Friendships,
            link => link.RequesterId == link.AddresseeId);
    }

    [Theory]
    [MemberData(nameof(AllSources))]
    public void NoTwoPeopleAreConnectedTwice(ISeedDataSource source)
    {
        // One row per pair, in either direction. Two would mean the unique
        // index fails on insert, and a seed that cannot be inserted is a seed
        // that takes every test with it.
        var pairs = source.Create(Context).Friendships
            .Select(link => link.RequesterId.CompareTo(link.AddresseeId) < 0
                ? (link.RequesterId, link.AddresseeId)
                : (link.AddresseeId, link.RequesterId))
            .ToList();

        Assert.Equal(pairs.Count, pairs.Distinct().Count());
    }

    [Theory]
    [MemberData(nameof(AllSources))]
    public void EveryGoalBelongsToSomebodyInTheWorld(ISeedDataSource source)
    {
        var data = source.Create(Context);
        var people = data.People.Select(p => p.Id).ToHashSet();

        Assert.All(data.Goals, goal => Assert.Contains(goal.OwnerPersonId, people));
    }

    /// <summary>
    /// A goal has at most one window to deliver into, and its past is behind it
    /// rather than around it.
    /// </summary>
    /// <remarks>
    /// "At most", not "exactly": a goal whose window has already been delivered
    /// today has none open, and the next one appears when the day rolls over.
    /// Opening it early would let somebody deliver tomorrow's proof tonight.
    /// </remarks>
    [Theory]
    [MemberData(nameof(AllSources))]
    public void EveryRunningGoalHasAtMostOneOpenWindow(ISeedDataSource source)
    {
        var running = source.Create(Context).Goals
            .Where(goal => goal.Status == GoalStatus.Active)
            .ToList();

        Assert.All(
            running,
            goal => Assert.True(
                goal.Instances.Count(instance => instance.Status == GoalInstanceStatus.Open) <= 1,
                $"'{goal.Title}' has more than one open window."));

        Assert.All(
            running.Where(goal => goal.CurrentInstance is null),
            goal => Assert.True(
                goal.LatestInstance?.Status == GoalInstanceStatus.Done
                && goal.LatestInstance.DueOn >= Context.Today,
                $"'{goal.Title}' has nothing open and nothing delivered for today either."));

        Assert.All(
            running.SelectMany(goal => goal.Instances)
                .Where(instance => instance.Status != GoalInstanceStatus.Open),
            instance => Assert.True(
                instance.DueOn < Context.Today || instance.ConfirmedProofs == instance.RequiredProofs,
                "A resolved window has to be in the past unless it was delivered."));
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
        // Otherwise "how much of today is done" would depend on the day the
        // suite runs, and a test that passes on Tuesday and fails on Saturday
        // is worse than no test.
        var goals = new AutomatedTestSeed().Create(Context).Goals;

        Assert.All(
            goals,
            goal => Assert.True(
                goal.Schedule.Kind is not ScheduleKind.Weekdays,
                $"'{goal.Title}' is due on named weekdays, which makes today's list depend on the day."));
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
