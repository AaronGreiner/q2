using Q2.Api.Features.Chats;
using Q2.Api.Infrastructure.Errors;

namespace Q2.Api.UnitTests.Chats;

/// <summary>
/// Correcting and taking back one's own message (#25).
/// </summary>
public class ChatMessageTests
{
    private static readonly DateTimeOffset Sent = new(2026, 6, 17, 9, 0, 0, TimeSpan.Zero);

    private readonly Guid _me = Guid.CreateVersion7();
    private readonly Guid _friend = Guid.CreateVersion7();

    private ChatMessage Message(string text = "Bis morgn", Guid? photo = null) =>
        ChatMessage.Create(Guid.CreateVersion7(), Guid.CreateVersion7(), _me, text, photo, Sent);

    [Fact]
    public void TheWordsCanBeCorrectedUntilTheWindowCloses()
    {
        var message = Message();

        Assert.True(message.CanBeEditedBy(_me, Sent + ChatMessage.EditWindow));
        Assert.False(message.CanBeEditedBy(_me, Sent + ChatMessage.EditWindow + TimeSpan.FromSeconds(1)));
        Assert.False(message.CanBeEditedBy(_friend, Sent));

        Assert.True(message.Edit(_me, "  Bis morgen ", Sent.AddMinutes(1)));
        Assert.Equal("Bis morgen", message.Text);
        Assert.Equal(Sent.AddMinutes(1), message.EditedAt);
    }

    [Fact]
    public void TheSameWordsAreNotAnEdit()
    {
        var message = Message();

        Assert.False(message.Edit(_me, "Bis morgn", Sent.AddMinutes(1)));
        Assert.Null(message.EditedAt);
    }

    [Fact]
    public void APhotographWithoutWordsHasNothingToCorrect()
    {
        var message = Message(text: "", photo: Guid.CreateVersion7());

        Assert.False(message.CanBeEditedBy(_me, Sent));
        Assert.Throws<DomainValidationException>(() => message.Edit(_me, "Doch Worte", Sent));
    }

    [Fact]
    public void OnlyItsSenderCanCorrectOrTakeItBack()
    {
        var message = Message();

        Assert.Throws<AccessDeniedException>(() => message.Edit(_friend, "Anders", Sent));
        Assert.Throws<AccessDeniedException>(() => message.Delete(_friend, Sent));
    }

    [Fact]
    public void TakingItBackLeavesThePlaceAndHandsBackThePhotograph()
    {
        var photo = Guid.CreateVersion7();
        var message = Message(text: "Schau mal", photo: photo);
        message.ToggleReaction(Guid.CreateVersion7(), _friend, KudosKind.Fire);

        Assert.Equal([photo], message.Delete(_me, Sent.AddHours(5)));

        Assert.True(message.IsDeleted);
        Assert.Equal(string.Empty, message.Text);
        Assert.Null(message.ImageId);
        Assert.Empty(message.Reactions);
        Assert.False(message.CanBeEditedBy(_me, Sent.AddHours(5)));

        // Twice is once.
        Assert.Empty(message.Delete(_me, Sent.AddHours(6)));
        Assert.Equal(Sent.AddHours(5), message.DeletedAt);
    }
}
