using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Q2.Api.IntegrationTests.Infrastructure;

/// <summary>
/// Serialisation settings that mirror the API's, so a test reads exactly what
/// a typed client would.
/// </summary>
public static class TestJson
{
    public static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() },
        NumberHandling = JsonNumberHandling.Strict,
    };

    public static async Task<T> ReadAsync<T>(this HttpResponseMessage response)
    {
        var value = await response.Content.ReadFromJsonAsync<T>(Options, TestContext.Current.CancellationToken);

        Assert.NotNull(value);
        return value;
    }

    public static Task<HttpResponseMessage> PostJsonAsync(this HttpClient client, string url, object body) =>
        client.PostAsJsonAsync(url, body, Options, TestContext.Current.CancellationToken);
}
