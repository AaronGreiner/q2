using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Identity;
using Microsoft.OpenApi;
using Q2.Api.Features.Accounts;
using Q2.Api.Features.Activity;
using Q2.Api.Features.Challenges;
using Q2.Api.Features.Chats;
using Q2.Api.Features.Goals;
using Q2.Api.Features.Images;
using Q2.Api.Features.Moderation;
using Q2.Api.Features.Notifications;
using Q2.Api.Features.People;
using Q2.Api.Features.Profile;
using Q2.Api.Features.Proofs;
using Q2.Api.Features.Settings;
using Q2.Api.Infrastructure.Errors;
using Q2.Api.Infrastructure.Persistence;
using Q2.Api.Infrastructure.Time;

namespace Q2.Api.Infrastructure;

/// <summary>OpenAPI document identity, shared by the server and the exporter.</summary>
public static class OpenApiConfiguration
{
    public const string DocumentName = "v1";

    /// <summary>
    /// 3.0 rather than 3.1: it is what the whole client-generator ecosystem
    /// supports best, and nothing in this contract needs 3.1.
    /// </summary>
    public const OpenApiSpecVersion SpecVersion = OpenApiSpecVersion.OpenApi3_0;
}

/// <summary>Registers HTTP concerns: JSON, problem details, OpenAPI, CORS.</summary>
public static class ApiRegistration
{
    public const string CorsPolicyName = "q2-frontend";

    public static WebApplicationBuilder AddQ2ApiServices(this WebApplicationBuilder builder)
    {
        builder.Services.AddSingleton(TimeProvider.System);
        builder.Services.AddSingleton<IIdGenerator, SequentialIdGenerator>();

        // Singleton because resolving a zone reads the system database every
        // time, and a lookup per goal would be a lookup per goal.
        builder.Services.AddSingleton<TimeZoneResolver>();

        builder.Services.Configure<ImageOptions>(builder.Configuration.GetSection(ImageOptions.SectionName));

        // The daily challenge's prompts. Configuration is the editorial desk
        // here on purpose — see ChallengeOptions.
        builder.Services.Configure<ChallengeOptions>(
            builder.Configuration.GetSection(ChallengeOptions.SectionName));

        builder.Services.Configure<PushOptions>(builder.Configuration.GetSection(PushOptions.SectionName));

        /*
         * A typed client, so the connections to a push service are pooled and
         * the timeout is ours rather than the default hundred seconds.
         *
         * Ten seconds: a background pass that has to reach several hundred
         * devices cannot spend a minute and a half on each one that is
         * unreachable, and a push that is late is a push that has missed its
         * point (PushOptions.DefaultTimeToLiveSeconds).
         */
        builder.Services.AddHttpClient<IPushSender, WebPushSender>(client =>
            client.Timeout = TimeSpan.FromSeconds(10));

        /*
         * Singleton, and an interface with one implementation.
         *
         * The seam is the point: the store is a directory on the host today,
         * which is the same "simplest thing that carries the product" posture
         * as SQLite (docs/adr/0004-sqlite-first.md). Every caller depends on
         * IImageStore, so moving to an S3-compatible bucket is a registration
         * and a class, not a search through the features.
         */
        builder.Services.AddSingleton<IImageStore, FileSystemImageStore>();

        /*
         * Where a report actually goes, behind the same kind of seam.
         *
         * Today it rings the one bell this deployment already answers; a
         * mailbox, a queue or a moderation console is a registration and a
         * class (docs/adr/0022-blocking-reporting-and-erasure.md). Singleton
         * because it holds nothing per request.
         */
        builder.Services.AddSingleton<IReportSink, ObservabilityReportSink>();

        // Scoped, all of them: each holds a DbContext for the duration of one
        // request, and CurrentPerson caches the answer to "who is asking?" for
        // exactly that long.
        builder.Services.AddScoped<CurrentPerson>();

        // Beside CurrentPerson, and for the same reason: "who is asking" and
        // "who must they not see" are both answered once per request and cached
        // for exactly that long.
        builder.Services.AddScoped<BlockList>();
        builder.Services.AddScoped<BlockService>();
        builder.Services.AddScoped<InviteService>();
        builder.Services.AddScoped<NotificationService>();
        builder.Services.AddScoped<ReportService>();
        builder.Services.AddScoped<AccountService>();
        builder.Services.AddScoped<ActivityRecorder>();
        builder.Services.AddScoped<GoalService>();
        builder.Services.AddScoped<ImageService>();
        builder.Services.AddScoped<ProofService>();
        builder.Services.AddScoped<ChallengeService>();
        builder.Services.AddScoped<ActivityService>();
        builder.Services.AddScoped<FriendsService>();
        builder.Services.AddScoped<ChatService>();
        builder.Services.AddScoped<ProfileService>();
        builder.Services.AddScoped<SettingsService>();

        /*
         * The two things in q2 that happen without somebody asking for them:
         * windows that fall due while nobody is looking, and the queue of
         * prompts that has to be a few days deep before anybody opens the app.
         *
         * Neither runs in AutomatedTest: integration tests assert on exact
         * rows, and a job writing between the arrange and the assert would make
         * them flaky for a reason unrelated to what they check. Those tests
         * drive GoalMaintenance and ChallengeQueueWorker.RunOnceAsync directly,
         * or seed the rows they need.
         */
        if (!builder.Environment.IsAutomatedTest())
        {
            builder.Services.AddHostedService<GoalMaintenanceWorker>();
            builder.Services.AddHostedService<ChallengeQueueWorker>();
        }

        builder.Services.ConfigureHttpJsonOptions(options =>
        {
            // Enums travel as names ("Active"), which keeps the contract stable
            // and gives the generated TypeScript a string union.
            options.SerializerOptions.Converters.Add(new JsonStringEnumConverter());

            // The ASP.NET Core web defaults also accept numbers written as
            // strings ("50"). That makes every numeric property a
            // string-or-number union in the schema, which OpenAPI 3.0 cannot
            // express — it drops the type entirely and the generated client
            // ends up with `unknown`. Strict handling gives one honest type per
            // field, and "50" is not something a typed client would send.
            options.SerializerOptions.NumberHandling = JsonNumberHandling.Strict;
        });

        builder.Services.AddProblemDetails(options =>
        {
            options.CustomizeProblemDetails = context =>
            {
                context.ProblemDetails.Instance ??= context.HttpContext.Request.Path;
                context.ProblemDetails.Extensions["traceId"] = context.HttpContext.TraceIdentifier;
            };
        });

        builder.Services.AddExceptionHandler<GlobalExceptionHandler>();

        builder.Services.AddOpenApi(OpenApiConfiguration.DocumentName, options =>
        {
            options.OpenApiVersion = OpenApiConfiguration.SpecVersion;
            options.AddDocumentTransformer((document, _, _) =>
            {
                document.Info = new OpenApiInfo
                {
                    Title = "q2 API",
                    Version = "v1",
                    Description = "Kudos (q2) — shared self-care goals. Reference API for the initial version.",
                };

                // The generator fills Servers from the address the host happens
                // to be listening on. The exporter starts the host on port 0, so
                // that address is a different ephemeral port on every run, and a
                // committed contract could never match a freshly exported one —
                // CI's contract check would fail on nothing but the port.
                //
                // Dropping it is also the honest contract: the base address is
                // configuration on both sides (ASPNETCORE_URLS for the server,
                // NUXT_PUBLIC_API_BASE_URL for the client), never something a
                // client should read out of this document.
                document.Servers?.Clear();

                return Task.CompletedTask;
            });
        });

        var allowedOrigins = builder.Configuration
            .GetSection("Cors:AllowedOrigins")
            .Get<string[]>() ?? [];

        builder.Services.AddCors(options => options.AddPolicy(CorsPolicyName, policy =>
        {
            if (allowedOrigins.Length == 0)
            {
                // No origins configured means "same origin only" — the safe
                // default for Staging and Production, where the frontend is
                // served from the same host or through a gateway.
                return;
            }

            policy.WithOrigins(allowedOrigins)
                .AllowAnyHeader()
                .AllowAnyMethod()

                // The session travels as a cookie, and a cross-origin request
                // does not carry one unless both sides say so. This is why the
                // origins have to be listed explicitly: a wildcard origin and
                // credentials are not allowed together, by the specification
                // and for good reason.
                .AllowCredentials();
        }));

        return builder;
    }

