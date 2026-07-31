namespace Q2.Api.Infrastructure.Errors;

/// <summary>
/// Marker for exceptions that describe an <em>expected</em> outcome: bad input,
/// a missing resource, a rule the caller broke. They become 4xx responses and
/// are never reported to Sentry as technical errors.
/// </summary>
public interface IExpectedFailure
{
    int StatusCode { get; }
}

/// <summary>
/// A domain invariant was violated. Carries per-field messages so the API can
/// answer with a standard validation problem.
/// </summary>
public sealed class DomainValidationException : Exception, IExpectedFailure
{
    public DomainValidationException(string field, string message)
        : this(new Dictionary<string, string[]> { [field] = [message] })
    {
    }

    public DomainValidationException(IReadOnlyDictionary<string, string[]> errors)
        : base("One or more validation errors occurred.")
    {
        Errors = errors;
    }

    public IReadOnlyDictionary<string, string[]> Errors { get; }

    public int StatusCode => StatusCodes.Status400BadRequest;
}

/// <summary>A requested resource does not exist.</summary>
public sealed class ResourceNotFoundException(string resource, object key)
    : Exception($"{resource} '{key}' was not found."), IExpectedFailure
{
    public string Resource { get; } = resource;

    public int StatusCode => StatusCodes.Status404NotFound;
}

/// <summary>
/// The caller is not signed in, the credentials were wrong, or the account
/// behind the session is gone.
/// </summary>
/// <remarks>
/// Feature endpoints are guarded by <c>RequireAuthorization</c>, so an ordinary
/// unauthenticated request never reaches a service and never becomes this
/// exception. Two cases the guard cannot cover do: a sign-in attempt that
/// failed, and a session whose account or person has since been deleted.
///
/// <see cref="Reason"/> travels in the problem details as an extension so the
/// client can choose its own sentence. The API does not send prose — the app
/// speaks two languages, and "E-Mail oder Passwort stimmt nicht" is a decision
/// for the catalogue, not for a response body
/// (docs/adr/0010-german-first-interface.md).
/// </remarks>
public sealed class AuthenticationRequiredException(string message, string reason = AuthenticationFailures.NoSession)
    : Exception(message), IExpectedFailure
{
    /// <summary>One of <see cref="AuthenticationFailures"/>.</summary>
    public string Reason { get; } = reason;

    public int StatusCode => StatusCodes.Status401Unauthorized;
}

/// <summary>
/// The machine-readable reasons a 401 can carry. Part of the API contract:
/// the client picks a message from these, never from the response text.
/// </summary>
public static class AuthenticationFailures
{
    /// <summary>No session, or one that no longer resolves to an account.</summary>
    public const string NoSession = "noSession";

    /// <summary>The email address or the password was wrong.</summary>
    public const string InvalidCredentials = "invalidCredentials";

    /// <summary>Too many failed attempts; the account is temporarily locked.</summary>
    public const string LockedOut = "lockedOut";
}

/// <summary>
/// The caller is signed in, but this is not theirs to do.
/// </summary>
/// <remarks>
/// Used only where hiding the resource would be worse than naming the rule —
/// leaving a group you are not in, for instance. Where the existence of the
/// resource is itself private, features answer
/// <see cref="ResourceNotFoundException"/> instead: telling somebody that a
/// conversation exists but is not theirs is already a disclosure.
/// </remarks>
public sealed class AccessDeniedException(string message)
    : Exception(message), IExpectedFailure
{
    public int StatusCode => StatusCodes.Status403Forbidden;
}

/// <summary>
/// Deliberately thrown by the diagnostics endpoint to exercise the unexpected
/// error path end to end (error response shape, logging, Sentry capture).
/// Only reachable in non-production environments.
/// </summary>
public sealed class DiagnosticsTestException(string message) : Exception(message);
