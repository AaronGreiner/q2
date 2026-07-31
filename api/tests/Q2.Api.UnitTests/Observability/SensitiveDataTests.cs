using Q2.Api.Infrastructure.Observability;

namespace Q2.Api.UnitTests.Observability;

/// <summary>
/// The redaction rules that back every privacy claim in docs/privacy.md.
/// </summary>
[Trait("Category", "Sentry")]
public class SensitiveDataTests
{
    [Theory]
    [InlineData("password")]
    [InlineData("Password")]
    [InlineData("user_password")]
    [InlineData("apiKey")]
    [InlineData("api-key")]
    [InlineData("Authorization")]
    [InlineData("Cookie")]
    [InlineData("access_token")]
    [InlineData("ConnectionString")]
    [InlineData("latitude")]
    [InlineData("longitude")]
    [InlineData("coordinates")]
    [InlineData("goal.description")]
    [InlineData("GoalTitle")]
    public void SensitiveKeysAreRecognised(string key)
    {
        Assert.True(SensitiveData.IsSensitiveKey(key));
    }

    [Theory]
    [InlineData("requestId")]
    [InlineData("statusCode")]
    [InlineData("goalCount")]
    [InlineData("durationMs")]
    public void OrdinaryDiagnosticKeysAreKept(string key)
    {
        Assert.False(SensitiveData.IsSensitiveKey(key));
    }

    [Fact]
    public void ConnectionStringsAreRedacted()
    {
        var redacted = SensitiveData.Redact(
            "SQLite Error: unable to open database file Data Source=/srv/live/q2.db;Password=hunter2");

        Assert.DoesNotContain("/srv/live/q2.db", redacted);
        Assert.DoesNotContain("hunter2", redacted);
    }

    [Fact]
    public void BearerTokensAreRedacted()
    {
        var redacted = SensitiveData.Redact("Authorization header was 'Bearer abc123.def456.ghi789'");

        Assert.DoesNotContain("abc123", redacted);
    }

    [Fact]
    public void ATokenFollowingASensitiveKeywordIsFullyRemoved()
    {
        // Regression guard: the generic key=value rule stops at the first
        // whitespace. If it ran before the bearer rule it would replace only
        // the word "Bearer" and leave the token in place.
        var redacted = SensitiveData.Redact("Authorization: Bearer aaa.bbb.ccc");

        Assert.DoesNotContain("aaa.bbb.ccc", redacted);
    }

    [Fact]
    public void JsonWebTokensAreRedacted()
    {
        var redacted = SensitiveData.Redact(
            "token eyJhbGciOiJIUzI1NiJ9.eyJzdWIiOiIxMjMifQ.dBjftJeZ4CVPmB92K27uhbUJU1p1r_wW1gFWFOEjXk");

        Assert.DoesNotContain("eyJhbGciOiJIUzI1NiJ9", redacted);
    }

    [Fact]
    public void EmailAddressesAreRedacted()
    {
        var redacted = SensitiveData.Redact("Contact robin.sample@example.com about this");

        Assert.DoesNotContain("robin.sample@example.com", redacted);
    }

    [Theory]
    [InlineData("lat=48.1372 lon=11.5756")]
    [InlineData("position 48.13720, 11.57560")]
    [InlineData("latitude: -33.8688")]
    public void CoordinatesAreRedacted(string text)
    {
        var redacted = SensitiveData.Redact(text);

        Assert.DoesNotContain("48.1372", redacted);
        Assert.DoesNotContain("11.5756", redacted);
        Assert.DoesNotContain("33.8688", redacted);
    }

    [Fact]
    public void QueryStringsAreStrippedFromUrls()
    {
        // This is the shape ASP.NET Core's "Request starting" log line has, and
        // it reaches Sentry as a breadcrumb.
        var redacted = SensitiveData.Redact(
            "Request starting HTTP/1.1 GET http://localhost:5080/api/goals?token=supersecret123 - -");

        Assert.DoesNotContain("supersecret123", redacted);
        Assert.Contains("/api/goals", redacted);
    }

    [Fact]
    public void SensitiveAssignmentsInFreeTextAreRedacted()
    {
        var redacted = SensitiveData.Redact("failed with token=abc123secret and session=zzz");

        Assert.DoesNotContain("abc123secret", redacted);
        Assert.DoesNotContain("zzz", redacted);
    }

    [Theory]
    [InlineData("GoalTitle: Therapy appointment every Tuesday")]
    [InlineData("{\"messageText\":\"I feel overwhelmed today\"}")]
    public void UserContentAssignmentsInFreeTextAreRedacted(string text)
    {
        var redacted = SensitiveData.Redact(text);

        Assert.DoesNotContain("Therapy appointment", redacted);
        Assert.DoesNotContain("I feel overwhelmed", redacted);
    }

    [Fact]
    public void HarmlessTextIsLeftAlone()
    {
        const string message = "Goal 019faece-5a81-7c67-8fa2-00a63d9e9127 created with 2 participants";

        Assert.Equal(message, SensitiveData.Redact(message));
    }

    [Fact]
    public void SanitiseDropsSensitiveKeysAndRedactsTheRest()
    {
        var result = SensitiveData.Sanitise(new Dictionary<string, string>
        {
            ["Authorization"] = "Bearer abc",
            ["requestId"] = "0HN123",
            ["url"] = "http://localhost/api/goals?token=leak",
        });

        Assert.DoesNotContain("Authorization", result.Keys);
        Assert.Equal("0HN123", result["requestId"]);
        Assert.DoesNotContain("leak", result["url"]);
    }

    [Fact]
    public void OnlyExpectedHeadersAreAllowed()
    {
        Assert.Contains("User-Agent", SensitiveData.AllowedRequestHeaders);
        Assert.Contains("traceparent", SensitiveData.AllowedRequestHeaders);
        Assert.DoesNotContain("Authorization", SensitiveData.AllowedRequestHeaders);
        Assert.DoesNotContain("Cookie", SensitiveData.AllowedRequestHeaders);
    }
}
