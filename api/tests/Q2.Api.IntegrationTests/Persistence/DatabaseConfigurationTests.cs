using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Q2.Api.IntegrationTests.Infrastructure;

namespace Q2.Api.IntegrationTests.Persistence;

/// <summary>Persistence defaults used by the real application host.</summary>
[Trait("Category", "Persistence")]
public class DatabaseConfigurationTests(Q2ApiFactory factory) : IClassFixture<Q2ApiFactory>
{
    [Fact]
    public async Task CollectionIncludesUseSplitQueriesByDefault()
    {
        await factory.WithDatabaseAsync(context =>
        {
            var relationalOptions = context.GetService<IDbContextOptions>()
                .Extensions
                .OfType<RelationalOptionsExtension>()
                .Single();

            Assert.Equal(QuerySplittingBehavior.SplitQuery, relationalOptions.QuerySplittingBehavior);
            return Task.CompletedTask;
        });
    }
}
