using Q2.Api.Features.Settings;
using Q2.Api.Infrastructure.Mail;

namespace Q2.Api.Features.Accounts;

/// <summary>The words of a reset mail, in the language the account chose.</summary>
/// <remarks>
/// **The one place the server writes a sentence.** Everything else the API sends
/// is structure the app words from its own catalogue
/// (docs/adr/0010-german-first-interface.md). A mail cannot work that way: it is
/// read in a mail client, long after the request, with no q2 running to compose
/// it. So its two languages live here, beside the only feature that sends one,
/// and the exception is recorded in docs/adr/0026-mail-and-password-reset.md
/// rather than left for somebody to stumble on.
///
/// No name and no greeting by name: the mail passes through a provider, and the
/// address is all it needs to carry. No emoji, as everywhere in q2.
///
/// Both texts say "one hour", which is
/// <see cref="AccountPolicy.PasswordResetLinkLifetime"/>; a unit test holds the
/// two together.
/// </remarks>
public static class PasswordResetMail
{
    public static OutgoingMail For(string to, LanguagePreference language, string link) => language switch
    {
        LanguagePreference.English => new OutgoingMail(to, "Reset your Qdos password", English(link)),
        _ => new OutgoingMail(to, "Dein Qdos-Passwort zurücksetzen", German(link)),
    };

    private static string German(string link) => $"""
        Hallo,

        jemand – hoffentlich du – möchte das Passwort für dein Qdos-Konto zurücksetzen. Mit diesem Link setzt du ein neues:

        {link}

        Der Link ist eine Stunde lang gültig und funktioniert nur einmal. Sobald das neue Passwort gesetzt ist, bist du auf allen Geräten abgemeldet.

        Wenn du das nicht warst, ignoriere diese E-Mail einfach. Dein Passwort bleibt dann, wie es ist.

        Qdos
        """;

    private static string English(string link) => $"""
        Hello,

        someone – hopefully you – asked to reset the password of your Qdos account. This link lets you set a new one:

        {link}

        The link works for one hour, and only once. As soon as the new password is set, you are signed out on every device.

        If this was not you, just ignore this email. Your password stays as it is.

        Qdos
        """;
}
