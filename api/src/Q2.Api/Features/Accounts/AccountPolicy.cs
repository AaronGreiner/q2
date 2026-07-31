namespace Q2.Api.Features.Accounts;

/// <summary>
/// What q2 requires of an account, in one place.
/// </summary>
/// <remarks>
/// Read by both the Identity options and
/// <see cref="AccountRequestValidator"/>, so the rule a person is told about
/// and the rule that is actually enforced cannot drift apart.
/// </remarks>
public static class AccountPolicy
{
    /// <summary>
    /// Length is the whole password requirement.
    /// </summary>
    /// <remarks>
    /// Identity's defaults ask for an upper-case letter, a lower-case letter, a
    /// digit and a symbol in six characters. That produces "Passw0rd!" — worse
    /// than a longer passphrase, and far more annoying on a phone keyboard.
    /// Current NIST guidance (SP 800-63B) says the same: require length, drop
    /// composition rules.
    /// </remarks>
    public const int MinimumPasswordLength = 10;

    /// <summary>
    /// Not a security rule but a bound: a hasher should never be handed a
    /// megabyte of text by somebody who noticed there was no limit.
    /// </summary>
    public const int MaximumPasswordLength = 256;

    /// <summary>The longest address RFC 5321 allows.</summary>
    public const int MaximumEmailLength = 254;

    /// <summary>
    /// The session cookie's name. Deliberately not the framework default
    /// (".AspNetCore.Identity.Application"), which announces what the server is
    /// built with to anyone who opens the developer tools.
    /// </summary>
    public const string SessionCookieName = "q2.session";

    /// <summary>
    /// How long a session lasts without being used. Sliding, so somebody who
    /// opens q2 every day is never signed out.
    /// </summary>
    public static readonly TimeSpan SessionLifetime = TimeSpan.FromDays(14);
}
