using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace Q2.Api.Infrastructure.Persistence;

/// <summary>
/// How an instant is stored.
/// </summary>
/// <remarks>
/// The domain keeps <see cref="DateTimeOffset"/> — it carries the offset
/// explicitly, which is the right type for an instant — but the column is a
/// plain UTC <see cref="DateTime"/>.
///
/// This is not cosmetic. SQLite cannot order by a <see cref="DateTimeOffset"/>
/// at all ("SQLite does not support expressions of type 'DateTimeOffset' in
/// ORDER BY clauses"), so listing anything newest-first would fail at runtime.
/// Storing UTC also maps cleanly onto PostgreSQL's <c>timestamp with time
/// zone</c> later. See docs/adr/0004-sqlite-first.md.
///
/// One converter shared by every entity, so no table can quietly get this
/// wrong: a second copy is how half a schema ends up ordering correctly and
/// the other half throwing on the first <c>ORDER BY</c>.
/// </remarks>
public static class InstantConversion
{
    public static readonly ValueConverter<DateTimeOffset, DateTime> Required = new(
        value => value.UtcDateTime,
        value => new DateTimeOffset(DateTime.SpecifyKind(value, DateTimeKind.Utc)));

    public static readonly ValueConverter<DateTimeOffset?, DateTime?> Optional = new(
        value => value == null ? null : value.Value.UtcDateTime,
        value => value == null ? null : new DateTimeOffset(DateTime.SpecifyKind(value.Value, DateTimeKind.Utc)));
}
