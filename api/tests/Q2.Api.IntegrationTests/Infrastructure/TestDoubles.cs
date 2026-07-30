using Q2.Api.Infrastructure.Time;

namespace Q2.Api.IntegrationTests.Infrastructure;

/// <summary>
/// A clock that never moves, so seeds, <c>isOverdue</c> and created timestamps
/// are all computed against the same known instant.
/// </summary>
public sealed class FixedTimeProvider(DateTimeOffset now) : TimeProvider
{
    public override DateTimeOffset GetUtcNow() => now;
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
