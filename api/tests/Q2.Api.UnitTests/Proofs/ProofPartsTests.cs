using Q2.Api.Features.Images;
using Q2.Api.Features.Proofs;
using Q2.Api.Infrastructure.Errors;

namespace Q2.Api.UnitTests.Proofs;

/// <summary>
/// A proof that is a clip, or that comes from both cameras, and the rules
/// about how its parts fit together (#124, #125).
/// </summary>
public sealed class MediaPartsTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 23, 12, 0, 0, TimeSpan.Zero);

    private static readonly Guid Main = Guid.CreateVersion7();

    private static ProofPhoto Deliver(MediaParts parts, ProofSource source = ProofSource.Live) =>
        ProofPhoto.Create(Guid.CreateVersion7(), Guid.CreateVersion7(), Guid.CreateVersion7(), Main, 1, source, Now, parts);

    [Fact]
    public void AProofLeftWithoutPartsIsOnePhotograph()
    {
        var proof = ProofPhoto.Create(Guid.CreateVersion7(), Guid.CreateVersion7(), Guid.CreateVersion7(), Main, 1, ProofSource.Screenshot, Now);

        Assert.Equal(MediaKind.Photo, proof.Media);
        Assert.Null(proof.PosterImageId);
        Assert.Null(proof.InsetImageId);
        Assert.Null(proof.InsetMedia);
        Assert.Null(proof.InsetPosterImageId);
    }

    [Fact]
    public void AClipKeepsItsStillAndTheFrontPicture()
    {
        var poster = Guid.CreateVersion7();
        var inset = Guid.CreateVersion7();

        var proof = Deliver(new MediaParts(MediaKind.Video, poster, inset));

        Assert.Equal(MediaKind.Video, proof.Media);
        Assert.Equal(poster, proof.PosterImageId);
        Assert.Equal(inset, proof.InsetImageId);
        Assert.Equal(MediaKind.Photo, proof.InsetMedia);
        Assert.Null(proof.InsetPosterImageId);
    }

    /// <summary>Both cameras filmed at once: two clips, each with its own still (#151).</summary>
    [Fact]
    public void TwoClipsKeepBothStills()
    {
        var poster = Guid.CreateVersion7();
        var front = Guid.CreateVersion7();
        var frontPoster = Guid.CreateVersion7();

        var proof = Deliver(new MediaParts(MediaKind.Video, poster, front, MediaKind.Video, frontPoster));

        Assert.Equal(MediaKind.Video, proof.Media);
        Assert.Equal(front, proof.InsetImageId);
        Assert.Equal(MediaKind.Video, proof.InsetMedia);
        Assert.Equal(frontPoster, proof.InsetPosterImageId);
    }

    /// <summary>A front clip plays in step with the rear one, so there has to be a rear clip.</summary>
    [Fact]
    public void AFrontClipIsOnlyEverBesideAClip()
    {
        var error = Assert.Throws<DomainValidationException>(() =>
            Deliver(new MediaParts(MediaKind.Photo, InsetImageId: Guid.CreateVersion7(), InsetMedia: MediaKind.Video, InsetPosterImageId: Guid.CreateVersion7())));

        Assert.Contains(nameof(MediaParts.InsetMedia), error.Errors.Keys);
    }

    [Fact]
    public void AFrontClipNeedsAStill()
    {
        var error = Assert.Throws<DomainValidationException>(() =>
            Deliver(new MediaParts(MediaKind.Video, Guid.CreateVersion7(), Guid.CreateVersion7(), MediaKind.Video)));

        Assert.Contains(nameof(MediaParts.InsetPosterImageId), error.Errors.Keys);
    }

    [Fact]
    public void OnlyAFrontClipHasAStill()
    {
        var error = Assert.Throws<DomainValidationException>(() =>
            Deliver(new MediaParts(MediaKind.Video, Guid.CreateVersion7(), Guid.CreateVersion7(), MediaKind.Photo, Guid.CreateVersion7())));

        Assert.Contains(nameof(MediaParts.InsetPosterImageId), error.Errors.Keys);
    }

    /// <summary>"Video" for a front picture that is not there means nothing, and is not stored as anything.</summary>
    [Fact]
    public void AFrontClipNeedsAFrontPicture()
    {
        Assert.Throws<DomainValidationException>(() =>
            Deliver(new MediaParts(MediaKind.Video, Guid.CreateVersion7(), InsetMedia: MediaKind.Video, InsetPosterImageId: Guid.CreateVersion7())));
    }

    [Fact]
    public void AClipNeedsAStill()
    {
        var error = Assert.Throws<DomainValidationException>(() => Deliver(new MediaParts(MediaKind.Video)));

        Assert.Contains(nameof(MediaParts.PosterImageId), error.Errors.Keys);
    }

    [Fact]
    public void APhotographHasNoStill()
    {
        Assert.Throws<DomainValidationException>(() => Deliver(new MediaParts(MediaKind.Photo, PosterImageId: Guid.CreateVersion7())));
    }

    /// <summary>
    /// Neither a clip nor the second camera exists outside the app's camera,
    /// so a client that calls either one a screenshot is not believed.
    /// </summary>
    [Theory]
    [InlineData(ProofSource.Screenshot, true)]
    [InlineData(ProofSource.Unknown, true)]
    [InlineData(ProofSource.Screenshot, false)]
    public void AClipOrBothCamerasAreOnlyEverLive(ProofSource source, bool video)
    {
        var parts = video
            ? new MediaParts(MediaKind.Video, Guid.CreateVersion7())
            : new MediaParts(MediaKind.Photo, InsetImageId: Guid.CreateVersion7());

        var error = Assert.Throws<DomainValidationException>(() => Deliver(parts, source));

        Assert.Contains(nameof(ProofPhoto.Source), error.Errors.Keys);
    }

    [Fact]
    public void EachPartIsAPictureOfItsOwn()
    {
        var still = Guid.CreateVersion7();

        Assert.Throws<DomainValidationException>(() => Deliver(new MediaParts(MediaKind.Photo, InsetImageId: Main)));
        Assert.Throws<DomainValidationException>(() => Deliver(new MediaParts(MediaKind.Video, Main)));
        Assert.Throws<DomainValidationException>(() => Deliver(new MediaParts(MediaKind.Video, still, still)));
        Assert.Throws<DomainValidationException>(() => Deliver(new MediaParts(MediaKind.Photo, InsetImageId: Guid.Empty)));

        var front = Guid.CreateVersion7();
        Assert.Throws<DomainValidationException>(() => Deliver(new MediaParts(MediaKind.Video, still, front, MediaKind.Video, still)));
        Assert.Throws<DomainValidationException>(() => Deliver(new MediaParts(MediaKind.Video, still, front, MediaKind.Video, front)));
        Assert.Throws<DomainValidationException>(() => Deliver(new MediaParts(MediaKind.Video, still, front, MediaKind.Video, Main)));
        Assert.Throws<DomainValidationException>(() => Deliver(new MediaParts(MediaKind.Video, still, front, MediaKind.Video, Guid.Empty)));
    }
}
