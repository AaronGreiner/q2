using Microsoft.EntityFrameworkCore;
using Q2.Api.Infrastructure.Persistence.Seeding;

namespace Q2.Api.Infrastructure.Persistence;

/// <summary>Registers the DbContext, the seeder and the maintenance service.</summary>
public static class PersistenceRegistration
{
    public const string ConnectionStringName = "Database";

    public static WebApplicationBuilder AddQ2Persistence(this WebApplicationBuilder builder)
    {
        builder.Services
            .AddOptions<DatabaseOptions>()
            .Bind(builder.Configuration.GetSection(DatabaseOptions.SectionName))
            .ValidateDataAnnotations()
            .ValidateOnStart();

        var configured = builder.Configuration.GetConnectionString(ConnectionStringName);

        if (string.IsNullOrWhiteSpace(configured))
        {
            throw new InvalidOperationException(
                $"No database connection string configured for environment '{builder.Environment.EnvironmentName}'. "
                + $"Set ConnectionStrings__{ConnectionStringName} (see api/.env.example).");
        }

        var dataDirectory = DatabaseLocation.ResolveDataDirectory(builder.Environment.ContentRootPath);
        var (connectionString, filePath) = DatabaseLocation.Resolve(configured, dataDirectory);
        DatabaseLocation.EnsureDirectoryExists(filePath);

        builder.Services.AddDbContext<Q2DbContext>(options =>
        {
            options.UseSqlite(connectionString, sqlite =>
                sqlite.MigrationsAssembly(typeof(Q2DbContext).Assembly.FullName));

            if (builder.Environment.IsDevelopment())
            {
                // Helpful locally; never enabled elsewhere because parameter
                // values are user content.
                options.EnableDetailedErrors();
            }
        });

        builder.Services.AddScoped<DatabaseSeeder>();
        builder.Services.AddScoped<DatabaseMaintenance>();

        builder.Services.AddSingleton<ISeedDataSource, DevelopmentSeed>();
        builder.Services.AddSingleton<ISeedDataSource, ManualTestingSeed>();
        builder.Services.AddSingleton<ISeedDataSource, AutomatedTestSeed>();
        builder.Services.AddSingleton<ISeedDataSource, E2ESeed>();

        return builder;
    }
}
