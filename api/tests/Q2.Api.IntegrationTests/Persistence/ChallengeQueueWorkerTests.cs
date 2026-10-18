using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Q2.Api.Features.Challenges;
using Q2.Api.Features.Settings;
using Q2.Api.Infrastructure.Persistence.Seeding;
using Q2.Api.IntegrationTests.Infrastructure;

namespace Q2.Api.IntegrationTests.Persistence;

/// <summary>
/// The job that keeps a week of prompts queued.
/// </summary>
/// <remarks>
/// It is not registered in <c>AutomatedTest</c> — a job writing between an
/// arrange and an assert would make every other test in the suite flaky — so
/// these drive it directly. What is worth proving here rather than in
/// <c>ChallengeQueueTests</c> is the part the pure planner cannot see: that the
/// rows reach the database, that a second pass writes nothing, and that the
/// prompts really do come out of configuration.
/// </remarks>
[Trait("Category", "Integration")]
public class ChallengeQueueWorkerTests(Q2ApiFactory factory) : ApiTestBase(factory)
{
    private ChallengeQueueWorker Worker =>
        ActivatorUtilities.CreateInstance<ChallengeQueueWorker>(Factory.Services);

    private async Task<List<Challenge>> QueuedAsync()
    {
        var queued = new List<Challenge>();

        await Factory.WithDatabaseAsync(async database =>
            queued.AddRange(await database.Challenges
                .AsNoTracking()
                .OrderBy(challenge => challenge.Day)
                .ToListAsync(TestContext.Current.CancellationToken)));

        return queued;
    }

    [Fact]
    public async Task OnePassFillsTheHorizonAndTheNextHasNothingToDo()
    {
        var worker = Worker;

        // The seed already holds today's, so the pass writes the rest of the
        // week — which is also the proof that it never doubles up on a day
        // somebody else created.
        var queued = await worker.RunOnceAsync(TestContext.Current.CancellationToken);
        Assert.Equal(ChallengeOptions.DefaultHorizonDays - 1, queued);

        Assert.Equal(0, await worker.RunOnceAsync(TestContext.Current.CancellationToken));

        var rows = await QueuedAsync();

        Assert.Equal(ChallengeOptions.DefaultHorizonDays, rows.Count);
        Assert.Equal(AutomatedTestSeed.ChallengePrompt, rows[0].Prompt);
        Assert.Equal(rows.Count, rows.Select(row => row.Day).Distinct().Count());
    }

    /// <summary>
    /// The point of the queue: tomorrow's prompt exists tonight, so nothing has
    /// to happen at midnight for there to be a challenge in the morning.
    /// </summary>
    [Fact]
    public async Task TomorrowIsAlreadyWrittenAndIsNotRunningYet()
    {
        await Worker.RunOnceAsync(TestContext.Current.CancellationToken);

        var rows = await QueuedAsync();
        var tomorrow = rows[1];

        Assert.True(tomorrow.PublishedAt > Q2ApiFactory.Now);
        Assert.False(tomorrow.IsActiveAt(Q2ApiFactory.Now));
        Assert.False(string.IsNullOrWhiteSpace(tomorrow.Prompt));

        // Every queued day runs from one local midnight to the next, so the end
        // of one is exactly the start of the next.
        for (var index = 2; index < rows.Count; index++)
        {
            Assert.Equal(rows[index - 1].ExpiresAt, rows[index].PublishedAt);
        }
    }

    /// <summary>
    /// Every row the queue writes can be read in either language, each half
    /// straight from the configuration (#105).
    /// </summary>
    [Fact]
    public async Task EveryQueuedDayHasItsPromptInBothLanguages()
    {
        await Worker.RunOnceAsync(TestContext.Current.CancellationToken);

        var configured = Factory.Services.GetRequiredService<IOptions<ChallengeOptions>>().Value.Prompts;

        foreach (var row in (await QueuedAsync()).Skip(1))
        {
            var pair = Assert.Single(configured, prompt => prompt.German == row.Prompt);
            Assert.Equal(pair.English, row.PromptEnglish);
        }
    }

    /// <summary>
    /// A row written before prompts had two languages is given its English as
    /// soon as the configuration holds its German — the archive included —
    /// and one whose prompt has since left the configuration keeps reading in
    /// German, because nobody wrote its English.
    /// </summary>
    [Fact]
    public async Task ARowFromBeforeIsGivenItsEnglishWhereTheConfigurationHasIt()
    {
        var configured = Factory.Services.GetRequiredService<IOptions<ChallengeOptions>>().Value.Prompts[0];

        await Worker.RunOnceAsync(TestContext.Current.CancellationToken);

        // Two rows as they were before #105: one whose German is configured,
        // and the seeded one, whose German is not.
        Guid known = Guid.Empty;
        await Factory.WithDatabaseAsync(async database =>
        {
            var rows = await database.Challenges.OrderBy(challenge => challenge.Day).ToListAsync(TestContext.Current.CancellationToken);
            known = rows[^1].Id;

            await database.Challenges
                .Where(challenge => challenge.Id == known)
                .ExecuteUpdateAsync(
                    update => update
                        .SetProperty(challenge => challenge.Prompt, configured.German)
                        .SetProperty(challenge => challenge.PromptEnglish, (string?)null),
                    TestContext.Current.CancellationToken);

            await database.Challenges
                .Where(challenge => challenge.Id == rows[0].Id)
                .ExecuteUpdateAsync(
                    update => update.SetProperty(challenge => challenge.PromptEnglish, (string?)null),
                    TestContext.Current.CancellationToken);
        });

        await Worker.RunOnceAsync(TestContext.Current.CancellationToken);
        await Worker.RunOnceAsync(TestContext.Current.CancellationToken);

        var after = await QueuedAsync();

        Assert.Equal(configured.English, after.Single(row => row.Id == known).PromptEnglish);
        Assert.Null(after[0].PromptEnglish);
        Assert.Equal(AutomatedTestSeed.ChallengePrompt, after[0].PromptIn(LanguagePreference.English));
    }
}
