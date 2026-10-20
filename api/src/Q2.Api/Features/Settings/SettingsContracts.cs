using Q2.Api.Features.Notifications;

namespace Q2.Api.Features.Settings;

/// <summary>The preferences screen, as data.</summary>
/// <param name="Notifications">
/// What may reach this person's devices and when not — the screen under
/// Profil → Einstellungen → Benachrichtigungen.
/// </param>
/// <param name="TimeZone">
/// The zone this person's days are counted in (#9): every deadline, the
/// streak's day and the quiet hours.
/// </param>
public sealed record SettingsResponse(
    ThemePreference Theme,
    LanguagePreference Language,
    NotificationSettingsResponse Notifications,
    string TimeZone)
{
    public static SettingsResponse From(UserSettings settings, string timeZone)
    {
        ArgumentNullException.ThrowIfNull(settings);

        return new SettingsResponse(
            settings.Theme,
            settings.Language,
            NotificationSettingsResponse.From(settings.Notifications),
            timeZone);
    }
}

/// <summary>One switch per thing a person can decide to be interrupted for, and the quiet hours.</summary>
/// <remarks>
/// The switches govern what may interrupt — a push, or a banner in the open
/// app — never the bell: a line the bell keeps is there whatever they say, the
/// same way the message switch never took a message out of a chat
/// ([0024](../../../../docs/adr/0024-one-notification-pipeline.md),
/// [0025](../../../../docs/adr/0025-banners-in-the-open-app.md)).
/// </remarks>
/// <param name="QuietHoursFrom">
/// Null on both sides means quiet hours are off. They are counted in this
/// person's own zone, like every other clock in q2.
/// </param>
public sealed record NotificationSettingsResponse(
    bool Messages,
    bool Friendships,
    bool VotesDue,
    bool ProofResults,
    bool Reactions,
    bool GoalUpdates,
    bool FriendsAtRisk,
    bool Challenge,
    TimeOnly? QuietHoursFrom,
    TimeOnly? QuietHoursTo,
    bool SpotChecks,
    bool Reminders)
{
    public static NotificationSettingsResponse From(NotificationPreferences preferences)
    {
        ArgumentNullException.ThrowIfNull(preferences);

        return new NotificationSettingsResponse(
            preferences.Messages,
            preferences.Friendships,
            preferences.VotesDue,
            preferences.ProofResults,
            preferences.Reactions,
            preferences.GoalUpdates,
            preferences.FriendsAtRisk,
            preferences.Challenge,
            preferences.QuietHoursFrom,
            preferences.QuietHoursTo,
            preferences.SpotChecks,
            preferences.Reminders);
    }
}

/// <summary>
/// Request body for changing preferences.
/// </summary>
/// <remarks>
/// Every property is nullable, and an omitted one keeps its current value — at
/// every level, so a client that only sends the one switch that was tapped
/// cannot reset the others. That is what lets two switches tapped in quick
/// succession both stick.
/// </remarks>
/// <param name="TimeZone">
/// An IANA zone, "Europe/Lisbon". One the server does not know is refused
/// rather than counted in UTC. A window already open keeps its deadline; the
/// new zone counts from the next one (#9).
/// </param>
public sealed record UpdateSettingsRequest(
    ThemePreference? Theme = null,
    LanguagePreference? Language = null,
    UpdateNotificationSettingsRequest? Notifications = null,
    string? TimeZone = null);

/// <summary>The notification half of <see cref="UpdateSettingsRequest"/>.</summary>
/// <param name="QuietHoursEnabled">
/// The switch, separate from the two times, because "off" and "unchanged" are
/// both <c>null</c> in a nullable field and the server cannot tell them apart
/// otherwise. False clears the window; true applies the times below.
/// </param>
public sealed record UpdateNotificationSettingsRequest(
    bool? Messages = null,
    bool? Friendships = null,
    bool? VotesDue = null,
    bool? ProofResults = null,
    bool? Reactions = null,
    bool? GoalUpdates = null,
    bool? FriendsAtRisk = null,
    bool? Challenge = null,
    bool? QuietHoursEnabled = null,
    TimeOnly? QuietHoursFrom = null,
    TimeOnly? QuietHoursTo = null,
    bool? SpotChecks = null,
    bool? Reminders = null);
