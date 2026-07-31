using Q2.Api.Infrastructure.Errors;
using Sentry;

namespace Q2.Api.Infrastructure.Observability;

/// <summary>
/// The single <c>BeforeSend</c> / <c>BeforeBreadcrumb</c> filter for the API.
/// </summary>
/// <remarks>
/// Two jobs, in this order:
/// <list type="number">
///   <item><b>Drop noise.</b> Health checks, aborted requests and expected
///   failures (validation, "not found") are not technical errors and must not
///   create Sentry issues.</item>
///   <item><b>Remove sensitive data.</b> Cookies, authorization headers, query
///   strings, request bodies, user identity, machine name, location data and
///   anything that looks like a credential.</item>
/// </list>
/// What it deliberately does <em>not</em> do is drop events by environment.
/// Local and test events are supposed to be visible — that is the point of
/// having separate Sentry environments (see docs/observability.md).
/// </remarks>
public sealed class SentryEventScrubber
{
    /// <summary>Request paths that never produce an event.</summary>
    private static readonly string[] IgnoredPaths = ["/health", "/healthz", "/favicon.ico"];

    public SentryEvent? Scrub(SentryEvent sentryEvent)
    {
        if (ShouldDrop(sentryEvent))
        {
            return null;
        }

        ScrubRequest(sentryEvent);
        ScrubUser(sentryEvent);
        ScrubTagsAndExtras(sentryEvent);
        ScrubMessages(sentryEvent);

        // The machine name identifies a developer's laptop or a container host
        // and adds nothing that the environment tag does not already say.
        sentryEvent.ServerName = null;

        return sentryEvent;
    }

    /// <summary>
    /// The <c>BeforeSendLog</c> filter: what a structured log may carry.
    /// </summary>
    /// <remarks>
    /// Logs are the high-volume channel — every <c>ILogger</c> line the
    /// configured levels let through becomes one — so they are the likeliest
    /// place for a value to slip out. The rule from docs/privacy.md section 3
    /// still comes first: user content and secrets do not go into a log message
    /// at all. This is the net underneath it.
    ///
    /// Framework logs are what make that net necessary rather than theoretical:
    /// "Request starting … GET /path?token=…" is written by ASP.NET Core, not
    /// by us.
    ///
    /// Unlike an event, a log cannot be edited on its way out — <c>Message</c>,
    /// <c>Template</c> and <c>Parameters</c> are init-only, and the attributes
    /// the SDK copied from the message parameters cannot be enumerated at all.
    /// So the choice here is binary, and it is made in the safe direction: if
    /// the redactor would have changed anything, the whole log is dropped
    /// instead of sent. A line that disappears from Sentry Logs is a line that
    /// should not have been written that way.
    /// </remarks>
    public SentryLog? ScrubLog(SentryLog log)
    {
        if (CarriesSensitiveText(log.Message) || CarriesSensitiveText(log.Template))
        {
            return null;
        }

        if (!log.Parameters.IsDefaultOrEmpty
            && log.Parameters.Any(parameter =>
                SensitiveData.IsSensitiveKey(parameter.Key)
                || CarriesSensitiveText(parameter.Value as string)))
        {
            return null;
        }

        foreach (var attribute in IdentifyingLogAttributes)
        {
            // Only when it is actually there: SetAttribute adds what it cannot
            // find, and a "[redacted]" user on every anonymous log line would be
            // noise pretending to be a redaction.
            if (log.TryGetAttribute(attribute, out _))
            {
                log.SetAttribute(attribute, SensitiveData.Placeholder);
            }
        }

        return log;
    }

    /// <summary>
    /// Attributes the SDK attaches to every log by itself, and that the event
    /// scrubber removes from events for the same reasons.
    /// </summary>
    /// <remarks>
    /// This is the one place where logs are genuinely more dangerous than
    /// events. <c>SendDefaultPii</c> makes the SDK copy the signed-in user onto
    /// each log — <c>user.email</c> really is the address somebody signs in with
    /// — and <c>server.address</c> is the machine name that
    /// <see cref="Scrub"/> strips as <c>ServerName</c>. Neither is visible in
    /// the configuration, and both are exactly what docs/privacy.md says never
    /// leaves the process.
    ///
    /// They are overwritten rather than removed because that is what the log API
    /// offers: attributes can be set by name, and there is no way to enumerate
    /// or delete one. Which is also why the list is written out here — an
    /// attribute nobody named stays.
    ///
    /// The IP address is deliberately not in the list, exactly as in
    /// <see cref="ScrubUser"/>: it is the one identifying value q2 does send.
    /// </remarks>
    private static readonly string[] IdentifyingLogAttributes =
    [
        "user.id",
        "user.name",
        "user.username",
        "user.email",
        "server.address",
        "server.name",
        "host.name",
    ];

    /// <summary>
    /// The <c>BeforeSendMetric</c> filter: only the counters q2 declares are
    /// sent.
    /// </summary>
    /// <remarks>
    /// An allow-list rather than a redaction pass, because a metric name is
    /// chosen by whoever emits it and there is no way to sanitise a name
    /// meaningfully. Everything q2 counts goes through <see cref="Q2Metrics"/>,
    /// which is also where the names live; a metric from anywhere else is
    /// dropped rather than transmitted unexamined.
    /// </remarks>
    public SentryMetric? ScrubMetric(SentryMetric metric) =>
        Q2Metrics.Names.Contains(metric.Name) ? metric : null;

    /// <summary>True when redacting would change the value, i.e. there is something in it to hide.</summary>
    private static bool CarriesSensitiveText(string? value) =>
        !string.IsNullOrEmpty(value) && !string.Equals(SensitiveData.Redact(value), value, StringComparison.Ordinal);

