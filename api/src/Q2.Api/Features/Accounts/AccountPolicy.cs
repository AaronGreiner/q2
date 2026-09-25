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

    /// <summary>
    /// How often a session is checked against its account's security stamp.
    /// </summary>
    /// <remarks>
    /// Identity's default is thirty minutes, which means a session opened with
    /// the old password would outlive a reset by up to half an hour. A minute
    /// costs one extra read per session per minute, and is the difference
    /// between "whoever knew the old password is out" and "is out by lunch".
    /// </remarks>
    public static readonly TimeSpan SessionRecheckInterval = TimeSpan.FromMinutes(1);

    /// <summary>
    /// The authentication scheme every request is authenticated with: the
    /// session cookie, or a bearer token when the request carries one.
    /// </summary>
    /// <remarks>
    /// The browser signs in with the cookie; the iOS app cannot keep a
    /// cross-site cookie and signs in with a token instead
    /// (docs/adr/0034-bearer-tokens-for-the-native-app.md). Which one a
    /// request uses is decided by what it carries, never by who says it is.
    /// </remarks>
    public const string SessionOrTokenScheme = "q2.sessionOrToken";

    /// <summary>
    /// How long an access token is honoured.
    /// </summary>
    /// <remarks>
    /// The same as <see cref="SessionRecheckInterval"/>, and for the same
    /// reason. An access token is not checked against the security stamp — it
    /// is a sealed ticket, read without a database — so its lifetime *is* the
    /// recheck interval: after a password reset, whoever holds an old access
    /// token is out within a minute, and the refresh that would renew it is
    /// refused, because refreshing does check the stamp.
    /// </remarks>
    public static readonly TimeSpan AccessTokenLifetime = SessionRecheckInterval;

    /// <summary>
    /// How long a refresh token works: as long as an unused cookie session.
    /// </summary>
    /// <remarks>
    /// Every refresh hands out a new one, so this slides the way the cookie
    /// does — somebody who opens the app every fortnight is never signed out.
    /// </remarks>
    public static readonly TimeSpan RefreshTokenLifetime = SessionLifetime;

    /// <summary>
    /// How long the link in a reset mail works. Once, and for an hour.
    /// </summary>
    /// <remarks>
    /// Long enough to find the mail and sit down with it; short enough that a
    /// mail found in somebody's inbox next week is no longer a key. The mail
    /// itself says "one hour" (<see cref="PasswordResetMail"/>), and a test
    /// holds the two together.
    /// </remarks>
    public static readonly TimeSpan PasswordResetLinkLifetime = TimeSpan.FromHours(1);

    /// <summary>At most one reset mail per account in this long.</summary>
    /// <remarks>
    /// Long enough that nobody can fill an inbox with them, short enough that
    /// somebody whose first mail went missing does not wait long for a second
    /// (<see cref="PasswordResetCooldown"/>).
    /// </remarks>
    public static readonly TimeSpan PasswordResetMailCooldown = TimeSpan.FromMinutes(2);
}
