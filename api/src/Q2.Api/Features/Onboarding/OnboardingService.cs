using Microsoft.EntityFrameworkCore;
using Q2.Api.Features.Accounts;
using Q2.Api.Features.People;
using Q2.Api.Infrastructure.Errors;
using Q2.Api.Infrastructure.Persistence;

namespace Q2.Api.Features.Onboarding;

/// <summary>
/// The server's half of the first-run introduction: the handle somebody may
/// choose while it is open, and the moment it is over.
/// </summary>
/// <remarks>
/// Everything else the introduction does — the photograph, the name, the
/// theme, the waiting Do, the invite link, the push permission — goes through
/// the endpoints that already do it. What only exists because of the
/// introduction is here (docs/adr/0039-first-run-onboarding.md).
/// </remarks>
public sealed class OnboardingService(
    Q2DbContext database,
    CurrentPerson currentPerson,
    AccountService accounts,
    TimeProvider timeProvider,
    ILogger<OnboardingService> logger)
{
    /// <summary>
    /// What <paramref name="request"/>'s handle would be, asked while typing.
    /// </summary>
    /// <remarks>
    /// Public data only: a handle is what search shows anybody, so saying it
    /// is taken says nothing search would not.
    /// </remarks>
    public async Task<HandleCheckResponse> CheckHandleAsync(HandleRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var me = await currentPerson.GetAsync(cancellationToken);
        var handle = Handles.Normalise(request.Handle);

        return new HandleCheckResponse(handle, await AvailabilityAsync(handle, me, cancellationToken));
    }

    /// <summary>
    /// Gives the caller the handle they chose, while the introduction is open.
    /// </summary>
    /// <exception cref="DomainValidationException">
    /// Not a handle a person may choose, taken, or the introduction is over.
    /// </exception>
    public async Task<SessionResponse> ChooseHandleAsync(HandleRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var me = await currentPerson.GetAsync(cancellationToken);
        var handle = Handles.Normalise(request.Handle);

        switch (await AvailabilityAsync(handle, me, cancellationToken))
        {
            case HandleAvailability.Current:
                return await accounts.GetSessionAsync(cancellationToken);

            case HandleAvailability.Taken:
                throw Taken();

            case HandleAvailability.Invalid:
                throw new DomainValidationException(nameof(request.Handle), Handles.Rule);

            default:
                break;
        }

        me.ChooseHandle(handle!);

        try
        {
            await database.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException)
        {
            // Somebody took it between the check and the save. The unique
            // index is the authority; the check above only spares it the work.
            throw Taken();
        }

        logger.LogInformation("Person {PersonId} chose their handle", me.Id);

        return await accounts.GetSessionAsync(cancellationToken);
    }

    /// <summary>
    /// Records that the introduction is behind the caller. Idempotent: a
    /// second call keeps the first moment.
    /// </summary>
    public async Task<SessionResponse> CompleteAsync(CancellationToken cancellationToken)
    {
        var me = await currentPerson.GetAsync(cancellationToken);

        if (!me.IsOnboarded)
        {
            me.CompleteOnboarding(timeProvider.GetUtcNow());
            await database.SaveChangesAsync(cancellationToken);

            logger.LogInformation("Person {PersonId} finished the introduction", me.Id);
        }

        return await accounts.GetSessionAsync(cancellationToken);
    }

    private async Task<HandleAvailability> AvailabilityAsync(
        string? handle,
        Person me,
        CancellationToken cancellationToken)
    {
        if (handle is not null && string.Equals(handle, me.Handle, StringComparison.Ordinal))
        {
            return HandleAvailability.Current;
        }

        if (!Handles.IsChoosable(handle))
        {
            return HandleAvailability.Invalid;
        }

        var taken = await database.People
            .AsNoTracking()
            .AnyAsync(person => person.Handle == handle && person.Id != me.Id, cancellationToken);

        return taken ? HandleAvailability.Taken : HandleAvailability.Available;
    }

    private static DomainValidationException Taken() =>
        new(nameof(HandleRequest.Handle), "That handle is taken.");
}
