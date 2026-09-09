namespace Q2.Api.Features.Notifications;

/// <summary>
/// The VAPID key pair and who to contact about it, bound from
/// <c>Q2:Push</c>.
/// </summary>
/// <remarks>
/// **Empty keys turn push off**, and that is the default. Nothing breaks: the
/// browser is told there is no key to subscribe with, the settings screen says
/// so, and the workers skip delivery. A deployment that wants notifications
/// generates a pair and configures it; one that does not is not carrying a
/// half-working feature.
///
/// The private key is a credential and belongs in the environment rather than
/// in <c>appsettings.json</c>, exactly like the Sentry DSN and the connection
/// string — <c>Q2__Push__PrivateKey</c>. The public key is not a secret at all;
/// it is handed to every browser that subscribes.
/// </remarks>
public sealed class PushOptions
{
    public const string SectionName = "Q2:Push";

    /// <summary>
    /// How long a push service may hold a message for a device that is off.
    /// </summary>
    /// <remarks>
    /// Four hours. Both things q2 sends are about *today* — a window closing at
    /// midnight, a prompt that expires with the day — so a notification
    /// delivered tomorrow morning would be about something that can no longer
    /// be acted on, which is the same reason quiet hours drop rather than hold
    /// (<see cref="QuietHours"/>).
    /// </remarks>
    public const int DefaultTimeToLiveSeconds = 4 * 60 * 60;

    /// <summary>The uncompressed P-256 point, base64url. Public by design.</summary>
    public string PublicKey { get; init; } = string.Empty;

    /// <summary>The private scalar, base64url. A credential.</summary>
    public string PrivateKey { get; init; } = string.Empty;

    /// <summary>
    /// A <c>mailto:</c> or https URL a push service can use to reach whoever
    /// runs this deployment. Required by RFC 8292; not a secret.
    /// </summary>
    public string Subject { get; init; } = "mailto:push@q2.invalid";

    public int TimeToLiveSeconds { get; init; } = DefaultTimeToLiveSeconds;

    /// <summary>Whether this deployment can send anything at all.</summary>
    public bool IsConfigured =>
        !string.IsNullOrWhiteSpace(PublicKey)
        && !string.IsNullOrWhiteSpace(PrivateKey)
        && !string.IsNullOrWhiteSpace(Subject);
}
