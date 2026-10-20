using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Q2.Api.Features.Chats;
using Q2.Api.Features.Goals;
using Q2.Api.Features.Proofs;
using Q2.Api.Infrastructure.Persistence;
using Q2.Api.Infrastructure.Persistence.Seeding;
using Q2.Api.Infrastructure.Time;
using Q2.Api.IntegrationTests.Infrastructure;

namespace Q2.Api.IntegrationTests.Api;

/// <summary>
/// A long-running goal's conversation, and the chat list it is a row of, read
/// only as much of the goal as a page or a row shows (#127) — and show exactly
/// what reading the whole goal showed.
/// </summary>
[Trait("Category", "Integration")]
public class GoalConversationReadTests(Q2ApiFactory factory) : ApiTestBase(factory)
{
    /// <summary>Days of history: five pages and more of lines.</summary>
    private const int Days = 160;

    private sealed record EventDocument(string Key, GoalEventKind Kind, DateTimeOffset At, int? Streak);

    private sealed record ThreadDocument(IReadOnlyList<EventDocument> Events, string? OlderCursor);

    private sealed record SummaryDocument(Guid Id, DateTimeOffset? LastMessageAt, GoalEventKind? LastEvent);

    private static readonly LocalCalendar Utc = new(TimeZoneInfo.Utc);

    /// <summary>
    /// A daily Do of the current person's, checked by their friend: a
    /// photograph and a vote on every day of <see cref="Days"/>, every tenth
    /// day missed instead, and today's window open.
    /// </summary>
    private async Task<(Guid GoalId, Guid ConversationId)> LongGoalAsync()
    {
        var goalId = Guid.CreateVersion7();
        var conversationId = Guid.CreateVersion7();
        var schedule = GoalSchedule.EveryNDays(1);
        var start = Q2ApiFactory.Today.AddDays(-Days);

        await Factory.WithDatabaseAsync(async database =>
        {
            var goal = Goal.Create(
                goalId,
                AutomatedTestSeed.CurrentPersonId,
                "Automated test: jeden Tag lesen, seit Langem",
                null,
                GoalCategory.Reading,
                schedule,
                isGroup: false,
                reminderAt: null,
                targetDate: null,
                Utc.StartOfDay(start).AddHours(8));
            goal.AddParticipant(Guid.CreateVersion7(), AutomatedTestSeed.FriendPersonId);

            for (var day = start; day < Q2ApiFactory.Today; day = day.AddDays(1))
            {
                var window = goal.OpenWindow(Guid.CreateVersion7(), schedule.FirstWindow(day, day), Utc.StartOfDay(day), Utc.EndOfDay(day))!;

                if (day.DayNumber % 10 == 0)
                {
                    window.Miss(Utc.EndOfDay(day).AddTicks(1));
                    continue;
                }

                var at = Utc.StartOfDay(day).AddHours(7);
                var proof = goal.SubmitProof(Guid.CreateVersion7(), Guid.CreateVersion7(), ProofSource.Live, at)!;
                proof.CastVote(Guid.CreateVersion7(), AutomatedTestSeed.FriendPersonId, VoteValue.Confirm, at.AddMinutes(30));
                goal.ApplyProofOutcome(proof, VotingResult.Confirmed, at.AddMinutes(30));
            }

            goal.OpenWindow(Guid.CreateVersion7(), schedule.FirstWindow(Q2ApiFactory.Today, Q2ApiFactory.Today), Utc.StartOfDay(Q2ApiFactory.Today), Utc.EndOfDay(Q2ApiFactory.Today));

            database.Goals.Add(goal);
            database.Conversations.Add(GoalConversations.Open(goal, conversationId, Guid.CreateVersion7, goal.CreatedAt));
            await database.SaveChangesAsync(TestContext.Current.CancellationToken);
        });

        return (goalId, conversationId);
    }

    /// <summary>The goal's timeline from a whole read of it — the way it was read before #127.</summary>
    private async Task<IReadOnlyList<GoalEvent>> WholeTimelineAsync(Guid goalId)
    {
        Goal goal = null!;

        await Factory.WithDatabaseAsync(async database =>
            goal = await database.Goals
                .AsNoTracking()
                .Include(g => g.Participants)
                .Include(g => g.Instances).ThenInclude(instance => instance.Proofs).ThenInclude(proof => proof.Votes)
                .Include(g => g.Instances).ThenInclude(instance => instance.Proofs).ThenInclude(proof => proof.Reactions)
                .Include(g => g.Instances).ThenInclude(instance => instance.UseLog)
                .Include(g => g.Instances).ThenInclude(instance => instance.SpotChecks)
                .Include(g => g.Pauses)
                .Include(g => g.Changes).ThenInclude(change => change.Checkers)
                .Include(g => g.Changes).ThenInclude(change => change.Vetoes)
                .SingleAsync(g => g.Id == goalId, TestContext.Current.CancellationToken));

        return GoalTimeline.For(goal, Q2ApiFactory.Now);
    }

