using Microsoft.EntityFrameworkCore;
using Q2.Api.Features.Notifications;
using Q2.Api.Features.People;
using Q2.Api.Infrastructure.Errors;
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
    TimeZoneResolver timeZones,
    ILogger<SettingsService> logger)
{
    public async Task<SettingsResponse> GetAsync(CancellationToken cancellationToken)
    {
        var settings = await LoadAsync(cancellationToken);
        var me = await currentPerson.GetAsync(cancellationToken);

        return SettingsResponse.From(settings, me, timeZones.EffectiveId(me.TimeZoneId));
    }

    public async Task<SettingsResponse> UpdateAsync(UpdateSettingsRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var settings = await LoadAsync(cancellationToken);
        var me = await currentPerson.GetAsync(cancellationToken);

        /*
         * The zone moves the days that come; it does not move a window already
         * open, whose deadline is stored as an instant — the same "from the
         * next window" rule as editing a schedule (#9, docs/adr/0044).
         */
        if (request.TimeZone is { } zone)
        {
            if (!TimeZoneResolver.IsKnown(zone.Trim()))
            {
                throw new DomainValidationException(
                    nameof(request.TimeZone),
                    "That is not a time zone this server knows.",
                    new FieldErrorCode(FieldErrorCodes.UnknownOption));
            }

            me.SetTimeZone(zone.Trim());
        }

        // Who can find this person and whether their activity shows (#106):
        // the person's own, read by everybody else's search and screens.
        if (request.Findability is not null || request.ShowActivity is not null)
        {
            me.SetPrivacy(request.Findability ?? me.Findability, request.ShowActivity ?? me.ShowsActivity);
        }

        // An omitted property keeps its current value, so a client that only
        // knows about some of the switches cannot silently reset the rest.
        settings.Update(
            request.Theme ?? settings.Theme,
            request.Language ?? settings.Language,
            Merge(settings.Notifications, request.Notifications));

        await database.SaveChangesAsync(cancellationToken);

        logger.LogInformation("Settings updated for {PersonId}", settings.PersonId);

        return SettingsResponse.From(settings, me, timeZones.EffectiveId(me.TimeZoneId));
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
            SpotChecks = change.SpotChecks ?? current.SpotChecks,
            Reminders = change.Reminders ?? current.Reminders,
            Badges = change.Badges ?? current.Badges,
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
