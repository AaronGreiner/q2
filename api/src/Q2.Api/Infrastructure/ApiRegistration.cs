using System.Text.Json.Serialization;
using Microsoft.OpenApi;
using Q2.Api.Features.Goals;
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
        builder.Services.AddScoped<GoalService>();

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
                .AllowAnyMethod();
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