    /// <summary>Middleware, in the order it has to run.</summary>
    public static WebApplication UseQ2Pipeline(this WebApplication app)
    {
        // First, so everything after it produces Problem Details instead of a
        // raw stack trace — including in Development, where the developer
        // exception page would otherwise leak internals to the browser.
        app.UseExceptionHandler();
        app.UseStatusCodePages();

        // Creates the Sentry transaction for each request. Sentry's own
        // exception middleware sits outside UseExceptionHandler, so handled
        // exceptions are reported exactly once, by GlobalExceptionHandler.
        app.UseSentryTracing();

        app.UseCors(CorsPolicyName);

        // After CORS, before the endpoints: a rejected pre-flight must not
        // depend on a session it was never going to send.
        app.UseAuthentication();
        app.UseAuthorization();

        if (!ApplicationEnvironments.Protected.Contains(app.Environment.EnvironmentName))
        {
            // The contract is public API surface, but there is no reason to
            // serve it from production hosts.
            app.MapOpenApi();
        }

        return app;
    }

    /// <summary>
    /// Applies whatever database work this environment permits at startup.
    /// </summary>
    public static async Task ApplyDatabaseStartupPolicyAsync(this WebApplication app)
    {
        await using var scope = app.Services.CreateAsyncScope();
        var maintenance = scope.ServiceProvider.GetRequiredService<DatabaseMaintenance>();
        await maintenance.ApplyStartupPolicyAsync(CancellationToken.None);
    }
}
