using Microsoft.Extensions.Options;
using Q2.Api.Infrastructure.Persistence;

namespace Q2.Api.Features.Images;

/// <summary>
/// Keeps image bytes as files in a directory on the host.
/// </summary>
/// <remarks>
/// Two things about this are decisions rather than details.
///
/// **The directory is not served.** Nothing maps a static file handler at it,
/// it is not under <c>wwwroot</c>, and the only way out of it is
/// <see cref="ImageEndpoints"/>, which checks the session first. A guessable
/// address in a public folder would be the one place the whole access control
/// of the app is bypassed — every other read in q2 is scoped to the person
/// asking (AGENTS.md section 8), and an image must not be the exception.
///
/// **The path is derived, never stored.** <c>a1/a1b2c3….bin</c>: the first byte
/// of the id becomes a subdirectory, so no directory holds more than a few
/// hundred files however many are uploaded, and the mapping stays a pure
/// function of the id. Storing the path instead would be a second copy of a
/// fact that can then be wrong.
///
/// The extension is <c>.bin</c> on purpose. The media type is a column on
/// <see cref="StoredImage"/>, read out of the bytes; a <c>.jpg</c> on disk
/// would invite something to serve the directory one day.
/// </remarks>
public sealed class FileSystemImageStore : IImageStore
{
    private readonly string _root;

    public FileSystemImageStore(IOptions<ImageOptions> options, IHostEnvironment environment)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(environment);

        var configured = options.Value.RootPath;

        _root = string.IsNullOrWhiteSpace(configured)
            ? Path.Combine(DatabaseLocation.ResolveDataDirectory(environment.ContentRootPath), "images")
            : Path.GetFullPath(configured);
    }

    /// <summary>The directory files are written to. Absolute.</summary>
    public string RootPath => _root;

    public async Task SaveAsync(Guid id, ReadOnlyMemory<byte> bytes, CancellationToken cancellationToken)
    {
        var path = PathFor(id);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);

        // Written beside the target and moved into place, so a crash halfway
        // through leaves no half-file that a later read would serve as an
        // image. The move is atomic on every file system q2 runs on.
        var temporary = $"{path}.{Guid.NewGuid():N}.tmp";

        try
        {
            await File.WriteAllBytesAsync(temporary, bytes, cancellationToken);
            File.Move(temporary, path, overwrite: true);
        }
        catch
        {
            if (File.Exists(temporary))
            {
                File.Delete(temporary);
            }

            throw;
        }
    }

    public Task<Stream?> OpenAsync(Guid id, CancellationToken cancellationToken)
    {
        var path = PathFor(id);

        if (!File.Exists(path))
        {
            return Task.FromResult<Stream?>(null);
        }

        Stream stream = new FileStream(
            path,
            FileMode.Open,
            FileAccess.Read,
            FileShare.Read,
            bufferSize: 64 * 1024,
            useAsync: true);

        return Task.FromResult<Stream?>(stream);
    }

    public Task DeleteAsync(Guid id, CancellationToken cancellationToken)
    {
        var path = PathFor(id);

        if (File.Exists(path))
        {
            File.Delete(path);
        }

        return Task.CompletedTask;
    }

    private string PathFor(Guid id)
    {
        var name = id.ToString("N");
        return Path.Combine(_root, name[..2], $"{name}.bin");
    }
}
