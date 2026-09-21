using Q2.Api.Features.Chats;
using Q2.Api.Features.Goals;
using Q2.Api.Infrastructure.Errors;

namespace Q2.Api.UnitTests.Chats;

/// <summary>
/// What a conversation lets happen, and how "unread" is worked out.
/// </summary>
public class ConversationTests
{
    private static readonly DateTimeOffset Start = new(2026, 6, 17, 9, 0, 0, TimeSpan.Zero);

    private readonly Guid _me = Guid.CreateVersion7();
    private readonly Guid _friend = Guid.CreateVersion7();
    private readonly Guid _stranger = Guid.CreateVersion7();

    private Conversation BuildDirect()
    {
        var conversation = Conversation.CreateDirect(Guid.CreateVersion7(), Start);
        conversation.AddParticipant(Guid.CreateVersion7(), _me);
        conversation.AddParticipant(Guid.CreateVersion7(), _friend);
        return conversation;
    }

    [Fact]
    public void AGroupNeedsAName()
    {
        var error = Assert.Throws<DomainValidationException>(
            () => Conversation.CreateGroup(Guid.CreateVersion7(), "  ", "🌅", Start));

        Assert.Contains(nameof(Conversation.Title), error.Errors.Keys);
    }

    [Fact]
    public void ADirectConversationHasNoNameOfItsOwn()
    {
        // Naming it would store somebody's name a second time and let it go
        // stale; the name is derived from the other participant on read.
        Assert.Null(BuildDirect().Title);
    }

    [Fact]
    public void AGoalsConversationIsCalledWhateverItsGoalIsCalled()
    {
        var goalId = Guid.CreateVersion7();

        var conversation = Conversation.CreateForGoal(Guid.CreateVersion7(), goalId, Start);

        Assert.Equal(ConversationKind.Goal, conversation.Kind);
        Assert.Equal(goalId, conversation.GoalId);

        // Named on read from the goal, like a direct chat from the other person.
        Assert.Null(conversation.Title);
        Assert.Null(conversation.Icon);
    }

    [Fact]
    public void AGoalsConversationNeedsItsGoal()
    {
        var error = Assert.Throws<DomainValidationException>(
            () => Conversation.CreateForGoal(Guid.CreateVersion7(), Guid.Empty, Start));

        Assert.Contains(nameof(Conversation.GoalId), error.Errors.Keys);
    }

    [Fact]
    public void AGoalsConversationCannotBeLeft()
    {
        var conversation = Conversation.CreateForGoal(Guid.CreateVersion7(), Guid.CreateVersion7(), Start);
        conversation.AddParticipant(Guid.CreateVersion7(), _me);
        conversation.AddParticipant(Guid.CreateVersion7(), _friend);

        // Leaving would take a vote off the goal without anybody deciding to.
        Assert.Throws<DomainValidationException>(() => conversation.RemoveParticipant(_friend));
        Assert.True(conversation.Includes(_friend));
    }

    [Fact]
    public void AGoalsConversationIsItsOwnerAndEverybodyInvited()
    {
        var goal = Goal.Create(
            Guid.CreateVersion7(),
            _me,
            "Jeden Tag lesen",
            null,
            null,
            GoalSchedule.EveryNDays(1),
            isGroup: false,
            reminderAt: null,
            targetDate: null,
            Start);
        goal.AddParticipant(Guid.CreateVersion7(), _friend);

        var conversation = GoalConversations.Open(goal, Guid.CreateVersion7(), Guid.CreateVersion7, Start);

        Assert.Equal(goal.Id, conversation.GoalId);
        Assert.Equal([_me, _friend], conversation.Participants.Select(participant => participant.PersonId));
        Assert.DoesNotContain(_stranger, conversation.Participants.Select(participant => participant.PersonId));

        // The owner opened it; everybody else has it waiting for them.
        Assert.Equal(Start, conversation.Participants.Single(participant => participant.PersonId == _me).LastReadAt);
        Assert.Null(conversation.Participants.Single(participant => participant.PersonId == _friend).LastReadAt);
    }

    [Fact]
    public void SomebodyOutsideTheConversationCannotWriteInIt()
    {
        var conversation = BuildDirect();

        var error = Assert.Throws<DomainValidationException>(
            () => conversation.AddMessage(Guid.CreateVersion7(), _stranger, "Hallo", Start));

        Assert.Contains(nameof(Conversation.Messages), error.Errors.Keys);
    }

    [Fact]
    public void AnEmptyMessageIsRejected()
    {
        var conversation = BuildDirect();

        Assert.Throws<DomainValidationException>(
            () => conversation.AddMessage(Guid.CreateVersion7(), _me, "   ", Start));
    }

    [Fact]
    public void EverythingFromTheOtherPersonIsUnreadUntilTheThreadIsOpened()
    {
        var conversation = BuildDirect();
        conversation.AddMessage(Guid.CreateVersion7(), _friend, "Erste", Start);
        conversation.AddMessage(Guid.CreateVersion7(), _friend, "Zweite", Start.AddMinutes(1));

        Assert.Equal(2, conversation.UnreadCountFor(_me));
    }

    [Fact]
    public void YourOwnMessagesAreNeverUnread()
    {
        var conversation = BuildDirect();
        conversation.AddMessage(Guid.CreateVersion7(), _me, "Meine", Start);

        Assert.Equal(0, conversation.UnreadCountFor(_me));
    }

    [Fact]
    public void ReadingTheThreadClearsWhatCameBeforeIt()
    {
        var conversation = BuildDirect();
        conversation.AddMessage(Guid.CreateVersion7(), _friend, "Alt", Start);
        conversation.MarkRead(_me, Start.AddMinutes(1));
        conversation.AddMessage(Guid.CreateVersion7(), _friend, "Neu", Start.AddMinutes(2));

        Assert.Equal(1, conversation.UnreadCountFor(_me));
    }

    [Fact]
    public void TheReadMarkerNeverMovesBackwards()
    {
        var conversation = BuildDirect();
        conversation.AddMessage(Guid.CreateVersion7(), _friend, "Alt", Start);

        conversation.MarkRead(_me, Start.AddMinutes(5));
        conversation.MarkRead(_me, Start.AddMinutes(1));

        Assert.Equal(0, conversation.UnreadCountFor(_me));
    }

    [Fact]
    public void SomebodyOutsideTheConversationHasNothingUnreadInIt()
    {
        var conversation = BuildDirect();
        conversation.AddMessage(Guid.CreateVersion7(), _friend, "Hallo", Start);

        Assert.Equal(0, conversation.UnreadCountFor(_stranger));
    }

    [Fact]
    public void AReactionIsAddedAndTakenBackByTheSameTap()
    {
        var conversation = BuildDirect();
        var message = conversation.AddMessage(Guid.CreateVersion7(), _friend, "Stark!", Start);

        Assert.True(message.ToggleReaction(Guid.CreateVersion7(), _me, KudosKind.Applause));
        Assert.Single(message.Reactions);

        Assert.False(message.ToggleReaction(Guid.CreateVersion7(), _me, KudosKind.Applause));
        Assert.Empty(message.Reactions);
    }

    /// <summary>
    /// An enum is not a guarantee: a request body deserialises into whatever
    /// integer it was given, so a value nobody defined still reaches the model.
    /// </summary>
    [Fact]
    public void AnUnknownReactionIsRejected()
    {
        var conversation = BuildDirect();
        var message = conversation.AddMessage(Guid.CreateVersion7(), _friend, "Stark!", Start);

        Assert.Throws<DomainValidationException>(
            () => message.ToggleReaction(Guid.CreateVersion7(), _me, (KudosKind)42));
    }
}
