using Q2.Api.Features.Notifications;

namespace Q2.Api.Features.Settings;

/// <summary>Which colour scheme to use.</summary>
public enum ThemePreference
{
    /// <summary>Follow the operating system.</summary>
    System,

    Light,

    Dark,
}

/// <summary>The languages the interface is available in.</summary>
public enum LanguagePreference
{
    German,

    English,
}

/// <summary>
/// One person's preferences.
/// </summary>
/// <remarks>
/// Kept on the server rather than only in the browser: q2 ships as a phone app
/// alongside the web build, and a preference that lives in one device's local
/// storage is a preference the other device does not have. The client still
/// applies the theme immediately and does not wait for a round trip — the
/// server is where it is remembered, not where it is decided.
///
/// What may reach this person's devices is one value,
/// <see cref="Notifications"/>, rather than a row of switches beside the theme:
/// it is one decision about one thing, and the notification pipeline is what
/// reads it (<see cref="NotificationRules.ShouldPush"/>). Every switch in it now
/// does something — the three that were stored and honoured by nothing for
/// four stages are either wired up or gone
/// ([0024](../../../../docs/adr/0024-one-notification-pipeline.md)).
/// </remarks>
public sealed class UserSettings
{
    // EF Core materialisation only.
    private UserSettings()
    {
    }

    private UserSettings(Guid id, Guid personId)
    {
        Id = id;
        PersonId = personId;
    }

    public Guid Id { get; private set; }

    public Guid PersonId { get; private set; }

    /// <summary>Which colour scheme this person sees.</summary>
    /// <remarks>
    /// Dark rather than System, because dark is not a preference in q2, it is
    /// the design: photographs are the material the product is made of, and the
    /// surface around them is black so that they are the only thing with colour
    /// on the screen. Light stays fully supported and one tap away — it is what
    /// somebody reads in sunlight — but it is the alternative, not the neutral
    /// starting point, and "System" would make the first launch a coin toss.
    /// </remarks>
    public ThemePreference Theme { get; private set; } = ThemePreference.Dark;

    public LanguagePreference Language { get; private set; } = LanguagePreference.German;

    /// <summary>What may reach this person's devices, and when nothing may.</summary>
    public NotificationPreferences Notifications { get; private set; } = NotificationPreferences.Default;

    public static UserSettings CreateDefault(Guid id, Guid personId) => new(id, personId);

    public void Update(ThemePreference theme, LanguagePreference language, NotificationPreferences notifications)
    {
        ArgumentNullException.ThrowIfNull(notifications);

        Theme = theme;
        Language = language;

        // Both ends of the quiet window or neither, whatever the caller built —
        // see NotificationPreferences.WithQuietHours.
        Notifications = notifications.WithQuietHours(notifications.QuietHoursFrom, notifications.QuietHoursTo);
    }
}
