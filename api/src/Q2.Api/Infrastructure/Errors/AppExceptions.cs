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
/// Deliberately thrown by the diagnostics endpoint to exercise the unexpected
/// error path end to end (error response shape, logging, Sentry capture).
/// Only reachable in non-production environments.
/// </summary>
public sealed class DiagnosticsTestException(string message) : Exception(message);
