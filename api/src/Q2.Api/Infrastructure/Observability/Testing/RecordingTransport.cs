using System.Collections.Concurrent;
using System.Text;
using System.Text.Json;
using Sentry.Extensibility;
using Sentry.Protocol.Envelopes;

namespace Q2.Api.Infrastructure.Observability.Testing;

/// <summary>
/// A Sentry event as it would have gone over the wire.
/// </summary>
/// <remarks>
/// Assertions run against <see cref="RawJson"/> — the serialised payload —
/// rather than against in-memory objects, so "this value is not in the event"
/// means it is genuinely absent from what would have been transmitted.
/// </remarks>
public sealed record RecordedSentryEvent(string RawJson)
{
    private JsonElement Root { get; } = JsonDocument.Parse(RawJson).RootElement.Clone();

    /// <summary>The Sentry event id, as the API reports it in <c>errorId</c>.</summary>
    public string? EventId => GetString("event_id");

    public string? Environment => GetString("environment");

    public string? Release => GetString("release");

    public string? Level => GetString("level");

    public string? ServerName => GetString("server_name");

    public IReadOnlyDictionary<string, string> Tags =>
        Root.TryGetProperty("tags", out var tags) && tags.ValueKind == JsonValueKind.Object
            ? tags.EnumerateObject().ToDictionary(p => p.Name, p => p.Value.ToString())
            : new Dictionary<string, string>();

    /// <summary>Exception messages, outermost first.</summary>
    public IReadOnlyList<string> ExceptionValues =>
        Root.TryGetProperty("exception", out var exception)
        && exception.TryGetProperty("values", out var values)
        && values.ValueKind == JsonValueKind.Array
            ? [.. values.EnumerateArray()
                .Select(v => v.TryGetProperty("value", out var value) ? value.GetString() ?? string.Empty : string.Empty)]
            : [];

    public IReadOnlyList<string> ExceptionTypes =>
        Root.TryGetProperty("exception", out var exception)
        && exception.TryGetProperty("values", out var values)
        && values.ValueKind == JsonValueKind.Array
            ? [.. values.EnumerateArray()
                .Select(v => v.TryGetProperty("type", out var type) ? type.GetString() ?? string.Empty : string.Empty)]
            : [];

    /// <summary>True when the text appears anywhere in the serialised payload.</summary>
    public bool Contains(string text) => RawJson.Contains(text, StringComparison.OrdinalIgnoreCase);

    private string? GetString(string property) =>
        Root.TryGetProperty(property, out var value) ? value.GetString() : null;
}

/// <summary>
/// Replaces the HTTP transport with an in-process recorder.
/// </summary>
/// <remarks>
/// Everything before the network still runs for real: the SDK builds the
/// event, applies <c>BeforeSend</c>, attaches environment, release and tags,
/// and serialises the envelope. Only the send is intercepted. That is what
/// makes it possible to assert on Sentry behaviour without a network
/// dependency and without a real project.
///
/// It is enabled solely by <c>SENTRY_TEST_TRANSPORT=recording</c> (or
/// <c>Sentry:TestTransport</c>) and <see cref="SentrySettings"/> refuses that
/// setting in Staging and Production.
///
/// With <paramref name="filePath"/> set, every event is also appended as one
/// JSON line, which is how the Playwright suite inspects events produced by a
/// separate server process.
/// </remarks>
public sealed class RecordingTransport(string? filePath = null) : ITransport
{
    private readonly ConcurrentQueue<RecordedSentryEvent> _events = new();
    private readonly Lock _fileLock = new();

    // No byte order mark: the file is JSON Lines, and a BOM on the first line
    // makes JSON.parse fail in the tests that read it.
    private static readonly UTF8Encoding FileEncoding = new(encoderShouldEmitUTF8Identifier: false);

    /// <summary>
    /// When set, sending throws. Used to prove that a broken Sentry transport
    /// does not take the API down with it.
    /// </summary>
    public bool FailOnSend { get; set; }

    public IReadOnlyList<RecordedSentryEvent> Events => [.. _events];

    public void Clear() => _events.Clear();

    public async Task SendEnvelopeAsync(Envelope envelope, CancellationToken cancellationToken = default)
    {
        if (FailOnSend)
        {
            throw new InvalidOperationException("Simulated Sentry transport failure.");
        }

        using var buffer = new MemoryStream();
        await envelope.SerializeAsync(buffer, null, cancellationToken);

        foreach (var payload in ExtractEventPayloads(Encoding.UTF8.GetString(buffer.ToArray())))
        {
            var recorded = new RecordedSentryEvent(payload);
            _events.Enqueue(recorded);
            AppendToFile(payload);
        }
    }

    /// <summary>
    /// Envelopes are newline-delimited: an envelope header, then alternating
    /// item headers and item payloads. We keep the payloads whose item header
    /// declares <c>"type":"event"</c>.
    /// </summary>
    private static IEnumerable<string> ExtractEventPayloads(string envelope)
    {
        var lines = envelope.Split('\n', StringSplitOptions.RemoveEmptyEntries);

        for (var i = 1; i < lines.Length - 1; i++)
        {
            if (!IsEventItemHeader(lines[i]))
            {
                continue;
            }

            var payload = lines[i + 1];
            if (IsJsonObject(payload))
            {
                yield return payload;
            }
        }
    }

    private static bool IsEventItemHeader(string line)
    {
        if (!IsJsonObject(line))
        {
            return false;
        }

        try
        {
            using var document = JsonDocument.Parse(line);
            return document.RootElement.TryGetProperty("type", out var type)
                && type.ValueKind == JsonValueKind.String
                && type.GetString() == "event";
        }
        catch (JsonException)
        {
            return false;
        }
    }

    private static bool IsJsonObject(string line) => line.TrimStart().StartsWith('{');

    private void AppendToFile(string payload)
    {
        if (string.IsNullOrWhiteSpace(filePath))
        {
            return;
        }

        lock (_fileLock)
        {
            var directory = Path.GetDirectoryName(filePath);
            if (!string.IsNullOrEmpty(directory))
            {
                Directory.CreateDirectory(directory);
            }

            File.AppendAllText(filePath, payload + System.Environment.NewLine, FileEncoding);
        }
    }
}
