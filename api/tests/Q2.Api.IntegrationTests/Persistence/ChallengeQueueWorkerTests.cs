using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Q2.Api.Features.Challenges;
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
}
