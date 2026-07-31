using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using Q2.Api.Infrastructure.Errors;
using Q2.Api.Infrastructure.Persistence;

namespace Q2.Api.Features.People;

/// <summary>
/// Answers "who is asking?".
/// </summary>
/// <remarks>
/// The answer is the <see cref="Person"/> behind the signed-in account. Every
/// feature that needs an identity goes through this type rather than reading a
/// claim itself, which is what kept the change from
/// <c>Person.IsCurrentUser</c> to a real session down to this one file
/// (docs/adr/0009-single-known-person.md, superseded by
/// docs/adr/0011-authentication-with-identity.md).
///
/// It fails loudly rather than returning null. A silent fallback to "somebody"
/// would hand one person's chats to another, and that is the sort of bug that
/// is only ever noticed in production.
/// </remarks>
public sealed class CurrentPerson(Q2DbContext database, IHttpContextAccessor httpContextAccessor)
{
    private Person? _cached;

    /// <exception cref="AuthenticationRequiredException">
    /// There is no session, or nothing behind it any more.
    /// </exception>
    public async Task<Person> GetAsync(CancellationToken cancellationToken)
    {
        if (_cached is not null)
        {
            return _cached;
        }

        var principal = httpContextAccessor.HttpContext?.User;

        if (principal?.Identity?.IsAuthenticated != true)
        {
            throw new AuthenticationRequiredException("This request carries no session.");
        }

        // Identity puts the account id here. It is the account's id, not the
        // person's — the two are separate rows on purpose (see AppUser).
        if (!Guid.TryParse(principal.FindFirstValue(ClaimTypes.NameIdentifier), out var accountId))
        {
            throw new AuthenticationRequiredException("This session does not identify an account.");
        }

        var personId = await database.Users
            .AsNoTracking()
            .Where(user => user.Id == accountId)
            .Select(user => user.PersonId)
            .SingleOrDefaultAsync(cancellationToken);

        if (personId == Guid.Empty)
        {
            throw new AuthenticationRequiredException("The account behind this session no longer exists.");
        }

        // Check-ins come along because almost everything that reads the current
        // person also reads their streak, and because Person.CheckIn on a
        // person whose days were never loaded would insert a duplicate and hit
        // the unique index instead of doing nothing.
        _cached = await database.People
            .Include(p => p.CheckIns)
            .SingleOrDefaultAsync(p => p.Id == personId, cancellationToken)
            ?? throw new AuthenticationRequiredException("The person behind this session no longer exists.");

        return _cached;
    }

    /// <summary>The id alone, for the many callers that only need to compare.</summary>
    public async Task<Guid> GetIdAsync(CancellationToken cancellationToken) =>
        (await GetAsync(cancellationToken)).Id;
}
