using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Q2.Api.Features.People;
using Q2.Api.Features.Settings;
using Q2.Api.Infrastructure.Errors;
using Q2.Api.Infrastructure.Persistence;
using Q2.Api.Infrastructure.Time;

namespace Q2.Api.Features.Accounts;

/// <summary>
/// Creating an account, signing in, signing out, and saying who is signed in.
/// </summary>
/// <remarks>
/// Nothing about authentication is implemented here. The password hash, the
/// security stamp, lockout and the session cookie are all ASP.NET Core
/// Identity's, through <see cref="UserManager{TUser}"/> and
/// <see cref="SignInManager{TUser}"/> — which is the whole reason a library is
/// used for this (docs/adr/0006-authentication-deferred.md, "do not implement
/// password hashing, session management or token issuing by hand").
///
/// What this class does own is the q2 side of an account: the
/// <see cref="Person"/> it signs in as, that person's handle, and the
/// preferences row every screen expects to find.
/// </remarks>
public sealed class AccountService(
    Q2DbContext database,
    UserManager<AppUser> users,
    SignInManager<AppUser> signIn,
    CurrentPerson currentPerson,
    IIdGenerator idGenerator,
    TimeProvider timeProvider,
    ILogger<AccountService> logger)
{
    /// <summary>
    /// Creates an account and signs it in.
    /// </summary>
    /// <remarks>
    /// Three rows in one transaction: the person, the account that points at
    /// them, and their preferences. A half-created account — credentials with
    /// no person behind them — would fail on the next request with no way for
    /// its owner to repair it.
    /// </remarks>
    /// <exception cref="DomainValidationException">The request is not usable.</exception>
    public async Task<SessionResponse> RegisterAsync(RegisterRequest request, CancellationToken cancellationToken)
    {
        var errors = AccountRequestValidator.Validate(request);

        if (errors.Count > 0)
        {
            throw new DomainValidationException(errors);
        }

        var name = request.Name!.Trim();
        var email = request.Email!.Trim();
        var now = timeProvider.GetUtcNow();

        await using var transaction = await database.Database.BeginTransactionAsync(cancellationToken);

        var handle = await ReserveHandleAsync(name, cancellationToken);

        var person = Person.Create(
            idGenerator.NewId(),
            name,
            handle,
            ProfileDefaults.Initials(name),
            ProfileDefaults.AvatarColor(handle));

        // Signing up is being here, and "online" is what the friends screen
        // draws a green dot from.
        person.SetLastSeen(now);

        database.People.Add(person);
        await database.SaveChangesAsync(cancellationToken);

        var account = new AppUser
        {
            Id = idGenerator.NewId(),
            PersonId = person.Id,

            // The address is the credential; there is no separate user name to
            // invent, forget and then be unable to sign in with.
            UserName = email,
            Email = email,
        };

        var created = await users.CreateAsync(account, request.Password!);

        if (!created.Succeeded)
        {
            await transaction.RollbackAsync(cancellationToken);
            throw new DomainValidationException(ToFieldErrors(created));
        }

        // Created here rather than lazily on first read: there is a sign-up
        // now, which is exactly the moment the row was always missing from.
        database.UserSettings.Add(UserSettings.CreateDefault(idGenerator.NewId(), person.Id));
        await database.SaveChangesAsync(cancellationToken);

        await transaction.CommitAsync(cancellationToken);

        await signIn.SignInAsync(account, isPersistent: true);

        // The id, never the name or the address: both are personal data
        // (docs/privacy.md).
        logger.LogInformation("Account created for person {PersonId}", person.Id);

        return new SessionResponse(PersonSummary.From(person, now), email);
    }

    /// <exception cref="DomainValidationException">The request is not usable.</exception>
    /// <exception cref="AuthenticationRequiredException">The credentials were refused.</exception>
    public async Task<SessionResponse> LoginAsync(LoginRequest request, CancellationToken cancellationToken)
    {
        var errors = AccountRequestValidator.Validate(request);

        if (errors.Count > 0)
        {
            throw new DomainValidationException(errors);
        }

        var email = request.Email!.Trim();
        var account = await users.FindByEmailAsync(email);

        // Same answer for "no such account" and "wrong password". A different
        // one would turn this endpoint into a way to find out who has an
        // account here.
        if (account is null)
        {
            throw InvalidCredentials();
        }

        var result = await signIn.PasswordSignInAsync(
            account,
            request.Password!,
            isPersistent: true,

            // Counts failures, so a password cannot be guessed at machine
            // speed. The lockout is temporary and per account.
            lockoutOnFailure: true);

        if (result.IsLockedOut)
        {
            logger.LogWarning("Sign-in refused: account locked out.");
            throw new AuthenticationRequiredException(
                "Too many failed attempts. Try again later.",
                AuthenticationFailures.LockedOut);
        }

        if (!result.Succeeded)
        {
            throw InvalidCredentials();
        }

        var now = timeProvider.GetUtcNow();
        var person = await database.People.SingleOrDefaultAsync(p => p.Id == account.PersonId, cancellationToken)
            ?? throw new AuthenticationRequiredException("This account has no person behind it.");

        person.SetLastSeen(now);
        await database.SaveChangesAsync(cancellationToken);

        logger.LogInformation("Signed in as person {PersonId}", person.Id);

        return new SessionResponse(PersonSummary.From(person, now), account.Email ?? email);
    }

    /// <summary>
    /// Ends the session.
    /// </summary>
    /// <remarks>
    /// Succeeds whether or not anybody was signed in. Signing out is the thing
    /// somebody does when they are unsure of their state, and an error there
    /// would leave them with the cookie they were trying to get rid of.
    /// </remarks>
    public async Task LogoutAsync()
    {
        await signIn.SignOutAsync();
        logger.LogInformation("Session ended.");
    }

    /// <exception cref="AuthenticationRequiredException">There is no usable session.</exception>
    public async Task<SessionResponse> GetSessionAsync(CancellationToken cancellationToken)
    {
        var person = await currentPerson.GetAsync(cancellationToken);
        var now = timeProvider.GetUtcNow();

        var email = await database.Users
            .AsNoTracking()
            .Where(u => u.PersonId == person.Id)
            .Select(u => u.Email)
            .SingleOrDefaultAsync(cancellationToken);

        return new SessionResponse(PersonSummary.From(person, now), email ?? string.Empty);
    }

    private static AuthenticationRequiredException InvalidCredentials() =>
        new("The email address or password is incorrect.", AuthenticationFailures.InvalidCredentials);

    /// <summary>
    /// Finds a handle nobody has yet.
    /// </summary>
    /// <remarks>
    /// Two people called Lena Schmidt both derive "@lena.schmidt", so the
    /// second one becomes "@lena.schmidt-2". The unique index on the column is
    /// still the authority — this only avoids reaching it in the ordinary case.
    /// </remarks>
    private async Task<string> ReserveHandleAsync(string name, CancellationToken cancellationToken)
    {
        var body = ProfileDefaults.HandleBody(name);

        // One query rather than one per attempt. The body contains only
        // [a-z0-9.] by construction, so it carries no LIKE wildcard.
        var taken = await database.People
            .AsNoTracking()
            .Where(p => p.Handle.StartsWith($"@{body}"))
            .Select(p => p.Handle)
            .ToListAsync(cancellationToken);

        var used = taken.ToHashSet(StringComparer.OrdinalIgnoreCase);

        for (var attempt = 1; ; attempt++)
        {
            var candidate = ProfileDefaults.Handle(body, attempt);

            if (!used.Contains(candidate))
            {
                return candidate;
            }
        }
    }

    /// <summary>
    /// Turns Identity's error codes into the field-level shape every other
    /// rejected request in q2 has.
    /// </summary>
    private static Dictionary<string, string[]> ToFieldErrors(IdentityResult result)
    {
        var errors = new Dictionary<string, List<string>>();

        foreach (var error in result.Errors)
        {
            var field = error.Code switch
            {
                var code when code.StartsWith("Password", StringComparison.Ordinal) => nameof(RegisterRequest.Password),
                var code when code.Contains("Email", StringComparison.Ordinal) => nameof(RegisterRequest.Email),
                var code when code.Contains("UserName", StringComparison.Ordinal) => nameof(RegisterRequest.Email),
                _ => nameof(RegisterRequest.Email),
            };

            errors.TryAdd(field, []);
            errors[field].Add(error.Description);
        }

        return errors.ToDictionary(entry => entry.Key, entry => entry.Value.ToArray());
    }
}
