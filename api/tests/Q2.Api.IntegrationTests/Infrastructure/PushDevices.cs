namespace Q2.Api.IntegrationTests.Infrastructure;

/// <summary>
/// A pretend browser that has agreed to be told things.
/// </summary>
/// <remarks>
/// A real P-256 point and secret, because the endpoint validates their shape;
/// nothing in these tests ever decrypts with them — what is sent is recorded
/// by <see cref="RecordingPushSender"/> before any encryption would happen.
/// </remarks>
public static class PushDevices
{
    public const string BrowserKey =
        "BCVxsr7N_eNgVRqvHtD0zTZsEc6-VV-JvLexhqUzORcxaOzi6-AYWXvTBHm4bjyPjs7Vd8pZGH6SRpkNtoIAiw4";

    public const string BrowserSecret = "BTBZMqHH6r4Tts7J_aSIgg";

    /// <summary>Registers a device for whoever <paramref name="client"/> is signed in as.</summary>
    public static async Task SubscribeDeviceAsync(this HttpClient client, string endpoint)
    {
        var response = await client.PostJsonAsync(
            "/api/notifications/subscribe",
            new { endpoint, publicKey = BrowserKey, authSecret = BrowserSecret });

        response.EnsureSuccessStatusCode();
    }
}
