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
/// One Sentry structured log, as it would have gone over the wire.
/// </summary>
/// <remarks>
/// Logs travel in a container item — one envelope item holding many logs — so
/// this is one entry out of that container, not a whole envelope item.
/// </remarks>
public sealed record RecordedSentryLog(string RawJson)
{
    private JsonElement Root { get; } = JsonDocument.Parse(RawJson).RootElement.Clone();

    /// <summary>"trace", "debug", "info", "warning", "error" or "fatal".</summary>
    public string? Level => GetString("level");

    /// <summary>The formatted message.</summary>
    public string? Body => GetString("body");

    public bool Contains(string text) => RawJson.Contains(text, StringComparison.OrdinalIgnoreCase);

    private string? GetString(string property) =>
        Root.TryGetProperty(property, out var value) ? value.GetString() : null;
}

/// <summary>
/// One Sentry metric, as it would have gone over the wire.
/// </summary>
public sealed record RecordedSentryMetric(string RawJson)
{
    private JsonElement Root { get; } = JsonDocument.Parse(RawJson).RootElement.Clone();

    public string? Name => GetString("name");

    /// <summary>"counter", "gauge" or "distribution".</summary>
    public string? Type => GetString("type");

    public double? Value =>
        Root.TryGetProperty("value", out var value) && value.ValueKind == JsonValueKind.Number
            ? value.GetDouble()
            : null;

    /// <summary>Attribute values, by name, as serialised.</summary>
    public IReadOnlyDictionary<string, string> Attributes =>
        Root.TryGetProperty("attributes", out var attributes) && attributes.ValueKind == JsonValueKind.Object
            ? attributes.EnumerateObject().ToDictionary(
                p => p.Name,
                p => p.Value.TryGetProperty("value", out var value) ? value.ToString() : p.Value.ToString())
            : new Dictionary<string, string>();

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
    private readonly ConcurrentQueue<RecordedSentryLog> _logs = new();
    private readonly ConcurrentQueue<RecordedSentryMetric> _metrics = new();
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

    /// <summary>Structured logs delivered during this run.</summary>
    public IReadOnlyList<RecordedSentryLog> Logs => [.. _logs];

    /// <summary>Metrics delivered during this run.</summary>
    public IReadOnlyList<RecordedSentryMetric> Metrics => [.. _metrics];

    public void Clear()
    {
        _events.Clear();
        _logs.Clear();
        _metrics.Clear();
    }

    public async Task SendEnvelopeAsync(Envelope envelope, CancellationToken cancellationToken = default)
    {
        if (FailOnSend)
        {
            throw new InvalidOperationException("Simulated Sentry transport failure.");
        }

        using var buffer = new MemoryStream();
        await envelope.SerializeAsync(buffer, null, cancellationToken);

        foreach (var (type, payload) in ExtractItems(Encoding.UTF8.GetString(buffer.ToArray())))
        {
            switch (type)
            {
                case EventItemType:
                    _events.Enqueue(new RecordedSentryEvent(payload));

                    // Only events go to the file: it is read by the Playwright
                    // suite, which asserts on error reporting.
                    AppendToFile(payload);
                    break;

                case LogItemType:
                    foreach (var entry in ExtractContainerEntries(payload))
                    {
                        _logs.Enqueue(new RecordedSentryLog(entry));
                    }

                    break;

                case MetricItemType:
                    foreach (var entry in ExtractContainerEntries(payload))
                    {
                        _metrics.Enqueue(new RecordedSentryMetric(entry));
                    }

                    break;
            }
        }
    }

    private const string EventItemType = "event";

    /// <summary>Logs and metrics travel as containers of many entries.</summary>
    private const string LogItemType = "log";

    private const string MetricItemType = "trace_metric";

    /// <summary>
    /// Envelopes are newline-delimited: an envelope header, then alternating
    /// item headers and item payloads. Yields the payloads of the item types
    /// this recorder knows about, each with the type its header declared.
    /// </summary>
    private static IEnumerable<(string Type, string Payload)> ExtractItems(string envelope)
    {
        var lines = envelope.Split('\n', StringSplitOptions.RemoveEmptyEntries);

        for (var i = 1; i < lines.Length - 1; i++)
        {
            if (ItemType(lines[i]) is not { } type)
            {
                continue;
            }

            var payload = lines[i + 1];
            if (IsJsonObject(payload))
            {
                yield return (type, payload);
            }
        }
    }

    private static string? ItemType(string line)
    {
        if (!IsJsonObject(line))
        {
            return null;
        }

        try
        {
            using var document = JsonDocument.Parse(line);
            return document.RootElement.TryGetProperty("type", out var type)
                && type.ValueKind == JsonValueKind.String
                ? type.GetString()
                : null;
        }
        catch (JsonException)
        {
            return null;
        }
    }

    /// <summary>
    /// A log or metric item is <c>{"items":[…]}</c>. Yields each entry as its
    /// own JSON document so assertions can be made per log line.
    /// </summary>
    private static IEnumerable<string> ExtractContainerEntries(string payload)
    {
        JsonDocument document;

        try
        {
            document = JsonDocument.Parse(payload);
        }
        catch (JsonException)
        {
            yield break;
        }

        using (document)
        {
            if (!document.RootElement.TryGetProperty("items", out var items)
                || items.ValueKind != JsonValueKind.Array)
            {
                yield break;
            }

            foreach (var item in items.EnumerateArray())
            {
                yield return item.GetRawText();
            }
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