    public Breadcrumb? ScrubBreadcrumb(Breadcrumb breadcrumb)
    {
        // The message needs scrubbing even when there is no data dictionary:
        // ASP.NET Core's "Request starting … GET /path?token=…" log line
        // becomes a breadcrumb, query string and all.
        var data = breadcrumb.Data is null
            ? new Dictionary<string, string>()
            : SensitiveData.Sanitise(breadcrumb.Data);

        // Breadcrumbs for HTTP calls carry the full URL; strip the query.
        if (data.TryGetValue("url", out var url))
        {
            data["url"] = StripQuery(url) ?? string.Empty;
        }

        // The timestamp-preserving constructor is internal to the SDK. That is
        // harmless here: BeforeBreadcrumb runs the moment the breadcrumb is
        // recorded, so the replacement's timestamp is the same instant.
        return new Breadcrumb(
            SensitiveData.Redact(breadcrumb.Message) ?? string.Empty,
            breadcrumb.Type ?? string.Empty,
            data,
            breadcrumb.Category,
            breadcrumb.Level);
    }

    private static bool ShouldDrop(SentryEvent sentryEvent)
    {
        // Expected failures are answered with a 4xx and are not defects.
        if (sentryEvent.Exception is IExpectedFailure)
        {
            return true;
        }

        // A client that goes away mid-request is not a server error.
        if (sentryEvent.Exception is OperationCanceledException)
        {
            return true;
        }

        var url = sentryEvent.Request?.Url;
        if (!string.IsNullOrEmpty(url)
            && IgnoredPaths.Any(path => url.Contains(path, StringComparison.OrdinalIgnoreCase)))
        {
            return true;
        }

        return sentryEvent.TransactionName is { } transaction
            && IgnoredPaths.Any(path => transaction.Contains(path, StringComparison.OrdinalIgnoreCase));
    }

    private static void ScrubRequest(SentryEvent sentryEvent)
    {
        if (sentryEvent.Request is not { } request)
        {
            return;
        }

        // Bodies may contain a goal description or anything else the user typed.
        request.Data = null;

        // Query strings are used for filters today, but are exactly where
        // tokens and coordinates end up tomorrow.
        if (!string.IsNullOrEmpty(request.QueryString))
        {
            request.QueryString = SensitiveData.Placeholder;
        }

        request.Url = StripQuery(request.Url);
        request.Cookies = null;

        var allowedHeaders = request.Headers
            .Where(header => SensitiveData.AllowedRequestHeaders.Contains(header.Key))
            .ToDictionary(header => header.Key, header => SensitiveData.Redact(header.Value) ?? string.Empty);

        request.Headers.Clear();
        foreach (var (key, value) in allowedHeaders)
        {
            request.Headers[key] = value;
        }

        // The CGI/environment block carries REMOTE_ADDR and server internals.
        request.Env.Clear();
    }

    private static void ScrubUser(SentryEvent sentryEvent)
    {
        // The IP address is kept: SendDefaultPii is on, and clearing it here
        // would silently undo that option while the configuration still reads
        // true. See docs/privacy.md section 4.
        //
        // Everything else still goes. The authenticated account is real, so an
        // id, email address or name here would identify the person behind the
        // request. None is needed to diagnose the exception.
        sentryEvent.User.Id = null;
        sentryEvent.User.Email = null;
        sentryEvent.User.Username = null;
        sentryEvent.User.Other.Clear();
    }

    private static void ScrubTagsAndExtras(SentryEvent sentryEvent)
    {
        foreach (var key in sentryEvent.Tags.Keys.Where(SensitiveData.IsSensitiveKey).ToList())
        {
            sentryEvent.UnsetTag(key);
        }

        foreach (var (key, value) in sentryEvent.Tags.ToList())
        {
            var redacted = SensitiveData.Redact(value);
            if (!string.Equals(redacted, value, StringComparison.Ordinal))
            {
                sentryEvent.SetTag(key, redacted ?? string.Empty);
            }
        }

        foreach (var (key, value) in sentryEvent.Extra.ToList())
        {
            if (SensitiveData.IsSensitiveKey(key))
            {
                sentryEvent.SetExtra(key, SensitiveData.Placeholder);
                continue;
            }

            if (value is string text)
            {
                sentryEvent.SetExtra(key, SensitiveData.Redact(text));
            }
        }

        foreach (var key in sentryEvent.Contexts.Keys.Where(SensitiveData.IsSensitiveKey).ToList())
        {
            sentryEvent.Contexts.Remove(key);
        }

        // Device context can carry the host name and, on some platforms,
        // coarse location fields.
        sentryEvent.Contexts.Device.Name = null;
    }

    private static void ScrubMessages(SentryEvent sentryEvent)
    {
        if (sentryEvent.Message is { } message)
        {
            sentryEvent.Message = new SentryMessage
            {
                Message = SensitiveData.Redact(message.Message),
                Formatted = SensitiveData.Redact(message.Formatted),
                Params = message.Params,
            };
        }

        // A SqliteException happily includes the connection string.
        if (sentryEvent.SentryExceptions is { } exceptions)
        {
            foreach (var exception in exceptions)
            {
                exception.Value = SensitiveData.Redact(exception.Value);
            }
        }
    }

    private static string? StripQuery(string? url)
    {
        if (string.IsNullOrEmpty(url))
        {
            return url;
        }

        var separator = url.IndexOf('?', StringComparison.Ordinal);
        return separator < 0 ? url : url[..separator];
    }
}
