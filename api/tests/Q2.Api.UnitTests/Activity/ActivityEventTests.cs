using Q2.Api.Features.Activity;
using Q2.Api.Features.People;
using Q2.Api.Infrastructure.Errors;

namespace Q2.Api.UnitTests.Activity;

/// <summary>
/// Feed entries and the kudos on them.
/// </summary>
public class ActivityEventTests
{
    private static readonly DateTimeOffset Now = new(2026, 6, 17, 9, 0, 0, TimeSpan.Zero);

    private readonly Guid _actor = Guid.CreateVersion7();
    private readonly Guid _viewer = Guid.CreateVersion7();

    private ActivityEvent Build(int kudos = 0) => ActivityEvent.Create(
        Guid.CreateVersion7(), _actor, ActivityKind.TaskCompleted, "Joggen 5 km", null, kudos, Now);

    [Fact]
    public void AnEntryThatNeedsASubjectIsRejectedWithoutOne()
    {
        var error = Assert.Throws<DomainValidationException>(() => ActivityEvent.Create(
            Guid.CreateVersion7(), _actor, ActivityKind.TaskCompleted, null, null, 0, Now));

        Assert.Contains(nameof(ActivityEvent.Subject), error.Errors.Keys);
    }

    [Fact]
    public void AStreakEntryNeedsANumberOfDays()
    {
        var error = Assert.Throws<DomainValidationException>(() => ActivityEvent.Create(
            Guid.CreateVersion7(), _actor, ActivityKind.StreakReached, null, null, 0, Now));

        Assert.Contains(nameof(ActivityEvent.Amount), error.Errors.Keys);
    }

    [Fact]
    public void GivingKudosAddsOneToTheSeededTotal()
    {
        // The stored count starts above the rows on purpose — a seeded entry is
        // meant to look like it was cheered by more people than q2 models.
        var activity = Build(kudos: 8);

        Assert.True(activity.GiveKudos(Guid.CreateVersion7(), _viewer));

        Assert.Equal(9, activity.KudosCount);
        Assert.True(activity.HasKudosFrom(_viewer));
    }

    [Fact]
    public void TheSamePersonCannotGiveKudosTwice()
    {
        var activity = Build(kudos: 8);
        activity.GiveKudos(Guid.CreateVersion7(), _viewer);

        Assert.False(activity.GiveKudos(Guid.CreateVersion7(), _viewer));
        Assert.Equal(9, activity.KudosCount);
    }

    [Fact]
    public void TakingKudosBackUndoesExactlyThatOne()
    {
        var activity = Build(kudos: 8);
        activity.GiveKudos(Guid.CreateVersion7(), _viewer);

        Assert.True(activity.WithdrawKudos(_viewer));

        Assert.Equal(8, activity.KudosCount);
        Assert.False(activity.HasKudosFrom(_viewer));
    }

    [Fact]
    public void ThereIsNothingToTakeBackIfNoneWereGiven()
    {
        var activity = Build(kudos: 8);

        Assert.False(activity.WithdrawKudos(_viewer));
        Assert.Equal(8, activity.KudosCount);
    }

    [Fact]
    public void TheSentenceIsNotStoredOnlyItsParts()
    {
        // The app ships in German and English; a stored sentence could only
        // ever be one of them.
        var activity = ActivityEvent.Create(
            Guid.CreateVersion7(), _actor, ActivityKind.StreakReached, null, 7, 3, Now);

        Assert.Null(activity.Subject);
        Assert.Equal(7, activity.Amount);
        Assert.Equal(ActivityKind.StreakReached, activity.Kind);
    }
}

/// <summary>
/// Where a friend request can go from where it is.
/// </summary>
public class FriendshipTests
{
    [Fact]
    public void OnlyAPendingRequestCanBeAccepted()
    {
        var suggestion = Friendship.Create(
            Guid.CreateVersion7(), Guid.CreateVersion7(), FriendshipStatus.Suggested);

        // Accepting a suggestion would make somebody your friend without them
        // ever having asked.
        Assert.Throws<DomainValidationException>(suggestion.Accept);
    }

    [Fact]
    public void AcceptingARequestConnectsThem()
    {
        var request = Friendship.Create(
            Guid.CreateVersion7(), Guid.CreateVersion7(), FriendshipStatus.Requested, mutualFriends: 3);

        request.Accept();

        Assert.Equal(FriendshipStatus.Accepted, request.Status);
    }

    [Fact]
    public void OnlyASuggestionCanBeTurnedIntoARequest()
    {
        var accepted = Friendship.Create(
            Guid.CreateVersion7(), Guid.CreateVersion7(), FriendshipStatus.Accepted);

        Assert.Throws<DomainValidationException>(accepted.Invite);
    }

    [Fact]
    public void AskingASuggestedPersonSendsAnInvitation()
    {
        var suggestion = Friendship.Create(
            Guid.CreateVersion7(), Guid.CreateVersion7(), FriendshipStatus.Suggested);

        suggestion.Invite();

        Assert.Equal(FriendshipStatus.Invited, suggestion.Status);
    }
}
