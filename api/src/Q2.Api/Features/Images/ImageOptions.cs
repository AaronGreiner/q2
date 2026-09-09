namespace Q2.Api.Features.Images;

/// <summary>
/// Where <see cref="FileSystemImageStore"/> keeps its files, bound from the
/// <c>Q2:Images</c> configuration section.
/// </summary>
/// <remarks>
/// One setting, because there is one decision: which directory. Everything else
/// about an image — how large, how many, what kind — is a domain rule and lives
/// on <see cref="StoredImage"/>, where the tests can reach it without a host.
/// </remarks>
public sealed class ImageOptions
{
    public const string SectionName = "Q2:Images";

    /// <summary>
    /// The directory the files go in. Relative paths are resolved against the
    /// same data directory the database uses.
    /// </summary>
    /// <remarks>
    /// Empty means <c>images</c> beside the database file, which is what makes
    /// "back up the data directory" one instruction rather than two. It must
    /// never be inside <c>wwwroot</c> or any other directory a static file
    /// handler serves: an image reaches a browser through
    /// <see cref="ImageEndpoints"/> and a session check, or it does not reach
    /// one at all.
    /// </remarks>
    public string RootPath { get; init; } = string.Empty;
}
