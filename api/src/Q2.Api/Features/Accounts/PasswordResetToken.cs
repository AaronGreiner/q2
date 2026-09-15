using System.Text;
using Microsoft.AspNetCore.WebUtilities;

namespace Q2.Api.Features.Accounts;

/// <summary>What a reset link carries after <c>#token=</c>, and how it is read back.</summary>
/// <remarks>
/// Two parts in one opaque string: the account's id, and the token Identity
/// issued for it. Identity's token is checked against one account's security
/// stamp, so the link has to say which account — and saying it with the id
/// keeps the address out of a URL that ends up in a browser history.
///
/// Identity's token is base64 with <c>+</c>, <c>/</c> and <c>=</c> in it, so it
/// is re-encoded as base64url: the link has to survive a mail client that
/// wraps, an address bar and a fragment parser unchanged.
///
/// Nothing here is a secret or a signature of ours. Identity's token is the
/// credential — data-protected, bound to the security stamp, and valid for
/// <see cref="AccountPolicy.PasswordResetLinkLifetime"/>.
/// </remarks>
public static class PasswordResetToken
{
    private const char Separator = '.';

    public static string Encode(Guid accountId, string identityToken)
    {
        ArgumentException.ThrowIfNullOrEmpty(identityToken);

        return $"{accountId:N}{Separator}{WebEncoders.Base64UrlEncode(Encoding.UTF8.GetBytes(identityToken))}";
    }

    /// <summary>False for anything that is not a token this class wrote, without saying why.</summary>
    public static bool TryDecode(string? value, out Guid accountId, out string identityToken)
    {
        accountId = Guid.Empty;
        identityToken = string.Empty;

        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        var separator = value.IndexOf(Separator, StringComparison.Ordinal);

        if (separator <= 0
            || separator == value.Length - 1
            || !Guid.TryParseExact(value.AsSpan(0, separator), "N", out var id))
        {
            return false;
        }

        try
        {
            var bytes = WebEncoders.Base64UrlDecode(value, separator + 1, value.Length - separator - 1);
            identityToken = Encoding.UTF8.GetString(bytes);
        }
        catch (FormatException)
        {
            return false;
        }

        accountId = id;
        return identityToken.Length > 0;
    }

    /// <summary>The link a reset mail carries.</summary>
    /// <remarks>
    /// The token goes in the fragment rather than the query, and that is the
    /// point of the format: a browser never sends a fragment anywhere, so the
    /// token reaches neither the reverse proxy nor the server rendering the page
    /// nor a server-side trace. The page lifts it out of the address bar before
    /// anything else on it runs (app/app/utils/resetLink.ts).
    /// </remarks>
    public static string Link(Uri publicAppUrl, string token)
    {
        ArgumentNullException.ThrowIfNull(publicAppUrl);
        ArgumentException.ThrowIfNullOrEmpty(token);

        return $"{publicAppUrl.AbsoluteUri.TrimEnd('/')}/reset-password#token={token}";
    }
}
