using Q2.Api.Infrastructure.Errors;

namespace Q2.Api.Features.Notifications;

/// <summary>
/// One browser, on one device, that has agreed to be told things.
/// </summary>
/// <remarks>
/// A row per <em>device</em> rather than per person: the same account on a
/// phone and a laptop is two subscriptions, and both should ring. The endpoint
/// is what makes them different, which is why it is the unique key rather than
/// the person.
///
/// **All three values come from the browser and none of them is ours.** The
/// endpoint addresses a push service somebody else runs; the two keys are the
/// browser's half of the encryption
/// (<see cref="WebPushCrypto"/>). q2 stores them, uses them, and can do nothing
/// else with them — it cannot read a notification it has sent, and neither can
/// the push service.
///
/// They are personal data all the same, and the most identifying kind: an
/// endpoint is a stable handle for one browser installation. It is never
/// logged, never sent to Sentry, and goes with the account when it is deleted.
/// </remarks>
public sealed class PushSubscription
{
    /// <summary>How long an endpoint may be. Generous: they are opaque and vendors differ.</summary>
    public const int MaxEndpointLength = 512;

    /// <summary>How long the two browser keys may be, base64url.</summary>
    public const int MaxKeyLength = 128;

    /// <summary>
    /// How many failures in a row before a subscription is given up on.
    /// </summary>
    /// <remarks>
    /// A push service answers 404 or 410 when a subscription is really gone,
    /// and those delete it immediately — this is for everything else. A device
    /// that has been off for a week produces timeouts rather than a verdict,
    /// and dropping it on the first one would unsubscribe somebody for going on
    /// holiday.
    /// </remarks>
    public const int MaxConsecutiveFailures = 10;

    // EF Core materialisation only.
    private PushSubscription()
    {
    }

    private PushSubscription(
        Guid id,
        Guid personId,
        string endpoint,
        string publicKey,
        string authSecret,
        DateTimeOffset createdAt)
    {
        Id = id;
        PersonId = personId;
        Endpoint = endpoint;
        PublicKey = publicKey;
        AuthSecret = authSecret;
        CreatedAt = createdAt;
    }

    public Guid Id { get; private set; }

    public Guid PersonId { get; private set; }

    /// <summary>Where the push service takes delivery. Opaque, and never logged.</summary>
    public string Endpoint { get; private set; } = string.Empty;

    /// <summary>The browser's public key, base64url — <c>p256dh</c> in the browser's own vocabulary.</summary>
    public string PublicKey { get; private set; } = string.Empty;

    /// <summary>The subscription's shared secret, base64url.</summary>
    public string AuthSecret { get; private set; } = string.Empty;

    public DateTimeOffset CreatedAt { get; private set; }

    /// <summary>When something last went out through it and was accepted.</summary>
    public DateTimeOffset? LastDeliveredAt { get; private set; }

    /// <summary>How many attempts in a row have failed without a verdict.</summary>
    public int ConsecutiveFailures { get; private set; }

    /// <exception cref="DomainValidationException">The browser sent something unusable.</exception>
    public static PushSubscription Create(
        Guid id,
        Guid personId,
        string? endpoint,
        string? publicKey,
        string? authSecret,
        DateTimeOffset createdAt)
    {
        var errors = new Dictionary<string, string[]>();

        var trimmedEndpoint = endpoint?.Trim() ?? string.Empty;

        // An endpoint that is not an absolute https URL cannot be posted to, and
        // one that is not https would send an encrypted payload over a channel
        // that leaks who is being notified.
        if (!Uri.TryCreate(trimmedEndpoint, UriKind.Absolute, out var parsed)
            || parsed.Scheme != Uri.UriSchemeHttps
            || trimmedEndpoint.Length > MaxEndpointLength)
        {
            errors[nameof(Endpoint)] = ["A push endpoint has to be an https URL."];
        }

        if (string.IsNullOrWhiteSpace(publicKey) || publicKey.Length > MaxKeyLength)
        {
            errors[nameof(PublicKey)] = ["A subscription needs its browser key."];
        }

        if (string.IsNullOrWhiteSpace(authSecret) || authSecret.Length > MaxKeyLength)
        {
            errors[nameof(AuthSecret)] = ["A subscription needs its shared secret."];
        }

        if (errors.Count > 0)
        {
            throw new DomainValidationException(errors);
        }

        return new PushSubscription(
            id,
            personId,
            trimmedEndpoint,
            publicKey!.Trim(),
            authSecret!.Trim(),
            createdAt);
    }

    /// <summary>
    /// Takes the browser's current keys for an endpoint that already exists.
    /// </summary>
    /// <remarks>
    /// A browser may re-subscribe to the same endpoint with new keys — it
    /// happens when a service worker updates — and the honest response is to
    /// believe the newer ones rather than to keep sending things the device can
    /// no longer decrypt.
    /// </remarks>
    public void Refresh(string publicKey, string authSecret, DateTimeOffset now)
    {
        PublicKey = publicKey;
        AuthSecret = authSecret;
        CreatedAt = now;
        ConsecutiveFailures = 0;
    }

    /// <summary>Records that something arrived.</summary>
    public void Delivered(DateTimeOffset now)
    {
        LastDeliveredAt = now;
        ConsecutiveFailures = 0;
    }

    /// <summary>
    /// Records a failure with no verdict. Returns true once it has failed often
    /// enough to be given up on.
    /// </summary>
    public bool Failed() => ++ConsecutiveFailures >= MaxConsecutiveFailures;
}
