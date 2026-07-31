using Microsoft.EntityFrameworkCore;
using Q2.Api.Infrastructure.Persistence;

namespace Q2.Api.Features.People;

/// <summary>
/// Answers "who is asking?".
/// </summary>
/// <remarks>
/// There is no authentication yet
/// (docs/adr/0006-authentication-deferred.md), so the answer is always the one
/// <see cref="Person"/> flagged <see cref="Person.IsCurrentUser"/>. Every
/// feature that needs an identity goes through this type rather than looking
/// the flag up itself, which means the day a request actually carries a
/// signed-in user, exactly one implementation changes.
///
/// It fails loudly when the flag is missing or ambiguous. A silent
/// <c>FirstOrDefault</c> would hand somebody else's chats to whoever asked
/// first, and that is the sort of bug that is only noticed in production.
/// </remarks>
public sealed class CurrentPerson(Q2DbContext database)
{
    private Person? _cached;

    /// <exception cref="InvalidOperationException">
    /// The database does not describe exactly one current user.
    /// </exception>
    public async Task<Person> GetAsync(CancellationToken cancellationToken)
    {
        if (_cached is not null)
        {
            return _cached;
        }

        // Check-ins come along because almost everything that reads the current
        // person also reads their streak, and because Person.CheckIn on a
        // person whose days were never loaded would insert a duplicate and hit
        // the unique index instead of doing nothing.
        var candidates = await database.People
            .Include(p => p.CheckIns)
            .Where(p => p.IsCurrentUser)
            .Take(2)
            .ToListAsync(cancellationToken);

        _cached = candidates.Count switch
        {
            1 => candidates[0],
            0 => throw new InvalidOperationException(
                "No person is marked as the current user. The database has not been seeded "
                + "(see api/README.md) — every read needs somebody to read it as."),
            _ => throw new InvalidOperationException(
                "More than one person is marked as the current user. Exactly one row may carry "
                + "Person.IsCurrentUser; see docs/adr/0009-single-known-person.md."),
        };

        return _cached;
    }

    /// <summary>The id alone, for the many callers that only need to compare.</summary>
    public async Task<Guid> GetIdAsync(CancellationToken cancellationToken) =>
        (await GetAsync(cancellationToken)).Id;
}
