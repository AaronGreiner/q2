using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Q2.Api.Features.Chats;
using Q2.Api.Features.Images;
using Q2.Api.Features.People;
using Q2.Api.Features.Settings;
using Q2.Api.Infrastructure.Errors;
using Q2.Api.Infrastructure.Observability;
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
    ImageService images,
    InviteService invites,
    IIdGenerator idGenerator,
    TimeProvider timeProvider,
    TimeZoneResolver timeZones,
    Q2Metrics metrics,
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

        // The deployment's zone, written onto the person rather than left
        // implicit. Every deadline this account ever gets is counted in it, and
        // "whatever the server thought at the time" is not something a streak
        // should depend on.
        person.SetTimeZone(timeZones.ConfiguredZoneId);

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

        /*
         * The first friendship, if they arrived through somebody's link.
         *
         * Inside the same transaction as the account, so nobody ends up as an
         * account with nobody — which is the exact state the link exists to
         * prevent, and would be a miserable one to land in because a second
         * request failed.
         */
        await invites.RedeemAsync(request.InviteCode, person, cancellationToken);

        await database.SaveChangesAsync(cancellationToken);

        await transaction.CommitAsync(cancellationToken);

        await signIn.SignInAsync(account, isPersistent: true);

        // The id, never the name or the address: both are personal data
        // (docs/privacy.md).
        logger.LogInformation("Account created for person {PersonId}", person.Id);
        metrics.CountAccountRegistered();

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
        metrics.CountAccountSignedIn();

        return new SessionResponse(PersonSummary.From(person, now), account.Email ?? email);
    }

    /// <summary>
    /// Deletes an account and everything personal behind it.
    /// </summary>
    /// <remarks>
    /// The one irreversible thing in q2, and the one with a legal deadline
    /// rather than a product argument behind it: Art. 17 over a face
    /// ([privacy.md](../../../../docs/privacy.md)).
    ///
    /// **The password is asked for again.** A session cookie authorises reading
    /// somebody's screens; it does not authorise erasing their year from a
    /// borrowed phone.
    ///
    /// Three decisions are worth reading before changing anything here:
    ///
    /// 1. **No tombstone.** The person's row goes, and every foreign key
    ///    pointing at it cascades. The alternative — an anonymised row kept so
    ///    other tables still resolve — leaves a shape of somebody in the
    ///    database after they asked to be gone, and it is the kind of
    ///    half-erasure that is defended rather than explained.
    /// 2. **Direct conversations go with them.** A direct thread was between
    ///    the two of them and one of them no longer exists; there is precedent
    ///    for the same trade in
    ///    [0020](../../../../docs/adr/0020-pause-and-archive.md), where
    ///    deleting a goal takes its chat. **Groups stay**, minus this person's
    ///    messages: a group is other people's conversation as well.
    /// 3. **Counters are repaired, not left stale.** Kudos this person gave
    ///    cascade away, so the totals they contributed to are decremented
    ///    first — otherwise every friend keeps a number that no longer counts
    ///    anything, and a derived-not-stored product would be storing a lie.
    ///
    /// The image bytes are last and outside the transaction, because they are
    /// not transactional: a file removed before the rows are committed comes
    /// back as a broken picture if the commit fails. A half-finished attempt
    /// leaves unreferenced bytes on disk, which is the safe direction and is
    /// why the store is safe to sweep separately.
    /// </remarks>
    /// <exception cref="DomainValidationException">No password was given.</exception>
    /// <exception cref="AuthenticationRequiredException">The password is wrong.</exception>
    public async Task<AccountDeletionResponse> DeleteAsync(
        DeleteAccountRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (string.IsNullOrWhiteSpace(request.Password))
        {
            throw new DomainValidationException(
                nameof(request.Password),
                "Your password is required to delete your account.");
        }

        var me = await currentPerson.GetAsync(cancellationToken);

        var account = await database.Users.SingleOrDefaultAsync(
            user => user.PersonId == me.Id,
            cancellationToken)
            ?? throw new AuthenticationRequiredException("This session has no account behind it.");

        // Not PasswordSignInAsync: this must not create a session, move the
        // lockout counter or touch the sign-in metrics. It is a check, not a
        // sign-in.
        if (!await users.CheckPasswordAsync(account, request.Password))
        {
            throw new AuthenticationRequiredException(
                "That password is not correct.",
                AuthenticationFailures.InvalidCredentials);
        }

        var summary = await ErasePersonalDataAsync(me.Id, cancellationToken);

        database.Users.Remove(account);
        database.People.Remove(me);
        await database.SaveChangesAsync(cancellationToken);

        // Bytes last, and outside the transaction — see the remarks.
        await images.DeleteBytesAsync(summary.ImageIds, cancellationToken);

        // The session belongs to an account that no longer exists; leaving the
        // cookie in place would mean every later request fails as "the person
        // behind this session no longer exists" instead of as signed out.
        await signIn.SignOutAsync();

        // Counts only. Everything that could identify the person is the thing
        // that was just deleted.
        logger.LogInformation(
            "An account was deleted: {GoalCount} goal(s), {ImageCount} image(s), {ConversationCount} conversation(s)",
            summary.Goals,
            summary.ImageIds.Count,
            summary.Conversations);

        return new AccountDeletionResponse(
            summary.Goals,
            summary.ImageIds.Count,
            summary.Conversations,
            summary.Messages,
            summary.ChallengeEntries);
    }

    /// <summary>What one erasure removed, gathered as it goes.</summary>
    private sealed record ErasureSummary(
        int Goals,
        IReadOnlyList<Guid> ImageIds,
        int Conversations,
        int Messages,
        int ChallengeEntries);

    /// <summary>
    /// Removes everything that is this person's, without saving.
    /// </summary>
    /// <remarks>
    /// Everything here is either something a cascade cannot express — a whole
    /// direct conversation, a counter on somebody else's row — or something
    /// outside the database. What the cascades already handle is deliberately
    /// not repeated: goals, windows, proofs, check-ins, badges, friendships,
    /// blocks, activity and the reports this person filed all go with the
    /// person row, and listing them again here would be a second place to keep
    /// in step with the schema.
    /// </remarks>
    private async Task<ErasureSummary> ErasePersonalDataAsync(Guid personId, CancellationToken cancellationToken)
    {
        /*
         * The kudos this person gave, before the rows cascade away.
         *
         * Both counters are stored rather than derived — ActivityEvent.KudosCount
         * and Person.KudosReceived — so a cascade would leave every friend they
         * ever cheered with a number counting something that is gone.
         */
        var given = await database.ActivityKudos
            .AsNoTracking()
            .Where(kudos => kudos.PersonId == personId)
            .Select(kudos => kudos.ActivityEventId)
            .ToListAsync(cancellationToken);

        if (given.Count > 0)
        {
            var events = await database.ActivityEvents
                .Include(activityEvent => activityEvent.Kudos)
                .Where(activityEvent => given.Contains(activityEvent.Id))
                .ToListAsync(cancellationToken);

            var actors = await database.People
                .Where(person => events.Select(e => e.ActorPersonId).Contains(person.Id))
                .ToDictionaryAsync(person => person.Id, cancellationToken);

            foreach (var activityEvent in events)
            {
                activityEvent.WithdrawKudos(personId);
                actors.GetValueOrDefault(activityEvent.ActorPersonId)?.WithdrawKudos();
            }
        }

        /*
         * Their pictures: the rows now, the bytes after the commit.
         *
         * Removed explicitly, and this is the one place in the erasure where
         * that is load-bearing rather than tidy: `Images` deliberately has no
         * foreign key to `Person` — the row records that a file exists and who
         * may read it, and the bytes live outside any transaction
         * ([0017](../../../../docs/adr/0017-image-storage.md)). Nothing
         * cascades here, so an erasure that trusted the database would leave
         * somebody's photographs behind. A test says so.
         */
        var pictures = await database.Images
            .Where(image => image.OwnerPersonId == personId)
            .ToListAsync(cancellationToken);

        var imageIds = pictures.Select(image => image.Id).ToList();
        database.Images.RemoveRange(pictures);

        var challengeEntries = await database.ChallengeEntries
            .CountAsync(entry => entry.PersonId == personId, cancellationToken);

        var goals = await database.Goals
            .CountAsync(goal => goal.OwnerPersonId == personId, cancellationToken);

        /*
         * Direct conversations whole; group conversations only this person's
         * messages.
         *
         * A direct thread had two people in it and one is leaving for good, so
         * there is nothing left to keep. A group is somebody else's
         * conversation too, and removing it would take a thread away from
         * everybody still in it.
         */
        var direct = await database.Conversations
            .Include(conversation => conversation.Participants)
            .Include(conversation => conversation.Messages)
            .Where(conversation => conversation.Kind == ConversationKind.Direct
                && conversation.Participants.Any(participant => participant.PersonId == personId))
            .ToListAsync(cancellationToken);

        var messages = direct.Sum(conversation => conversation.Messages.Count)
            + await database.ChatMessages
                .CountAsync(message => message.SenderPersonId == personId
                    && !direct.Select(conversation => conversation.Id).Contains(message.ConversationId),
                    cancellationToken);

        database.Conversations.RemoveRange(direct);

        return new ErasureSummary(goals, imageIds, direct.Count, messages, challengeEntries);
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
