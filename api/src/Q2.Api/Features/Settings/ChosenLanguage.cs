using Microsoft.EntityFrameworkCore;
using Q2.Api.Infrastructure.Persistence;

namespace Q2.Api.Features.Settings;

/// <summary>
/// The language somebody chose in q2, for the few things the server words
/// itself.
/// </summary>
/// <remarks>
/// Almost everything is worded by the client, which knows the language it is
/// showing. What the server has to choose for is text it holds in both
/// languages and nobody can word afresh — a challenge's prompt (#105) — and
/// what leaves without the app around it: a push (#39), a mail.
///
/// Somebody without a settings row has the product's default, German, as in
/// the app.
/// </remarks>
public static class ChosenLanguage
{
    public const LanguagePreference Default = LanguagePreference.German;

    /// <summary>One person's language.</summary>
    public static async Task<LanguagePreference> OfAsync(
        Q2DbContext database,
        Guid personId,
        CancellationToken cancellationToken) =>
        await database.UserSettings
            .AsNoTracking()
            .Where(settings => settings.PersonId == personId)
            .Select(settings => (LanguagePreference?)settings.Language)
            .SingleOrDefaultAsync(cancellationToken) ?? Default;

    /// <summary>
    /// The languages of several people, for those who chose one. Read the
    /// answer with <c>GetValueOrDefault(id, Default)</c>.
    /// </summary>
    public static async Task<Dictionary<Guid, LanguagePreference>> OfAsync(
        Q2DbContext database,
        IReadOnlyCollection<Guid> personIds,
        CancellationToken cancellationToken) =>
        await database.UserSettings
            .AsNoTracking()
            .Where(settings => personIds.Contains(settings.PersonId))
            .ToDictionaryAsync(settings => settings.PersonId, settings => settings.Language, cancellationToken);
}
