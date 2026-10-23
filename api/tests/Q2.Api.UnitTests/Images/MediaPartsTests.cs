using Q2.Api.Features.Chats;
using Q2.Api.Features.Images;
using Q2.Api.Infrastructure.Errors;

namespace Q2.Api.UnitTests.Images;

/// <summary>
/// The parts of a picture from q2's camera, wherever it is stored (#152). A
/// proof's are covered in <c>ProofPartsTests</c>; what is particular here is
/// the route: where it is recorded a clip or a second camera has to be live,
/// and where nothing records it — a chat photograph, a logged use — only the
/// shape of the parts is checked.
/// </summary>
public sealed class MediaPartsTests
{
    private static readonly Guid Main = Guid.CreateVersion7();

    private static MediaParts TwoClips() =>
        new(MediaKind.Video, Guid.CreateVersion7(), Guid.CreateVersion7(), MediaKind.Video, Guid.CreateVersion7());

    [Fact]
    public void AClipOrASecondCameraIsOnlyEverLiveWhereTheRouteIsRecorded()
    {
        var error = Assert.Throws<DomainValidationException>(() => TwoClips().Validate(Main, live: false));
        Assert.Contains("Source", error.Errors.Keys);

        TwoClips().Validate(Main, live: true);
    }

    [Fact]
    public void WhereNothingRecordsTheRouteOnlyTheShapeIsChecked()
    {
        TwoClips().Validate(Main, live: null);

        Assert.Throws<DomainValidationException>(() => new MediaParts(MediaKind.Video).Validate(Main, live: null));
    }

    [Fact]
    public void NamesEveryPartBesideTheMainOne()
    {
        var parts = TwoClips();

        Assert.Equal([parts.PosterImageId!.Value, parts.InsetImageId!.Value, parts.InsetPosterImageId!.Value], parts.OtherIds);
        Assert.Empty(MediaParts.Photo.OtherIds);
        Assert.Null(MediaParts.Photo.StoredInsetMedia);
        Assert.Equal(MediaKind.Video, parts.StoredInsetMedia);
    }

    /// <summary>A photograph sent in a chat may be two clips, and taking it back hands every part over to be removed.</summary>
    [Fact]
    public void AChatMessageCarriesItsPartsAndHandsThemAllBackWhenTakenBack()
    {
        var sender = Guid.CreateVersion7();
        var parts = TwoClips();
        var conversation = Conversation.CreateDirect(Guid.CreateVersion7(), DateTimeOffset.UnixEpoch);

        var message = conversation.AddMessage(Guid.CreateVersion7(), sender, string.Empty, DateTimeOffset.UnixEpoch, Main, parts);

        Assert.Equal(MediaKind.Video, message.Media);
        Assert.Equal(MediaKind.Video, message.InsetMedia);
        Assert.Equal([Main, .. parts.OtherIds], message.PictureIds);

        var removed = message.Delete(sender, DateTimeOffset.UnixEpoch);

        Assert.Equal([Main, .. parts.OtherIds], removed);
        Assert.Empty(message.PictureIds);
        Assert.Null(message.InsetImageId);
    }

    [Fact]
    public void AChatMessagesPartsHaveToFitTogether()
    {
        var sender = Guid.CreateVersion7();
        var conversation = Conversation.CreateDirect(Guid.CreateVersion7(), DateTimeOffset.UnixEpoch);

        Assert.Throws<DomainValidationException>(() =>
            conversation.AddMessage(Guid.CreateVersion7(), sender, string.Empty, DateTimeOffset.UnixEpoch, Main, new MediaParts(MediaKind.Video)));
    }
}
