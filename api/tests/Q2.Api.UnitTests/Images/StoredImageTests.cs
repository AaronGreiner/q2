using Q2.Api.Features.Images;
using Q2.Api.Infrastructure.Errors;

namespace Q2.Api.UnitTests.Images;

/// <summary>
/// The limits an upload has to be inside, checked where they live rather than
/// through the endpoint — these are the rules, and they should fail here first.
/// </summary>
public sealed class StoredImageTests
{
    private static readonly Guid Owner = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly Guid Id = Guid.Parse("22222222-2222-2222-2222-222222222222");
    private static readonly DateTimeOffset Now = new(2026, 6, 15, 9, 0, 0, TimeSpan.Zero);

    [Fact]
    public void KeepsWhatTheBytesSaidRatherThanWhatWasClaimed()
    {
        var image = StoredImage.Create(
            Id,
            Owner,
            ImagePurpose.Avatar,
            new ImageContent(ImageFormatReader.Jpeg, 512, 384, 40_000),
            Now);

        Assert.Equal(ImageFormatReader.Jpeg, image.ContentType);
        Assert.Equal(512, image.Width);
        Assert.Equal(384, image.Height);
        Assert.Equal(40_000, image.ByteSize);
        Assert.Equal(Owner, image.OwnerPersonId);
        Assert.Equal(Now, image.CreatedAt);
    }

    [Fact]
    public void RejectsAnImageWithNobodyItBelongsTo()
    {
        var error = Assert.Throws<DomainValidationException>(() => StoredImage.Create(
            Id,
            Guid.Empty,
            ImagePurpose.Avatar,
            Valid(),
            Now));

        Assert.Contains("OwnerPersonId", error.Errors.Keys);
    }

    [Fact]
    public void RejectsAnUploadOverTheByteLimit()
    {
        var error = Assert.Throws<DomainValidationException>(() => StoredImage.Create(
            Id,
            Owner,
            ImagePurpose.Proof,
            Valid() with { ByteSize = StoredImage.MaxBytes + 1 },
            Now));

        Assert.Contains("ByteSize", error.Errors.Keys);
    }

    [Fact]
    public void AcceptsAnUploadExactlyAtTheByteLimit()
    {
        // The boundary belongs in a test because an off-by-one here is a
        // rejection somebody would experience as "it just does not work".
        var image = StoredImage.Create(
            Id,
            Owner,
            ImagePurpose.Proof,
            Valid() with { ByteSize = StoredImage.MaxBytes },
            Now);

        Assert.Equal(StoredImage.MaxBytes, image.ByteSize);
    }

    [Fact]
    public void RejectsAnEmptyUpload()
    {
        Assert.Throws<DomainValidationException>(() => StoredImage.Create(
            Id,
            Owner,
            ImagePurpose.Avatar,
            Valid() with { ByteSize = 0 },
            Now));
    }

    [Theory]
    [InlineData(StoredImage.MaxDimension + 1, 100)]
    [InlineData(100, StoredImage.MaxDimension + 1)]
    public void RejectsAnImageLargerThanAnythingItWillEverBeDrawnAt(int width, int height)
    {
        var error = Assert.Throws<DomainValidationException>(() => StoredImage.Create(
            Id,
            Owner,
            ImagePurpose.Proof,
            Valid() with { Width = width, Height = height },
            Now));

        Assert.Contains("Width", error.Errors.Keys);
    }

    [Theory]
    [InlineData(StoredImage.MinDimension - 1, 100)]
    [InlineData(100, StoredImage.MinDimension - 1)]
    public void RejectsSomethingTooSmallToBeAPhotograph(int width, int height)
    {
        // The floor is what stops a million one-pixel rows from slipping under
        // the byte quota.
        Assert.Throws<DomainValidationException>(() => StoredImage.Create(
            Id,
            Owner,
            ImagePurpose.Proof,
            Valid() with { Width = width, Height = height },
            Now));
    }

