using System.Text.RegularExpressions;

namespace Q2.Api.Infrastructure.Observability;

/// <summary>
/// Recognises and removes data that must not leave the process.
/// </summary>
/// <remarks>
/// Used by the Sentry scrubber, and available to anything else that builds
/// diagnostic output. The rules are deliberately blunt: it is better to redact
/// a harmless value than to leak a credential or a piece of personal data.
///
/// This is a safety net, not a licence. The primary rule stays "do not put
/// user content or secrets into logs and events in the first place" —
/// see docs/privacy.md.
/// </remarks>
public static partial class SensitiveData
{
    public const string Placeholder = "[redacted]";

    /// <summary>
    /// Keys whose value is dropped wherever it appears: headers, tags, extras,
    /// contexts, breadcrumb data, query parameters.
    /// </summary>
    [GeneratedRegex(
        @"(password|passwd|pwd|secret|token|api[_-]?key|apikey|authorization|auth[_-]?header|cookie|session[_-]?id|credential|connection[_-]?string|conn[_-]?str|dsn|private[_-]?key|latitude|longitude|coordinates?|geolocation|\bgeo\b|\bgps\b)",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex SensitiveKeyPattern { get; }

    /// <summary>
    /// Keys carrying content or identity written by a person. These values are
    /// personal by nature and are never useful for debugging a stack trace.
    /// </summary>
    private static readonly string[] UserContentKeys =
    [
        "title",
        "description",
        "goal.title",
        "goal.description",
        "goalTitle",
        "goalDescription",
        "goal_title",
        "goal_description",
        "task.title",
        "taskTitle",
        "task_title",
        "message.text",
        "messageText",
        "message_text",
        "chat.name",
        "chatName",
        "chat_name",
        "conversation.name",
        "conversationName",
        "conversation_name",
        "displayName",
        "display_name",
        "handle",
        "email",
    ];

    [GeneratedRegex(
        @"(?i)\b(data\s*source|initial\s*catalog|user\s*id|password|pwd)\s*=\s*[^;""'\s]+",
        RegexOptions.CultureInvariant)]
    private static partial Regex ConnectionStringPattern { get; }

    [GeneratedRegex(@"(?i)\bbearer\s+[A-Za-z0-9\-._~+/]+=*", RegexOptions.CultureInvariant)]
    private static partial Regex BearerTokenPattern { get; }

    [GeneratedRegex(@"\beyJ[A-Za-z0-9_-]{5,}\.[A-Za-z0-9_-]{5,}\.[A-Za-z0-9_-]*", RegexOptions.CultureInvariant)]
    private static partial Regex JsonWebTokenPattern { get; }

    [GeneratedRegex(@"[A-Za-z0-9._%+-]+@[A-Za-z0-9-]+\.[A-Za-z]{2,}", RegexOptions.CultureInvariant)]
    private static partial Regex EmailPattern { get; }

    /// <summary>
    /// A sensitive name assigned a value inside free text, such as
    /// <c>?token=abc123</c> or <c>"password": "hunter2"</c>. Framework log
    /// messages ("Request starting … GET /x?token=…") end up as breadcrumbs, so
    /// key/value filtering has to work on text as well as on dictionaries.
    /// </summary>
    [GeneratedRegex(
        @"(?i)\b(password|passwd|pwd|secret|token|access[_-]?token|refresh[_-]?token|api[_-]?key|apikey|authorization|cookie|session|credential|dsn)\b(""?\s*[=:]\s*""?)[^&\s""';,}]+",
        RegexOptions.CultureInvariant)]
    private static partial Regex SensitiveAssignmentPattern { get; }

    /// <summary>
    /// User content written into a formatted object or an improvised log line.
    /// Structured parameters are rejected by key as well; this covers the
    /// stringified-object case from console and framework integrations.
    /// </summary>
    [GeneratedRegex(
        @"(?i)\b(title|description|goal[._-]?title|goal[._-]?description|task[._-]?title|message[._-]?text|chat[._-]?name|conversation[._-]?name|display[_-]?name|handle|email)\b(""?\s*[=:]\s*)(?:""[^""]*""|'[^']*'|[^,;}\r\n]+)",
        RegexOptions.CultureInvariant)]
    private static partial Regex UserContentAssignmentPattern { get; }

    /// <summary>
    /// The query part of any URL appearing in free text. Query strings are
    /// where identifiers, tokens and coordinates end up, and nothing in a
    /// stack trace needs them.
    /// </summary>
    [GeneratedRegex(@"(?i)(https?://[^\s?""']+)\?[^\s""']*", RegexOptions.CultureInvariant)]
    private static partial Regex UrlQueryPattern { get; }

    /// <summary>Coordinate pairs such as "48.137,11.575" or "lat=48.1372".</summary>
    [GeneratedRegex(
        @"(?i)\b(lat|lon|lng|latitude|longitude)\b\s*[:=]\s*-?\d{1,3}\.\d+|-?\d{1,3}\.\d{4,},\s*-?\d{1,3}\.\d{4,}",
        RegexOptions.CultureInvariant)]
    private static partial Regex CoordinatePattern { get; }

    /// <summary>Request headers that may be reported. Everything else is dropped.</summary>
    public static readonly IReadOnlySet<string> AllowedRequestHeaders =
        new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "Accept",
            "Accept-Encoding",
            "Content-Type",
            "Content-Length",
            "User-Agent",
            "traceparent",
            "tracestate",
            "X-Request-Id",
        };

    public static bool IsSensitiveKey(string? key) =>
        !string.IsNullOrEmpty(key)
        && (SensitiveKeyPattern.IsMatch(key)
            || UserContentKeys.Contains(key, StringComparer.OrdinalIgnoreCase));

    /// <summary>
    /// Replaces credential-, token-, email- and coordinate-shaped substrings.
    /// Returns the input unchanged when there is nothing to redact.
    /// </summary>
    public static string? Redact(string? value)
    {
        if (string.IsNullOrEmpty(value))
        {
            return value;
        }

        // Order matters. The specific token shapes run before the generic
        // key=value rule: that rule stops at the first whitespace, so for
        // "Authorization: Bearer aaa.bbb.ccc" it would replace only the word
        // "Bearer" and leave the token itself in the payload.
        var result = UrlQueryPattern.Replace(value, $"$1?{Placeholder}");
        result = ConnectionStringPattern.Replace(result, Placeholder);
        result = BearerTokenPattern.Replace(result, Placeholder);
        result = JsonWebTokenPattern.Replace(result, Placeholder);
        result = SensitiveAssignmentPattern.Replace(result, $"$1$2{Placeholder}");
        result = UserContentAssignmentPattern.Replace(result, $"$1$2{Placeholder}");
        result = EmailPattern.Replace(result, Placeholder);
        result = CoordinatePattern.Replace(result, Placeholder);

        return result;
    }

    /// <summary>
    /// Copies <paramref name="source"/>, dropping sensitive keys and redacting
    /// the remaining values.
    /// </summary>
    public static Dictionary<string, string> Sanitise(IEnumerable<KeyValuePair<string, string>> source)
    {
        var result = new Dictionary<string, string>(StringComparer.Ordinal);

        foreach (var (key, value) in source)
        {
            if (IsSensitiveKey(key))
            {
                continue;
            }

            result[key] = Redact(value) ?? string.Empty;
        }

        return result;
    }
}
