using Q2.Api.Features.Settings;
using Q2.Api.Infrastructure.Mail;

namespace Q2.Api.Features.Accounts;

/// <summary>The words of the notice that a password was changed, in the language the account chose.</summary>
/// <remarks>
/// The second mail q2 writes, for the reason the first one is written on the
/// server (<see cref="PasswordResetMail"/>): it is read in a mail client, with
/// no q2 running to word it.
///
/// It exists for the person who did *not* change their password. Somebody
/// holding their open session could otherwise lock them out without a trace,
/// so the mail says what happened and what to do if it was not them: the reset
/// link, which ends every session including the intruder's. No link in the mail
/// itself — a mail that says "your password was changed" and asks for a click
/// is what phishing looks like.
/// </remarks>
public static class PasswordChangedMail
{
    public static OutgoingMail For(string to, LanguagePreference language) => language switch
    {
        LanguagePreference.English => new OutgoingMail(to, "Your Qdos password was changed", English),
        _ => new OutgoingMail(to, "Dein Qdos-Passwort wurde geändert", German),
    };

    private const string German = """
        Hallo,

        das Passwort für dein Qdos-Konto wurde eben geändert. Alle anderen Geräte sind dadurch abgemeldet.

        Warst du das, musst du nichts tun.

        Warst du es nicht, setz dein Passwort sofort über „Passwort vergessen?“ in der Anmeldung zurück. Damit endet jede Sitzung, auch die von jemand anderem.

        Qdos
        """;

    private const string English = """
        Hello,

        the password of your Qdos account was just changed. Every other device has been signed out.

        If this was you, there is nothing to do.

        If it was not, reset your password right away with "Forgot your password?" on the sign-in screen. That ends every session, including anybody else's.

        Qdos
        """;
}
