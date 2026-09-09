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
/// The notification switches were stored and honoured by nothing for four
/// stages, because a setting has to survive the day it starts working. That day
/// is [0023](../../../../docs/adr/0023-web-push.md): every one of them now
/// decides whether something is delivered, and the quiet hours below decide
/// when.
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

    /// <summary>Reminders for tasks that are due.</summary>
    public bool NotifyReminders { get; private set; } = true;

    /// <summary>Somebody gave kudos or reacted.</summary>
    public bool NotifyKudos { get; private set; } = true;

    /// <summary>A new chat message.</summary>
    public bool NotifyMessages { get; private set; } = true;

    /// <summary>The Sunday summary of the week.</summary>
    public bool NotifyWeeklyReview { get; private set; }

    /// <summary>Today's challenge, when it is published.</summary>
    /// <remarks>
    /// Its own switch rather than part of <see cref="NotifyReminders"/>, which
    /// is about goals. Somebody who has turned off "you are about to miss
    /// something" has said something specific, and taking the one cheerful
    /// notification in q2 away with it would be reading more into that than
    /// they said.
    /// </remarks>
    public bool NotifyChallenge { get; private set; } = true;

    /// <summary>
    /// When to stop delivering, in this person's own zone. Null on both sides
    /// means quiet hours are off.
    /// </summary>
    /// <remarks>
    /// **On by default**, and that is not a neutral choice: a product that has
    /// to be told not to buzz at three in the morning has already buzzed at
    /// three in the morning for everybody who never opened this screen. See
    /// <see cref="QuietHours"/> for what the window means when it crosses
    /// midnight, and for why a caught notification is dropped rather than held.
    /// </remarks>
    public TimeOnly? QuietHoursFrom { get; private set; } = QuietHours.DefaultFrom;

    /// <inheritdoc cref="QuietHoursFrom"/>
    public TimeOnly? QuietHoursTo { get; private set; } = QuietHours.DefaultTo;

    public static UserSettings CreateDefault(Guid id, Guid personId) => new(id, personId);

    public void Update(
        ThemePreference theme,
        LanguagePreference language,
        bool notifyReminders,
        bool notifyKudos,
        bool notifyMessages,
        bool notifyWeeklyReview,
        bool notifyChallenge,
        TimeOnly? quietHoursFrom,
        TimeOnly? quietHoursTo)
    {
        Theme = theme;
        Language = language;
        NotifyReminders = notifyReminders;
        NotifyKudos = notifyKudos;
        NotifyMessages = notifyMessages;
        NotifyWeeklyReview = notifyWeeklyReview;
        NotifyChallenge = notifyChallenge;

        // Both or neither. One half of a window is not a window, and storing it
        // would leave QuietHours.Covers deciding what half of one means.
        QuietHoursFrom = quietHoursFrom is { } from && quietHoursTo is { } ? from : null;
        QuietHoursTo = quietHoursFrom is not null && quietHoursTo is { } to ? to : null;
    }
}
