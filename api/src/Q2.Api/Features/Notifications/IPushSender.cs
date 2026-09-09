using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Options;

namespace Q2.Api.Features.Notifications;

/// <summary>What a push service said about one attempt.</summary>
/// <remarks>
/// Three outcomes rather than a boolean, because the middle one is the whole
/// reason subscriptions can be cleaned up: <see cref="Gone"/> is the push
/// service saying this device will never take delivery again, which is a fact
/// worth acting on, while <see cref="Failed"/> is a device that is merely off.
/// </remarks>
public enum PushOutcome
{
    Delivered,

    /// <summary>404 or 410: the subscription is dead and should be forgotten.</summary>
    Gone,

    /// <summary>Anything else. Try again next time.</summary>
    Failed,
}

/// <summary>Puts one encrypted message on its way.</summary>
/// <remarks>
/// An interface with one implementation, the same posture as
/// <see cref="Q2.Api.Features.Images.IImageStore"/> and
/// <see cref="Q2.Api.Features.Moderation.IReportSink"/> — and here it earns its
/// place twice over, because it is also what lets the delivery rules be tested
/// without a push service on the other end of the network.
/// </remarks>
public interface IPushSender
{
    /// <summary>Whether this deployment is configured to send at all.</summary>
    bool IsConfigured { get; }

    /// <summary>The public key a browser needs in order to subscribe.</summary>
    string? PublicKey { get; }

    Task<PushOutcome> SendAsync(
        PushSubscription subscription,
        PushPayload payload,
        CancellationToken cancellationToken);
}

/// <summary>
/// What a notification says, as data rather than as a sentence.
/// </summary>
/// <remarks>
/// **The server does not write the text.** It sends a kind and its parameters,
/// and the service worker composes the sentence from the same message catalogue
/// the rest of the app uses — which is how a notification arrives in the
/// language the person chose, and why adding a language is still one file.
///
/// It is the same shape the activity feed already uses
/// (<see cref="Q2.Api.Features.Activity.ActivityEvent"/>): a kind, a subject
/// and an amount. Push is a delivery route for something that already exists,
/// not a second way of saying it.
/// </remarks>
public sealed record PushPayload(PushKind Kind, string? Subject, int? Amount, Guid? SourceId)
{
    public string ToJson() => JsonSerializer.Serialize(this, PushJson.Options);
}

/// <summary>What a notification is about.</summary>
/// <remarks>
/// Deliberately shorter than <see cref="Q2.Api.Features.Activity.ActivityKind"/>.
/// Not everything worth recording is worth interrupting somebody for: a
/// finished goal belongs in the feed and nowhere else, while a window about to
/// be missed is the one thing in q2 that stops being useful the moment it is
/// read late.
/// </remarks>
public enum PushKind
{
    /// <summary>
    /// A goal's window is about to be missed. <c>Subject</c> is the goal, and
    /// <c>Amount</c> how many proofs are still outstanding.
    /// </summary>
    WindowAtRisk,

    /// <summary>Today's challenge has been published. <c>Subject</c> is the prompt.</summary>
    ChallengePublished,
}

internal static class PushJson
{
    public static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web)
    {
        Converters = { new System.Text.Json.Serialization.JsonStringEnumConverter() },
    };
}

/// <summary>
/// The real thing: RFC 8291 over HTTP.
/// </summary>
/// <remarks>
/// Everything interesting is in <see cref="WebPushCrypto"/>; this is the part
/// that puts headers on a request and reads a status code. The two are separate
/// so the half that is a specification can be tested against the
/// specification's own worked example, and the half that is a network call can
/// be replaced in a test.
/// </remarks>
public sealed class WebPushSender(
    HttpClient client,
    IOptions<PushOptions> options,
    TimeProvider timeProvider,
    ILogger<WebPushSender> logger) : IPushSender
{
    /// <summary>How long a VAPID token is good for. Short: it is minted per request.</summary>
    public static readonly TimeSpan TokenLifetime = TimeSpan.FromHours(12);

    private readonly PushOptions _options = options.Value;

    public bool IsConfigured => _options.IsConfigured;

    public string? PublicKey => IsConfigured ? _options.PublicKey : null;

    public async Task<PushOutcome> SendAsync(
        PushSubscription subscription,
        PushPayload payload,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(subscription);
        ArgumentNullException.ThrowIfNull(payload);

        if (!IsConfigured)
        {
            return PushOutcome.Failed;
        }

        var endpoint = new Uri(subscription.Endpoint);

        using var senderKey = WebPushCrypto.NewSenderKey();
        using var signingKey = WebPushCrypto.ImportSigningKey(_options.PublicKey, _options.PrivateKey);

        var body = WebPushCrypto.Encrypt(
            Encoding.UTF8.GetBytes(payload.ToJson()),
            WebPushCrypto.Decode(subscription.PublicKey),
            WebPushCrypto.Decode(subscription.AuthSecret),
            WebPushCrypto.NewSalt(),
            senderKey);

        using var request = new HttpRequestMessage(HttpMethod.Post, endpoint)
        {
            Content = new ByteArrayContent(body),
        };

        request.Headers.TryAddWithoutValidation(
            "Authorization",
            WebPushCrypto.CreateAuthorization(
                WebPushCrypto.AudienceOf(endpoint),
                _options.Subject,
                signingKey,
                _options.PublicKey,
                timeProvider.GetUtcNow().Add(TokenLifetime)));

        request.Headers.TryAddWithoutValidation("TTL", _options.TimeToLiveSeconds.ToString(System.Globalization.CultureInfo.InvariantCulture));

        // Everything q2 sends is worth waking a device for; nothing it sends is
        // an emergency.
        request.Headers.TryAddWithoutValidation("Urgency", "normal");

        request.Content.Headers.ContentType = new MediaTypeHeaderValue("application/octet-stream");
        request.Content.Headers.ContentEncoding.Add("aes128gcm");

        try
        {
            using var response = await client.SendAsync(request, cancellationToken);

            if (response.IsSuccessStatusCode)
            {
                return PushOutcome.Delivered;
            }

            // The push service's verdict that this device is gone for good —
            // an uninstalled app, a cleared browser, a revoked permission.
            if (response.StatusCode is HttpStatusCode.NotFound or HttpStatusCode.Gone)
            {
                return PushOutcome.Gone;
            }

            // The status and nothing else. An endpoint is a stable handle for
            // one browser installation and the most identifying thing this
            // feature touches (docs/privacy.md).
            logger.LogWarning("A push was refused with {StatusCode}", (int)response.StatusCode);
            return PushOutcome.Failed;
        }
        catch (Exception exception) when (exception is HttpRequestException or TaskCanceledException)
        {
            logger.LogWarning("A push could not be delivered: {Reason}", exception.GetType().Name);
            return PushOutcome.Failed;
        }
    }
}
