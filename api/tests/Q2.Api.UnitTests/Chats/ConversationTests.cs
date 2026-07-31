using Q2.Api.Features.Chats;
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
        var conversation = Conversation.CreateDirect(Guid.CreateVersion7(), goalId: null, Start);
        conversation.AddParticipant(Guid.CreateVersion7(), _me);
        conversation.AddParticipant(Guid.CreateVersion7(), _friend);
        return conversation;
    }

    [Fact]
    public void AGroupNeedsAName()
    {
        var error = Assert.Throws<DomainValidationException>(
            () => Conversation.CreateGroup(Guid.CreateVersion7(), "  ", "🌅", null, Start));

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

        Assert.True(message.ToggleReaction(Guid.CreateVersion7(), _me, MessageReactions.Clap));
        Assert.Single(message.Reactions);

        Assert.False(message.ToggleReaction(Guid.CreateVersion7(), _me, MessageReactions.Clap));
        Assert.Empty(message.Reactions);
    }

    [Fact]
    public void AnUnknownReactionIsRejected()
    {
        var conversation = BuildDirect();
        var message = conversation.AddMessage(Guid.CreateVersion7(), _friend, "Stark!", Start);

        Assert.Throws<DomainValidationException>(
            () => message.ToggleReaction(Guid.CreateVersion7(), _me, "🍕"));
    }
}
