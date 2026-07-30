using Q2.Api.Features.Diagnostics;
using Q2.Api.Features.Goals;
using Q2.Api.Infrastructure;
using Q2.Api.Infrastructure.Observability;
using Q2.Api.Infrastructure.Persistence;

var builder = WebApplication.CreateBuilder(args);

builder.AddQ2Observability();
builder.AddQ2Persistence();
builder.AddQ2ApiServices();

var app = builder.Build();

app.UseQ2Pipeline();

app.MapGoalEndpoints();
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
