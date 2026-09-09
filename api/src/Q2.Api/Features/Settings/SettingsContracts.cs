namespace Q2.Api.Features.Settings;

/// <summary>The preferences screen, as data.</summary>
/// <param name="QuietHoursFrom">
/// Null on both sides means quiet hours are off. They are counted in this
/// person's own zone, like every other clock in q2.
/// </param>
public sealed record SettingsResponse(
    ThemePreference Theme,
    LanguagePreference Language,
    bool NotifyReminders,
    bool NotifyKudos,
    bool NotifyMessages,
    bool NotifyWeeklyReview,
    bool NotifyChallenge,
    TimeOnly? QuietHoursFrom,
    TimeOnly? QuietHoursTo)
{
    public static SettingsResponse From(UserSettings settings) => new(
        settings.Theme,
        settings.Language,
        settings.NotifyReminders,
        settings.NotifyKudos,
        settings.NotifyMessages,
        settings.NotifyWeeklyReview,
        settings.NotifyChallenge,
        settings.QuietHoursFrom,
        settings.QuietHoursTo);
}

/// <summary>
/// Request body for changing preferences.
/// </summary>
/// <remarks>
/// A full replacement rather than a patch: there are six switches on one
/// screen, they are always all visible, and "send what changed" would buy
/// nothing except a way for two toggles in quick succession to overwrite each
/// other. Every property is nullable so an omitted one keeps its current value.
/// </remarks>
/// <param name="QuietHoursEnabled">
/// The switch, separate from the two times, because "off" and "unchanged" are
/// both <c>null</c> in a nullable field and the server cannot tell them apart
/// otherwise. False clears the window; true applies the times below.
/// </param>
public sealed record UpdateSettingsRequest(
    ThemePreference? Theme = null,
    LanguagePreference? Language = null,
    bool? NotifyReminders = null,
    bool? NotifyKudos = null,
    bool? NotifyMessages = null,
    bool? NotifyWeeklyReview = null,
    bool? NotifyChallenge = null,
    bool? QuietHoursEnabled = null,
    TimeOnly? QuietHoursFrom = null,
    TimeOnly? QuietHoursTo = null);
