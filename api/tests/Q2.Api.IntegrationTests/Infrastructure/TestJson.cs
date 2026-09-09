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

    public static Task<HttpResponseMessage> PutJsonAsync(this HttpClient client, string url, object body) =>
        client.PutAsJsonAsync(url, body, Options, TestContext.Current.CancellationToken);

    /// <summary>
    /// A DELETE that carries a body.
    /// </summary>
    /// <remarks>
    /// One endpoint needs it — deleting an account asks for the password again
    /// — and <see cref="HttpClient.DeleteAsync(string, CancellationToken)"/>
    /// has no overload that sends one.
    /// </remarks>
    public static Task<HttpResponseMessage> DeleteJsonAsync(this HttpClient client, string url, object body) =>
        client.SendAsync(
            new HttpRequestMessage(HttpMethod.Delete, url)
            {
                Content = JsonContent.Create(body, options: Options),
            },
            TestContext.Current.CancellationToken);
}
