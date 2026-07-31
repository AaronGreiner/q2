using Microsoft.EntityFrameworkCore;
using Q2.Api.Features.People;
using Q2.Api.Infrastructure.Persistence;
using Q2.Api.Infrastructure.Time;

namespace Q2.Api.Features.Settings;

/// <summary>
/// Reading and changing the signed-in person's preferences.
/// </summary>
/// <remarks>
/// Missing settings are created on first read rather than at sign-up: there is
/// no sign-up yet, and a screen that fails because a row was never written
/// would be a strange way to find that out.
/// </remarks>
public sealed class SettingsService(
    Q2DbContext database,
    CurrentPerson currentPerson,
    IIdGenerator idGenerator,
    ILogger<SettingsService> logger)
{
    public async Task<SettingsResponse> GetAsync(CancellationToken cancellationToken) =>
        SettingsResponse.From(await LoadAsync(cancellationToken));

    public async Task<SettingsResponse> UpdateAsync(UpdateSettingsRequest request, CancellationToken cancellationToken)
    {
        var settings = await LoadAsync(cancellationToken);

        // An omitted property keeps its current value, so a client that only
        // knows about four switches cannot silently reset the fifth.
        settings.Update(
            request.Theme ?? settings.Theme,
            request.Language ?? settings.Language,
            request.NotifyReminders ?? settings.NotifyReminders,
            request.NotifyKudos ?? settings.NotifyKudos,
            request.NotifyMessages ?? settings.NotifyMessages,
            request.NotifyWeeklyReview ?? settings.NotifyWeeklyReview);

        await database.SaveChangesAsync(cancellationToken);

        logger.LogInformation("Settings updated for {PersonId}", settings.PersonId);

        return SettingsResponse.From(settings);
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
