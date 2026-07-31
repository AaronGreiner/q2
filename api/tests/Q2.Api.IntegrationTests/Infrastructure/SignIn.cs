using System.Net.Http.Headers;
using Q2.Api.Infrastructure.Persistence.Seeding;

namespace Q2.Api.IntegrationTests.Infrastructure;

/// <summary>
/// Signs a test client in, through the real endpoint.
/// </summary>
/// <remarks>
/// Deliberately not a fake authentication handler. A stub would let every test
/// pass while the thing the tests exist to protect — that a request without a
/// session gets nothing, and that a request with one gets exactly that
/// person's data — was never exercised. So this posts to <c>/api/auth/login</c>
/// like a browser does and keeps the cookie it gets back.
///
/// The cookie is copied onto the client's default headers rather than handled
/// by a <see cref="System.Net.CookieContainer"/>: <c>WebApplicationFactory</c>
/// talks to the server in memory, where there is no cookie jar, and one visible
/// header is easier to reason about in a failing test than an invisible one.
/// </remarks>
public static class SignIn
{
    /// <summary>Signs <paramref name="client"/> in as the person with this address.</summary>
    /// <param name="password">
    /// Defaults to the one every seeded account shares. Tests that registered
    /// their own account pass the password they chose.
    /// </param>
    /// <exception cref="InvalidOperationException">The credentials were refused.</exception>
    public static async Task<HttpClient> AsAsync(this HttpClient client, string email, string? password = null)
    {
        var response = await client.PostJsonAsync(
            "/api/auth/login",
            new { email, password = password ?? SeedAccounts.Password });

        if (!response.IsSuccessStatusCode)
        {
            var body = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);

            throw new InvalidOperationException(
                $"Signing in as a seeded account failed with {(int)response.StatusCode}: {body}");
        }

        var cookie = response.Headers.TryGetValues("Set-Cookie", out var values)
            ? values.FirstOrDefault()
            : null;

        if (cookie is null)
        {
            throw new InvalidOperationException("Signing in succeeded but set no session cookie.");
        }

        // Everything after the first ";" is the cookie's own attributes — path,
        // expiry, SameSite — which a client sends back none of.
        client.DefaultRequestHeaders.Add("Cookie", cookie.Split(';')[0]);

        return client;
    }

    /// <summary>Drops the session, leaving the client anonymous again.</summary>
    public static HttpClient SignOut(this HttpClient client)
    {
        client.DefaultRequestHeaders.Remove("Cookie");
        return client;
    }

    /// <summary>The <c>Accept</c> header every test client sends.</summary>
    public static HttpClient AcceptingJson(this HttpClient client)
    {
        client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        return client;
    }
}
