using Microsoft.EntityFrameworkCore;
using Q2.Api.Features.Notifications;
using Q2.Api.Features.People;
using Q2.Api.Infrastructure.Persistence;
using Q2.Api.Infrastructure.Time;

namespace Q2.Api.Features.Settings;

/// <summary>
/// Reading and changing the signed-in person's preferences.
/// </summary>
/// <remarks>
/// A row is written at sign-up, and created here on first read for anybody
/// whose account predates that: a screen that failed because a row was never
/// written would be a strange way to find out.
/// </remarks>
public sealed class SettingsService(
    Q2DbContext database,
    CurrentPerson currentPerson,
    IIdGenerator idGenerator,
    ILogger<SettingsService> logger)
{
    public async Task<SettingsResponse> GetAsync(CancellationToken cancellationToken) =>
        SettingsResponse.From(await LoadAsync(cancellationToken));

    public async Task<SettingsResponse> UpdateAsync(UpdateSettingsRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var settings = await LoadAsync(cancellationToken);

        // An omitted property keeps its current value, so a client that only
        // knows about some of the switches cannot silently reset the rest.
        settings.Update(
            request.Theme ?? settings.Theme,
            request.Language ?? settings.Language,
            Merge(settings.Notifications, request.Notifications));

        await database.SaveChangesAsync(cancellationToken);

        logger.LogInformation("Settings updated for {PersonId}", settings.PersonId);

        return SettingsResponse.From(settings);
    }

    private static NotificationPreferences Merge(
        NotificationPreferences current,
        UpdateNotificationSettingsRequest? change)
    {
        if (change is null)
        {
            return current;
        }

        /*
         * "Off" and "unchanged" are both null in a nullable time, so the switch
         * decides which of the two this is. Without it, a request that left the
         * quiet hours alone would clear them.
         */
        var quiet = change.QuietHoursEnabled switch
        {
            false => (From: (TimeOnly?)null, To: (TimeOnly?)null),
            true => (
                From: change.QuietHoursFrom ?? current.QuietHoursFrom ?? QuietHours.DefaultFrom,
                To: change.QuietHoursTo ?? current.QuietHoursTo ?? QuietHours.DefaultTo),
            null => (From: current.QuietHoursFrom, To: current.QuietHoursTo),
        };

        var switches = current with
        {
            Messages = change.Messages ?? current.Messages,
            Friendships = change.Friendships ?? current.Friendships,
            VotesDue = change.VotesDue ?? current.VotesDue,
            ProofResults = change.ProofResults ?? current.ProofResults,
            Reactions = change.Reactions ?? current.Reactions,
            GoalUpdates = change.GoalUpdates ?? current.GoalUpdates,
            FriendsAtRisk = change.FriendsAtRisk ?? current.FriendsAtRisk,
            Challenge = change.Challenge ?? current.Challenge,
        };

        return switches.WithQuietHours(quiet.From, quiet.To);
    }

    private async Task<UserSettings> LoadAsync(CancellationToken cancellationToken)
    {
        var me = await currentPerson.GetAsync(cancellationToken);

        var settings = await database.UserSettings.SingleOrDefaultAsync(s => s.PersonId == me.Id, cancellationToken);

        if (settings is null)
        {
            settings = UserSettings.CreateDefault(idGenerator.NewId(), me.Id);
            database.UserSettings.Add(settings);
            await database.SaveChangesAsync(cancellationToken);
        }

        return settings;
    }
}
