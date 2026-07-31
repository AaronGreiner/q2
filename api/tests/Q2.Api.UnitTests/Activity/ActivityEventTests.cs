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
/// Where a friend request can go from where it is, and who may move it.
/// </summary>
public class FriendshipTests
{
    private static readonly DateTimeOffset Asked = new(2026, 6, 1, 9, 0, 0, TimeSpan.Zero);

    private static readonly Guid Requester = new("11111111-1111-4111-8111-111111111111");

    private static readonly Guid Addressee = new("22222222-2222-4222-8222-222222222222");

    [Fact]
    public void AcceptingARequestConnectsThem()
    {
        var request = Friendship.Request(Guid.CreateVersion7(), Requester, Addressee, Asked);

        request.Accept(Addressee, Asked.AddHours(2));

        Assert.Equal(FriendshipStatus.Accepted, request.Status);
        Assert.Equal(Asked.AddHours(2), request.RespondedAt);
    }

    [Fact]
    public void OnlyThePersonWhoWasAskedCanAccept()
    {
        var request = Friendship.Request(Guid.CreateVersion7(), Requester, Addressee, Asked);

        // Otherwise sending a request would be the same thing as being granted
        // one, and nobody would ever have to agree.
        Assert.Throws<DomainValidationException>(() => request.Accept(Requester, Asked));
    }

    [Fact]
    public void AnAlreadyAcceptedFriendshipCannotBeAcceptedAgain()
    {
        var friendship = Friendship.Create(
            Guid.CreateVersion7(), Requester, Addressee, FriendshipStatus.Accepted, Asked, Asked);

        Assert.Throws<DomainValidationException>(() => friendship.Accept(Addressee, Asked));
    }

    [Fact]
    public void NobodyCanBefriendThemselves()
    {
        Assert.Throws<DomainValidationException>(
            () => Friendship.Request(Guid.CreateVersion7(), Requester, Requester, Asked));
    }

    [Fact]
    public void TheSameRowReadsAsIncomingForOneSideAndOutgoingForTheOther()
    {
        var request = Friendship.Request(Guid.CreateVersion7(), Requester, Addressee, Asked);

        Assert.True(request.IsIncomingFor(Addressee));
        Assert.False(request.IsIncomingFor(Requester));

        Assert.True(request.IsOutgoingFrom(Requester));
        Assert.False(request.IsOutgoingFrom(Addressee));
    }

    [Fact]
    public void AnAcceptedFriendshipIsNeitherIncomingNorOutgoing()
    {
        var friendship = Friendship.Create(
            Guid.CreateVersion7(), Requester, Addressee, FriendshipStatus.Accepted, Asked, Asked);

        Assert.False(friendship.IsIncomingFor(Addressee));
        Assert.False(friendship.IsOutgoingFrom(Requester));
    }

    [Fact]
    public void TheOtherEndIsWhicheverOneYouAreNot()
    {
        var friendship = Friendship.Request(Guid.CreateVersion7(), Requester, Addressee, Asked);

        Assert.Equal(Addressee, friendship.OtherThan(Requester));
        Assert.Equal(Requester, friendship.OtherThan(Addressee));
        Assert.Throws<InvalidOperationException>(() => friendship.OtherThan(Guid.CreateVersion7()));
    }
}
