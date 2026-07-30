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
        // There are no accounts yet, so there is nothing legitimate to report.
        // The SDK otherwise fills Id with an installation identifier, which is
        // a stable per-machine identifier we have no use for. When
        // authentication arrives, an opaque user id may be kept here — never
        // the email address, name or IP.
        sentryEvent.User.Id = null;
        sentryEvent.User.Email = null;
        sentryEvent.User.Username = null;
        sentryEvent.User.IpAddress = null;
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
