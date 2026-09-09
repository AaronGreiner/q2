namespace Q2.Api.Features.Images;

/// <summary>
/// Where the bytes of an image live.
/// </summary>
/// <remarks>
/// The interface exists so the answer can change. Today it is the file system
/// of whatever host q2 runs on, which is the same posture as "SQLite first"
/// (docs/adr/0004-sqlite-first.md): the simplest thing that carries the product
/// now, behind a seam that turns the move to an S3-compatible bucket into
/// configuration rather than a rewrite.
///
/// It is deliberately narrow. There is no listing, no enumeration and no
/// "does this exist" — the database already answers all three, and a store that
/// could be queried would become a second, disagreeing index of what q2 holds.
/// The <see cref="StoredImage"/> row is the truth; this is a bag of bytes with
/// an id on it.
/// </remarks>
public interface IImageStore
{
    /// <summary>Writes the bytes for <paramref name="id"/>, replacing any that exist.</summary>
    Task SaveAsync(Guid id, ReadOnlyMemory<byte> bytes, CancellationToken cancellationToken);

    /// <summary>
    /// Opens the bytes for reading, or returns null when there are none.
    /// </summary>
    /// <remarks>
    /// A stream rather than a byte array: a four-megabyte response should not
    /// pass through the managed heap on its way to a socket, and a phone on a
    /// train will hold the connection open long enough for that to matter.
    /// </remarks>
    Task<Stream?> OpenAsync(Guid id, CancellationToken cancellationToken);

    /// <summary>
    /// Removes the bytes for <paramref name="id"/>. Removing something that is
    /// not there is not an error — a delete that has already half-happened must
    /// be able to finish.
    /// </summary>
    Task DeleteAsync(Guid id, CancellationToken cancellationToken);
}
