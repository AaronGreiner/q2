namespace Q2.Api.Features.Settings;

/// <summary>The preferences screen, as data.</summary>
public sealed record SettingsResponse(
    ThemePreference Theme,
    LanguagePreference Language,
    bool NotifyReminders,
    bool NotifyKudos,
    bool NotifyMessages,
    bool NotifyWeeklyReview)
{
    public static SettingsResponse From(UserSettings settings) => new(
        settings.Theme,
        settings.Language,
        settings.NotifyReminders,
        settings.NotifyKudos,
        settings.NotifyMessages,
        settings.NotifyWeeklyReview);
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
public sealed record UpdateSettingsRequest(
    ThemePreference? Theme = null,
    LanguagePreference? Language = null,
    bool? NotifyReminders = null,
    bool? NotifyKudos = null,
    bool? NotifyMessages = null,
    bool? NotifyWeeklyReview = null);
