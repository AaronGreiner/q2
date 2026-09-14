using System.Collections.Concurrent;
using Microsoft.AspNetCore.SignalR;
using Q2.Api.Features.People;
using Q2.Api.Infrastructure.Errors;

namespace Q2.Api.Features.Notifications;

/// <summary>The parts of an open app the server can say have changed.</summary>
/// <remarks>
/// Named after what a person sees rather than after tables, because the client
/// maps each one onto the screens that show it. Mirrored by hand in the
/// frontend's live composable: it never travels over a documented endpoint, so
/// nothing in the OpenAPI contract describes it.
/// </remarks>
public enum LiveArea
{
    /// <summary>The bell.</summary>
    Notifications,

    /// <summary>The conversation list, and one thread when an id comes with it.</summary>
    Chats,

    /// <summary>Requests, friends and suggestions.</summary>
    Friends,

    /// <summary>The photographs waiting for somebody's verdict.</summary>
    Proofs,

    /// <summary>Goals, and one goal's screen when an id comes with it.</summary>
    Goals,

    /// <summary>Today's challenge room.</summary>
    Challenge,

    /// <summary>The friends' feed, and your own history under it.</summary>
    Feed,
}

/// <summary>"This part of your screen changed — read it again."</summary>
/// <param name="Id">Which conversation or goal, when it is one in particular.</param>
public sealed record LiveChange(LiveArea Area, Guid? Id = null);

/// <summary>The only three things the server ever says over a live connection.</summary>
public static class LiveEvents
{
    /// <summary>The badge numbers, as a <see cref="CountsResponse"/>.</summary>
    public const string Counts = "counts";

    /// <summary>A <see cref="LiveChange"/>.</summary>
    public const string Changed = "changed";

    /// <summary>
    /// A <see cref="NotificationResponse"/>, for the app to show as a banner:
    /// what a push would have carried, sent instead of one to somebody who is
    /// looking (<see cref="NotificationRules.InterruptionFor"/>).
    /// </summary>
    public const string Notification = "notification";
}

/// <summary>
/// An open app's line back from the server.
/// </summary>
/// <remarks>
/// SignalR rather than a stream of server-sent events, so a later "tippt …" or
/// presence has a channel to use without a second transport
/// ([0024](../../../../docs/adr/0024-one-notification-pipeline.md)). Today it
/// is used in one direction only, and that is the design rather than a phase:
///
/// **There is no method a client can call.** Every change still goes through
/// the REST API, so the OpenAPI document stays the only contract and every
/// rule about who may do what stays where it already is. A client that could
/// invoke something here would be a second API with its own authorisation to
/// get right — and, as with the push endpoints before it, nothing here can ask
/// for somebody to be notified.
///
/// **It says what changed, never what it now is.** A <see cref="LiveChange"/>
/// names part of the screen, and the screen reads it again through the endpoint
/// it already uses. That keeps one read path with every scoping rule on it —
/// blocks, participants, a covered challenge room — instead of a second one in
/// here that would have to get each of them right again.
///
/// The one thing it hands over whole is a notification for somebody who is
/// looking, and that is not a second read path: it is the payload a push would
/// have carried, built by the same code for the same person after the same
/// rules, arriving in the app instead of on the phone
/// ([0025](../../../../docs/adr/0025-banners-in-the-open-app.md)).
///
/// A connection joins one group, its person's. Two devices are two connections
/// in the same group, and both hear everything.
/// </remarks>
public sealed class LiveHub(
    CurrentPerson currentPerson,
    LiveConnections connections,
    ILogger<LiveHub> logger) : Hub
{
    /// <summary>
    /// Under <c>/api</c>, so the one reverse-proxy rule that already sends the
    /// API its requests sends it these too, WebSocket upgrade included.
    /// </summary>
    public const string Path = "/api/live";

    private const string PersonKey = "q2.person";

    /// <summary>The group every connection of this person joins.</summary>
    public static string GroupOf(Guid personId) => $"person:{personId:N}";

    public override async Task OnConnectedAsync()
    {
        Person person;

        try
        {
            // Through CurrentPerson like every other feature, from the
            // connection's own principal: a hub has no request of its own to
            // read one from once the connection is established.
            person = await currentPerson.ForPrincipalAsync(Context.User, Context.ConnectionAborted);
        }
        catch (AuthenticationRequiredException)
        {
            // A cookie whose account has since been deleted. Expected, and not
            // an error: the connection simply has nobody to belong to.
            Context.Abort();
            return;
        }

        Context.Items[PersonKey] = person.Id;
        await Groups.AddToGroupAsync(Context.ConnectionId, GroupOf(person.Id), Context.ConnectionAborted);
        connections.Opened(person.Id, Context.ConnectionId);

        // Nothing about whom. Who is looking at the app right now is exactly
        // the kind of fact a log line should not be the place to find out.
        logger.LogDebug("A live connection opened");

        await base.OnConnectedAsync();
    }

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        if (Context.Items.TryGetValue(PersonKey, out var value) && value is Guid personId)
        {
            connections.Closed(personId, Context.ConnectionId);
        }

        await base.OnDisconnectedAsync(exception);
    }
}

/// <summary>
/// Who has the app open, right now, on this host.
/// </summary>
/// <remarks>
/// What decides whether somebody is shown a notification in the app or sent a
/// push. The client keeps a connection only while its page is visible, so
/// "connected" here means "watching", not "signed in somewhere".
///
/// In-process on purpose: q2 is one process on one host
/// ([0008](../../../../docs/adr/0008-deployment-topology.md)). A second instance
/// is the moment this and the hub's groups move to a backplane, and not before.
/// </remarks>
public sealed class LiveConnections
{
    private readonly ConcurrentDictionary<Guid, ConcurrentDictionary<string, byte>> _open = new();

    public void Opened(Guid personId, string connectionId) =>
        _open.GetOrAdd(personId, _ => new ConcurrentDictionary<string, byte>()).TryAdd(connectionId, 0);

    public void Closed(Guid personId, string connectionId)
    {
        // The person's entry stays behind when it empties. Removing it would
        // race the same person's next connection being added to it, and an
        // empty set already answers IsWatching correctly.
        if (_open.TryGetValue(personId, out var connectionsOfPerson))
        {
            connectionsOfPerson.TryRemove(connectionId, out _);
        }
    }

    /// <summary>Whether any device of this person has the app in front of them.</summary>
    public bool IsWatching(Guid personId) =>
        _open.TryGetValue(personId, out var connectionsOfPerson) && !connectionsOfPerson.IsEmpty;

    /// <summary>
    /// Everybody with the app in front of them — for the one notification that
    /// is addressed to everybody rather than to anybody's friends.
    /// </summary>
    public IReadOnlyList<Guid> Watching() =>
        [.. _open.Where(entry => !entry.Value.IsEmpty).Select(entry => entry.Key)];
}
