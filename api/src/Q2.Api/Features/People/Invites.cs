using System.Buffers.Text;
using System.Security.Cryptography;

namespace Q2.Api.Features.People;

/// <summary>
/// Makes invite codes.
/// </summary>
/// <remarks>
/// A code has one job: to be impossible to arrive at without having been sent
/// it. Everything else about the link — where it points, what the page says —
/// is the client's, which is why nothing here builds a URL. The server does not
/// know what host the app is served from, and a link with the wrong origin in
/// it is worse than no link.
///
/// Twelve random bytes, base64url. That is 96 bits: far past anything worth
/// guessing at, and still short enough to read out loud if somebody has to.
/// </remarks>
public static class Invites
{
    /// <summary>How many random bytes go into a code.</summary>
    public const int EntropyBytes = 12;

    /// <summary>
    /// How many times to try again if a code is somehow already taken.
    /// </summary>
    /// <remarks>
    /// A collision at 96 bits is not something that happens, and a loop that
    /// pretends otherwise is dishonest — but a unique index that can throw is a
    /// unique index that eventually will, and two retries cost nothing.
    /// </remarks>
    public const int Attempts = 3;

    /// <summary>
    /// A fresh code, URL-safe and without padding.
    /// </summary>
    /// <remarks>
    /// <see cref="RandomNumberGenerator"/> rather than <see cref="Random"/>:
    /// this is a credential in everything but name, and a predictable one would
    /// make the whole design pointless.
    /// </remarks>
    public static string NewCode() => Base64Url.EncodeToString(RandomNumberGenerator.GetBytes(EntropyBytes));
}
