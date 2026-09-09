using Q2.Api.Features.Notifications;
using Q2.Api.Infrastructure.Time;

namespace Q2.Api.IntegrationTests.Infrastructure;

/// <summary>
/// A clock that never moves, so seeds, <c>isOverdue</c> and created timestamps
/// are all computed against the same known instant.
/// </summary>
public sealed class FixedTimeProvider(DateTimeOffset start) : TimeProvider
{
    /*
     * Two fields rather than one plus the captured parameter: a primary
     * constructor parameter that is both stored *and* read later is captured
     * into hidden state, which the Release build rejects as CS9124. Naming both
     * says plainly that one of them never moves.
     */
    private readonly DateTimeOffset _start = start;

    private DateTimeOffset _now = start;

    public override DateTimeOffset GetUtcNow() => _now;

    /// <summary>
    /// Moves the clock. For the handful of tests whose subject *is* the time of
    /// day — the evening warning, and a voting deadline running out.
    /// </summary>
    /// <remarks>
    /// The clock is shared by everything in the host, so a test that moves it
    /// puts it back afterwards. That is why this is a setter rather than a
    /// second provider: one clock the whole application agrees on is what makes
    /// every other assertion in the suite reproducible, and two would be a
    /// quiet way to lose that.
    /// </remarks>
    public void Set(DateTimeOffset value) => _now = value;

    /// <summary>Puts it back to the instant every other test runs at.</summary>
    public void Reset() => _now = _start;
}

/// <summary>
/// Ids that are predictable per test host: <c>aaaaaaaa-0000-4000-8000-000000000001</c>
/// and up. Lets a test assert the exact id of the goal it just created.
/// </summary>
public sealed class SequentialTestIdGenerator : IIdGenerator
{
    private int _next;

    public Guid NewId() => IdAt(Interlocked.Increment(ref _next));

    /// <summary>Restarts the sequence so each test sees the same first id.</summary>
    public void Reset() => Interlocked.Exchange(ref _next, 0);

    public static Guid IdAt(int index) => new($"aaaaaaaa-0000-4000-8000-{index:D12}");
}

/// <summary>
/// A push sender that records instead of sending.
/// </summary>
/// <remarks>
/// The whole point of <see cref="IPushSender"/> being an interface. What is
/// worth testing about notifications is not the encryption — that is measured
/// against RFC 8291's own worked example in <c>WebPushCryptoTests</c> — but
/// everything in front of it: whose device gets told, whose switch stops it,
/// and what the clock says where that person is.
///
/// <see cref="IsConfigured"/> is true, so those rules actually run. A test host
/// with no VAPID keys would exercise the one branch that does nothing.
/// </remarks>
public sealed class RecordingPushSender : IPushSender
{
    private readonly List<(string Endpoint, PushPayload Payload)> _sent = [];

    private readonly Lock _gate = new();

    public bool IsConfigured { get; set; } = true;

    /// <summary>Null when unconfigured, exactly as the real sender behaves.</summary>
    public string? PublicKey => IsConfigured
        ? "BObo0Ie7wLSMlR5YZJTPZ0jbnHKSSMg7YWJcJs-8gDMYpDgSnMLLoBiCVOx5B5WvvLZ2_a-BJlYJ0dtOWEfXhaU"
        : null;

    /// <summary>What the next attempt answers. Set it to check the failure paths.</summary>
    public PushOutcome Outcome { get; set; } = PushOutcome.Delivered;

    /// <summary>Everything this host would have sent, in order.</summary>
    public IReadOnlyList<(string Endpoint, PushPayload Payload)> Sent
    {
        get
        {
            lock (_gate)
            {
                return [.. _sent];
            }
        }
    }

    public Task<PushOutcome> SendAsync(
        PushSubscription subscription,
        PushPayload payload,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(subscription);

        lock (_gate)
        {
            _sent.Add((subscription.Endpoint, payload));
        }

        return Task.FromResult(Outcome);
    }

    public void Reset()
    {
        lock (_gate)
        {
            _sent.Clear();
        }

        IsConfigured = true;
        Outcome = PushOutcome.Delivered;
    }
}
