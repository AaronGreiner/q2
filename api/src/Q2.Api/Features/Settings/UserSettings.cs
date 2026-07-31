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
/// Notification switches are stored and honoured by nothing yet; there is no
/// notification delivery in this version. They exist because the setting has to
/// survive the day it starts working, and an empty screen would have been the
/// dishonest alternative.
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

    public ThemePreference Theme { get; private set; } = ThemePreference.System;

    public LanguagePreference Language { get; private set; } = LanguagePreference.German;

    /// <summary>Reminders for tasks that are due.</summary>
    public bool NotifyReminders { get; private set; } = true;

    /// <summary>Somebody gave kudos or reacted.</summary>
    public bool NotifyKudos { get; private set; } = true;

    /// <summary>A new chat message.</summary>
    public bool NotifyMessages { get; private set; } = true;

    /// <summary>The Sunday summary of the week.</summary>
    public bool NotifyWeeklyReview { get; private set; }

    public static UserSettings CreateDefault(Guid id, Guid personId) => new(id, personId);

    public void Update(
        ThemePreference theme,
        LanguagePreference language,
        bool notifyReminders,
        bool notifyKudos,
        bool notifyMessages,
        bool notifyWeeklyReview)
    {
        Theme = theme;
        Language = language;
        NotifyReminders = notifyReminders;
        NotifyKudos = notifyKudos;
        NotifyMessages = notifyMessages;
        NotifyWeeklyReview = notifyWeeklyReview;
    }
}
