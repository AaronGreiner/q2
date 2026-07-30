namespace Q2.Api.Infrastructure.Time;

/// <summary>
/// Source of new entity identifiers.
/// </summary>
/// <remarks>
/// The domain never calls <see cref="Guid.NewGuid"/> itself — identifiers are
/// always passed in. That keeps <c>Goal.Create</c> a pure function and lets
/// seeds and tests use fixed, well-known ids.
/// Time works the same way through the built-in <see cref="TimeProvider"/>.
/// </remarks>
public interface IIdGenerator
{
    Guid NewId();
}

/// <summary>
/// Produces UUID v7 values: random, but ordered by creation time, which keeps
/// index locality reasonable when these become primary keys in PostgreSQL.
/// </summary>
public sealed class SequentialIdGenerator : IIdGenerator
{
    public Guid NewId() => Guid.CreateVersion7();
}
