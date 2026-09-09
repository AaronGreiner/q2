namespace Q2.Api.Features.Notifications;

/// <summary>
/// Whether this deployment sends notifications, and the key to subscribe with.
/// </summary>
/// <remarks>
/// <paramref name="PublicKey"/> is null when push is not configured, and the
/// settings screen says so rather than offering a switch that would do nothing.
/// A deployment without VAPID keys is a supported state, not a broken one.
/// </remarks>
public sealed record PushKeyResponse(bool IsAvailable, string? PublicKey);

/// <summary>Request body for registering a browser.</summary>
/// <remarks>
/// The three values a <c>PushSubscription</c> carries in the browser, flattened.
/// Nullable so an empty body produces field errors rather than a binding
/// failure, like every other request in this API.
/// </remarks>
public sealed record SubscribeRequest(
    string? Endpoint = null,
    string? PublicKey = null,
    string? AuthSecret = null);

/// <summary>Request body for forgetting one.</summary>
public sealed record UnsubscribeRequest(string? Endpoint = null);
