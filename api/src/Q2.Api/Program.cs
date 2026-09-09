using Q2.Api.Features.Accounts;
using Q2.Api.Features.Activity;
using Q2.Api.Features.Challenges;
using Q2.Api.Features.Chats;
using Q2.Api.Features.Diagnostics;
using Q2.Api.Features.Goals;
using Q2.Api.Features.Images;
using Q2.Api.Features.Moderation;
using Q2.Api.Features.Notifications;
using Q2.Api.Features.People;
using Q2.Api.Features.Profile;
using Q2.Api.Features.Proofs;
using Q2.Api.Features.Settings;
using Q2.Api.Infrastructure;
using Q2.Api.Infrastructure.Observability;
using Q2.Api.Infrastructure.Persistence;

var builder = WebApplication.CreateBuilder(args);

builder.AddQ2Observability();
builder.AddQ2Persistence();

// After persistence: Identity's stores are registered against Q2DbContext.
builder.AddQ2Authentication();
builder.AddQ2ApiServices();

var app = builder.Build();

app.UseQ2Pipeline();

app.MapAccountEndpoints();
app.MapProfileEndpoints();
app.MapGoalEndpoints();
app.MapImageEndpoints();
app.MapProofEndpoints();
app.MapChallengeEndpoints();
app.MapActivityEndpoints();
app.MapFriendsEndpoints();
app.MapBlockEndpoints();
app.MapInviteEndpoints();
app.MapModerationEndpoints();
app.MapNotificationEndpoints();
app.MapChatEndpoints();
app.MapSettingsEndpoints();
app.MapHealthEndpoint();
app.MapDiagnosticsEndpoints(app.Environment);

// `db …` and `openapi …` reuse the fully configured host and then exit, so a
// maintenance command always sees exactly the configuration the server sees —
// including the routes, which is what the OpenAPI export reads.
if (CommandLineRunner.IsCommand(args))
{
    return await CommandLineRunner.RunAsync(app, args, CancellationToken.None);
}

app.LogObservabilityStatus();
await app.ApplyDatabaseStartupPolicyAsync();

await app.RunAsync();
return 0;

/// <summary>
/// Exposed so <c>WebApplicationFactory&lt;Program&gt;</c> can boot the real
/// application in integration tests.
/// </summary>
public partial class Program;