    /// <summary>The pages a whole read cut, newest first, as their lines' keys and streaks.</summary>
    private async Task<List<List<(string Key, int? Streak)>>> PagesFromTheWholeGoalAsync(Guid goalId)
    {
        var timeline = await WholeTimelineAsync(goalId);
        var pages = new List<List<(string, int?)>>();
        ThreadPosition? before = null;

        do
        {
            var page = ThreadPage.Cut([], timeline, before);
            pages.Add([.. page.Events.Select(entry => (entry.Key, entry.Streak))]);
            before = page.Older;
        }
        while (before is not null);

        return pages;
    }

    /// <summary>
    /// Runs <paramref name="read"/> as the current person in a scope of its
    /// own, and counts the photographs of <paramref name="goalId"/> it read.
    /// </summary>
    /// <remarks>
    /// The goal is read tracked for the chat (<see cref="GoalWindowDetails"/>),
    /// so what the scope's context tracks afterwards is what was read. Calling
    /// the service rather than the endpoint is the only way to look into that
    /// context; the session is the person's own account, not a fake handler.
    /// </remarks>
    private async Task<int> PhotographsReadAsync(Guid goalId, Func<ChatService, Task> read)
    {
        using var scope = Factory.Services.CreateScope();
        var database = scope.ServiceProvider.GetRequiredService<Q2DbContext>();
        var accessor = scope.ServiceProvider.GetRequiredService<IHttpContextAccessor>();

        var accountId = await database.Users
            .Where(user => user.PersonId == AutomatedTestSeed.CurrentPersonId)
            .Select(user => user.Id)
            .SingleAsync(TestContext.Current.CancellationToken);
        var windows = await database.GoalInstances
            .Where(window => window.GoalId == goalId)
            .Select(window => window.Id)
            .ToListAsync(TestContext.Current.CancellationToken);

        accessor.HttpContext = new DefaultHttpContext
        {
            User = new ClaimsPrincipal(new ClaimsIdentity([new Claim(ClaimTypes.NameIdentifier, accountId.ToString())], "Session")),
            RequestServices = scope.ServiceProvider,
        };

        try
        {
            await read(scope.ServiceProvider.GetRequiredService<ChatService>());
        }
        finally
        {
            accessor.HttpContext = null;
        }

        return database.ChangeTracker.Entries<ProofPhoto>().Count(entry => windows.Contains(entry.Entity.GoalInstanceId));
    }

    [Fact]
    public async Task EveryPageOfALongGoalsThreadIsTheOneAWholeReadCut()
    {
        var (goalId, conversationId) = await LongGoalAsync();
        var expected = await PagesFromTheWholeGoalAsync(goalId);

        Assert.True(expected.Count >= 5);

        var pages = new List<List<(string, int?)>>();
        var thread = await (await Client.GetAsync($"/api/chats/{conversationId}", TestContext.Current.CancellationToken))
            .ReadAsync<ThreadDocument>();
        pages.Add([.. thread.Events.Select(entry => (entry.Key, entry.Streak))]);

        for (var before = thread.OlderCursor; before is not null;)
        {
            var older = await (await Client.GetAsync(
                    $"/api/chats/{conversationId}/messages?before={Uri.EscapeDataString(before)}",
                    TestContext.Current.CancellationToken))
                .ReadAsync<ThreadDocument>();
            pages.Add([.. older.Events.Select(entry => (entry.Key, entry.Streak))]);
            before = older.OlderCursor;
        }

        Assert.Equal(expected, pages);
    }

    [Fact]
    public async Task OpeningALongGoalsThreadReadsAPagesWorthOfPhotographs()
    {
        var (goalId, conversationId) = await LongGoalAsync();

        var read = await PhotographsReadAsync(goalId, chats => chats.GetAsync(conversationId, TestContext.Current.CancellationToken));

        // A page holds thirty lines, and every day adds one or two: the newest
        // windows' photographs, nowhere near the ~145 the goal has.
        Assert.InRange(read, 1, ThreadPage.Size + 2);
    }

    [Fact]
    public async Task TheChatListReadsTheNewestWindowsOfALongGoalAndShowsWhatAWholeReadShowed()
    {
        var (goalId, conversationId) = await LongGoalAsync();

        // Today's open window and yesterday's — two more if yesterday was one
        // of the missed days, whose line the row does not show.
        var read = await PhotographsReadAsync(goalId, chats => chats.ListAsync(null, TestContext.Current.CancellationToken));
        Assert.InRange(read, 0, 4);

        // The row's line is the one a whole read finds.
        var row = (await (await Client.GetAsync("/api/chats", TestContext.Current.CancellationToken))
                .ReadAsync<IReadOnlyList<SummaryDocument>>())
            .Single(chat => chat.Id == conversationId);
        var last = (await WholeTimelineAsync(goalId))
            .Last(entry => entry.Kind is not (GoalEventKind.WindowMissed or GoalEventKind.WindowRelapse or GoalEventKind.UseLogged));

        Assert.Equal(last.Kind, row.LastEvent);
        Assert.Equal(last.At, row.LastMessageAt);
    }
}
