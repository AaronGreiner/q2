using Microsoft.EntityFrameworkCore;
using Q2.Api.Features.People;
using Q2.Api.Features.Proofs;
using Q2.Api.Infrastructure.Errors;
using Q2.Api.Infrastructure.Persistence;
using Q2.Api.Infrastructure.Time;

namespace Q2.Api.Features.Images;

/// <summary>
/// Everything q2 decides about a picture: whether to accept it, who may see it
/// and what happens when it goes.
/// </summary>
/// <remarks>
/// The rules are here rather than in the endpoint because they are not
/// presentation. Whether a photograph is readable is the same question whether
/// it is asked by an <c>&lt;img&gt;</c>, by a future native client or by a
/// share sheet, and an answer written into a route handler is an answer the
/// second caller does not get (section 7e of the migration plan).
/// </remarks>
public sealed class ImageService(
    Q2DbContext database,
    IImageStore store,
    CurrentPerson currentPerson,
    IIdGenerator ids,
    TimeProvider timeProvider)
{
    /// <summary>
    /// Stores an upload after checking that it is an image, that it is within
    /// the limits, and that the person has room for it.
    /// </summary>
    /// <exception cref="DomainValidationException">
    /// The bytes are not an accepted image, or the person is out of allowance.
    /// </exception>
    public async Task<ImageResponse> UploadAsync(
        ImagePurpose purpose,
        ReadOnlyMemory<byte> bytes,
        CancellationToken cancellationToken)
    {
        var me = await currentPerson.GetAsync(cancellationToken);

        // What it is, from the bytes. The Content-Type header got the request
        // routed here and is not consulted again: the caller wrote it.
        if (ImageFormatReader.Read(bytes.Span) is not { } content)
        {
            throw new DomainValidationException(
                "File",
                $"An upload has to be one of: {string.Join(", ", ImageFormatReader.AcceptedContentTypes)}.");
        }

        var quota = await GetQuotaAsync(me.Id, cancellationToken);

        if (quota.ImageCount >= StoredImage.MaxImagesPerPerson
            || quota.BytesUsed + content.ByteSize > StoredImage.MaxBytesPerPerson)
        {
            throw new DomainValidationException(
                "File",
                "You have used all of your image storage. Delete something first.");
        }

        var image = StoredImage.Create(
            ids.NewId(),
            me.Id,
            purpose,
            content,
            timeProvider.GetUtcNow());

        // Bytes first, row second. The other order can leave a row pointing at
        // nothing, which every reader afterwards has to handle; this order can
        // at worst leave a file nobody references, which costs disk and no
        // correctness.
        await store.SaveAsync(image.Id, bytes, cancellationToken);

        database.Images.Add(image);
        await database.SaveChangesAsync(cancellationToken);

        return ImageResponse.From(image);
    }

    /// <summary>
    /// Opens an image for the person asking, or throws
    /// <see cref="ResourceNotFoundException"/> when it is not theirs to see.
    /// </summary>
    /// <remarks>
    /// Not-found rather than forbidden, deliberately. "You may not see this
    /// image" confirms that the image exists, which is itself a disclosure —
    /// the same reasoning the chats already use
    /// (<see cref="AccessDeniedException"/>).
    /// </remarks>
    public async Task<(StoredImage Image, Stream Content)> OpenAsync(Guid id, CancellationToken cancellationToken)
    {
        var viewerId = await currentPerson.GetIdAsync(cancellationToken);

        var image = await database.Images
            .AsNoTracking()
            .SingleOrDefaultAsync(candidate => candidate.Id == id, cancellationToken);

        if (image is null || !await CanReadAsync(image, viewerId, cancellationToken))
        {
            throw new ResourceNotFoundException("Image", id);
        }

        var content = await store.OpenAsync(id, cancellationToken)
            ?? throw new ResourceNotFoundException("Image", id);

        return (image, content);
    }

    /// <summary>
    /// Deletes an image and its bytes. Only its owner may.
    /// </summary>
    /// <remarks>
    /// An avatar that is deleted stops being one: the person falls back to
    /// their initials rather than to a broken picture. Doing it here, in the
    /// same transaction, is what keeps "deleted" from meaning "still on every
    /// screen until somebody notices".
    /// </remarks>
    public async Task DeleteAsync(Guid id, CancellationToken cancellationToken)
    {
        var me = await currentPerson.GetAsync(cancellationToken);

        var image = await database.Images
            .SingleOrDefaultAsync(candidate => candidate.Id == id, cancellationToken);

        if (image is null || image.OwnerPersonId != me.Id)
        {
            throw new ResourceNotFoundException("Image", id);
        }

        if (me.AvatarImageId == id)
        {
            me.SetAvatarImage(null);
        }

        database.Images.Remove(image);
        await database.SaveChangesAsync(cancellationToken);

        // Bytes last: a row removed and a file left behind is wasted disk, the
        // reverse is a reference to nothing.
        await store.DeleteAsync(id, cancellationToken);
    }

    /// <summary>
    /// Marks a person's images for deletion without saving, and returns the
    /// ids that were actually theirs.
    /// </summary>
    /// <remarks>
    /// The half of a deletion that has to share a transaction with whatever
    /// owned the pictures — a goal being deleted from the archive, today. The
    /// bytes are a separate step (<see cref="DeleteBytesAsync"/>) because they
    /// are not transactional: a file removed before the rows are committed is
    /// a picture that comes back as a broken image if the commit fails.
    /// </remarks>
    public async Task<IReadOnlyList<Guid>> RemoveOwnedAsync(
        IReadOnlyCollection<Guid> ids,
        Guid ownerId,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(ids);

        if (ids.Count == 0)
        {
            return [];
        }

        var images = await database.Images
            .Where(image => ids.Contains(image.Id) && image.OwnerPersonId == ownerId)
            .ToListAsync(cancellationToken);

        database.Images.RemoveRange(images);

        return [.. images.Select(image => image.Id)];
    }

    /// <summary>
    /// Deletes the bytes behind images whose rows have already gone.
    /// </summary>
    /// <remarks>
    /// Always after the save, never before: a row removed with the file left
    /// behind is wasted disk, the other way round is a reference to nothing.
    /// </remarks>
    public async Task DeleteBytesAsync(IReadOnlyCollection<Guid> ids, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(ids);

        foreach (var id in ids)
        {
            await store.DeleteAsync(id, cancellationToken);
        }
    }

    /// <summary>What the signed-in person has used of their allowance.</summary>
    public async Task<ImageQuotaResponse> GetOwnQuotaAsync(CancellationToken cancellationToken) =>
        await GetQuotaAsync(await currentPerson.GetIdAsync(cancellationToken), cancellationToken);

    /// <summary>
    /// Confirms that an image exists, belongs to <paramref name="ownerId"/> and
    /// was uploaded for <paramref name="purpose"/> — what a feature calls
    /// before pointing something at it.
    /// </summary>
    /// <remarks>
    /// Without it, "set my avatar to this id" would accept any id at all,
    /// including somebody else's proof photograph, and the avatar endpoint
    /// would have quietly become a way to read one.
    /// </remarks>
    public async Task<StoredImage> RequireOwnedAsync(
        Guid id,
        Guid ownerId,
        ImagePurpose purpose,
        CancellationToken cancellationToken)
    {
        var image = await database.Images
            .AsNoTracking()
            .SingleOrDefaultAsync(candidate => candidate.Id == id, cancellationToken);

        return image is null || image.OwnerPersonId != ownerId || image.Purpose != purpose
            ? throw new ResourceNotFoundException("Image", id)
            : image;
    }

    /// <summary>
    /// Whether <paramref name="viewerId"/> may read this image, including the
    /// answers that need the database.
    /// </summary>
    /// <remarks>
    /// A proof photograph's audience is the goal it was delivered against, and
    /// that cannot be answered from the image row alone. Split from
    /// <see cref="CanRead"/> so the part that is a pure rule stays testable
    /// without a database, and the part that is a query is obviously a query.
    /// </remarks>
    public async Task<bool> CanReadAsync(StoredImage image, Guid viewerId, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(image);

        if (CanRead(image, viewerId))
        {
            return true;
        }

        /*
         * The people the goal is shared with, and nobody else.
         *
         * Not "everybody signed in": a photograph of somebody's living room
         * delivered against "aufräumen" is not public the way an avatar is. Not
         * "friends" either — a friendship is not an invitation to every goal.
         * The audience is exactly the audience of the goal, which is the set
         * the chat and the history already use.
         *
         * A proof whose vote has closed stays readable to that same set: the
         * history grid shows what was delivered, and a picture that vanished
         * the moment it was believed would make it unreadable.
         */
        if (image.Purpose == ImagePurpose.Proof)
        {
            return await database.ProofPhotos
                .AsNoTracking()
                .AnyAsync(
                    proof => proof.ImageId == image.Id
                        && database.Goals.Any(goal =>
                            goal.Instances.Any(instance => instance.Id == proof.GoalInstanceId)
                            && (goal.OwnerPersonId == viewerId
                                || goal.Participants.Any(participant => participant.PersonId == viewerId))),
                    cancellationToken);
        }

        /*
         * A challenge contribution: a friend of the author, who has contributed
         * to the same challenge themselves.
         *
         * The same test the room applies, repeated here on purpose. The room
         * leaves the id out until somebody has taken part, and this is what
         * makes that a rule rather than a habit: an id kept from an earlier day,
         * guessed, or read out of somebody else's screenshot still does not open
         * the picture.
         *
         * It stays true after the day is over. The room is gone by then, but the
         * author's own archive is not, and the friends who saw it that evening
         * are not somebody the picture has to be taken back from.
         */
        return image.Purpose == ImagePurpose.ChallengeEntry
            && await database.ChallengeEntries
                .AsNoTracking()
                .AnyAsync(
                    entry => entry.ImageId == image.Id
                        && database.ChallengeEntries.Any(mine =>
                            mine.ChallengeId == entry.ChallengeId && mine.PersonId == viewerId)
                        && database.Friendships.Any(friendship =>
                            friendship.Status == FriendshipStatus.Accepted
                            && ((friendship.RequesterId == viewerId && friendship.AddresseeId == entry.PersonId)
                                || (friendship.RequesterId == entry.PersonId && friendship.AddresseeId == viewerId))),
                    cancellationToken);
    }

    /// <summary>
    /// Whether <paramref name="viewerId"/> may read this image on the strength
    /// of its purpose alone.
    /// </summary>
    /// <remarks>
    /// The purpose-only access rule, in one place, switching on the purpose — so a new
    /// kind of image is a compiler-visible decision rather than something that
    /// defaults to visible. An avatar is as public as the initials it replaces:
    /// it appears in search results and friend suggestions, where the viewer is
    /// by definition not connected to the person yet.
    ///
    /// A proof is <em>not</em> answered here, because its audience is a question
    /// about a goal rather than about the picture — and a challenge contribution
    /// is not, because its audience is a question about who else took part. Both
    /// are in <see cref="CanReadAsync"/>. Anything else is its owner's alone,
    /// which is the safe default a new purpose falls into until somebody decides
    /// otherwise on purpose.
    /// </remarks>
    public static bool CanRead(StoredImage image, Guid viewerId)
    {
        ArgumentNullException.ThrowIfNull(image);

        return image.OwnerPersonId == viewerId || image.Purpose switch
        {
            ImagePurpose.Avatar => true,
            _ => false,
        };
    }

    private async Task<ImageQuotaResponse> GetQuotaAsync(Guid personId, CancellationToken cancellationToken)
    {
        var used = await database.Images
            .AsNoTracking()
            .Where(image => image.OwnerPersonId == personId)
            .GroupBy(_ => 1)
            .Select(group => new { Bytes = group.Sum(image => (long)image.ByteSize), Count = group.Count() })
            .SingleOrDefaultAsync(cancellationToken);

        return new ImageQuotaResponse(
            used?.Bytes ?? 0,
            StoredImage.MaxBytesPerPerson,
            used?.Count ?? 0,
            StoredImage.MaxImagesPerPerson);
    }
}
