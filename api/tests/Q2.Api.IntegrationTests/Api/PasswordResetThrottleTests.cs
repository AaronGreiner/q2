using System.Globalization;
using System.Net;
using Microsoft.AspNetCore.Hosting;
using Q2.Api.Features.Accounts;
using Q2.Api.Infrastructure.Persistence.Seeding;
using Q2.Api.IntegrationTests.Infrastructure;

namespace Q2.Api.IntegrationTests.Api;

/// <summary>The limit on how often one client may ask for a reset link.</summary>
/// <remarks>
/// A class and a host of its own. The limit counts per client for the lifetime
/// of a host, and every test client shares one address — so the ordinary test
/// host lifts it (appsettings.AutomatedTest.json) and this one puts a small one
/// back.
/// </remarks>
[Trait("Category", "Integration")]
public sealed class PasswordResetThrottleTests(PasswordResetThrottleTests.ThrottledHost host)
    : IClassFixture<PasswordResetThrottleTests.ThrottledHost>
{
    /// <summary>The ordinary test host, with a budget of two requests.</summary>
    public sealed class ThrottledHost : Q2ApiFactory
    {
        public const int Permits = 2;

        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            base.ConfigureWebHost(builder);
            builder.UseSetting(PasswordResetRateLimit.RequestsPerClientKey, Permits.ToString(CultureInfo.InvariantCulture));
        }
    }

    [Fact]
    public async Task OnceTheBudgetIsSpentTheNextRequestIsTurnedAway()
    {
        using var client = host.CreateClient().AcceptingJson();

        for (var attempt = 0; attempt < ThrottledHost.Permits; attempt++)
        {
            var allowed = await client.PostJsonAsync(
                "/api/auth/password/forgot",
                new { email = AutomatedTestSeed.CurrentPersonEmail });

            Assert.Equal(HttpStatusCode.Accepted, allowed.StatusCode);
        }

        // Whatever the address: the budget belongs to the client, not to it.
        var refused = await client.PostJsonAsync("/api/auth/password/forgot", new { email = "nobody@kudos.example" });

        Assert.Equal(HttpStatusCode.TooManyRequests, refused.StatusCode);
        Assert.Equal("application/problem+json", refused.Content.Headers.ContentType?.MediaType);

        // Two requests got through, and the cooldown still let only one mail out.
        Assert.Single(host.Mail.Sent);
    }
}
