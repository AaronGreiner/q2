using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Q2.Api.Infrastructure.Persistence;

/// <summary>
/// Lets <c>dotnet ef</c> build a context without starting the application.
/// </summary>
/// <remarks>
/// The alternative — having the EF tools boot the real host — would mean
/// design-time commands depend on configuration, environment variables and
/// whatever the startup policy decides to do. A migration should be
/// reproducible from a checkout alone, so it uses its own throwaway target.
/// Nothing is ever written to this file: EF only needs a provider to know
/// which SQL dialect to generate.
/// </remarks>
public sealed class Q2DbContextFactory : IDesignTimeDbContextFactory<Q2DbContext>
{
    public Q2DbContext CreateDbContext(string[] args)
    {
        var options = new DbContextOptionsBuilder<Q2DbContext>()
            .UseSqlite("Data Source=q2-design-time.db")
            .Options;

        return new Q2DbContext(options);
    }
}