    [Fact]
    public void LetsAnybodyReadAnAvatarAndNobodyElseReadAProof()
    {
        var stranger = Guid.Parse("33333333-3333-3333-3333-333333333333");

        var avatar = StoredImage.Create(Id, Owner, ImagePurpose.Avatar, Valid(), Now);
        var proof = StoredImage.Create(Id, Owner, ImagePurpose.Proof, Valid(), Now);

        Assert.True(ImageService.CanRead(avatar, stranger));
        Assert.True(ImageService.CanRead(avatar, Owner));

        // A proof's wider audience is its goal's, which only CanReadAsync can ask.
        Assert.False(ImageService.CanRead(proof, stranger));
        Assert.True(ImageService.CanRead(proof, Owner));
    }

    [Fact]
    public void LeavesAChatPhotographToItsConversation()
    {
        var stranger = Guid.Parse("33333333-3333-3333-3333-333333333333");
        var photo = StoredImage.Create(Id, Owner, ImagePurpose.ChatPhoto, Valid(), Now);

        // Who else is in the conversation is a query, answered by CanReadAsync;
        // on the picture alone it is nobody's but the sender's.
        Assert.False(ImageService.CanRead(photo, stranger));
        Assert.True(ImageService.CanRead(photo, Owner));
    }

    private static ImageContent Valid() => new(ImageFormatReader.Jpeg, 512, 512, 10_000);

    [Fact]
    public void KeepsAClipsLengthAndKnowsItIsOne()
    {
        var clip = StoredImage.Create(Id, Owner, ImagePurpose.Proof, Clip(durationMilliseconds: 9_800), Now);

        Assert.True(clip.IsVideo);
        Assert.Equal(9_800, clip.DurationMilliseconds);
    }

    /// <summary>A clip is a proof's, and nothing else may move (#124).</summary>
    [Theory]
    [InlineData(ImagePurpose.Avatar)]
    [InlineData(ImagePurpose.ChatPhoto)]
    [InlineData(ImagePurpose.ChallengeEntry)]
    public void OnlyAProofMayBeAClip(ImagePurpose purpose)
    {
        var error = Assert.Throws<DomainValidationException>(() =>
            StoredImage.Create(Id, Owner, purpose, Clip(), Now));

        Assert.Contains(nameof(StoredImage.Purpose), error.Errors.Keys);
    }

    [Fact]
    public void RefusesAClipLongerThanTheRecorderStopsAt()
    {
        var error = Assert.Throws<DomainValidationException>(() =>
            StoredImage.Create(Id, Owner, ImagePurpose.Proof, Clip(durationMilliseconds: StoredImage.MaxVideoMilliseconds + 1), Now));

        Assert.Contains(nameof(StoredImage.DurationMilliseconds), error.Errors.Keys);
    }

    [Fact]
    public void AClipMayRunAMomentPastTheTenSecondsTheAppStopsAt()
    {
        var clip = StoredImage.Create(Id, Owner, ImagePurpose.Proof, Clip(durationMilliseconds: (StoredImage.MaxVideoSeconds * 1000) + 400), Now);

        Assert.Equal(10_400, clip.DurationMilliseconds);
    }

    [Fact]
    public void AClipHasItsOwnByteLimitAndAPictureKeepsItsOwn()
    {
        // Above a picture's limit and inside a clip's.
        var bytes = StoredImage.MaxBytes + 1;

        Assert.True(StoredImage.Create(Id, Owner, ImagePurpose.Proof, Clip(byteSize: bytes), Now).IsVideo);
        Assert.Throws<DomainValidationException>(() => StoredImage.Create(
            Id, Owner, ImagePurpose.Proof, new ImageContent(ImageFormatReader.Jpeg, 640, 480, bytes), Now));
        Assert.Throws<DomainValidationException>(() => StoredImage.Create(
            Id, Owner, ImagePurpose.Proof, Clip(byteSize: StoredImage.MaxVideoBytes + 1), Now));
    }

    private static ImageContent Clip(int durationMilliseconds = 5_000, int byteSize = 2_000_000) =>
        new(ImageFormatReader.Mp4, 1280, 720, byteSize, durationMilliseconds);
}
