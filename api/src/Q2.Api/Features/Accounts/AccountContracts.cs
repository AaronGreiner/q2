using Q2.Api.Features.People;

namespace Q2.Api.Features.Accounts;

/// <summary>
/// Who is signed in.
/// </summary>
/// <remarks>
/// The person, plus the one thing about the account itself that its owner may
/// want to see. No password state, no security stamp, no lockout counter:
/// nothing about how the session was established is any of the client's
/// business.
/// </remarks>
public sealed record SessionResponse(PersonSummary Person, string Email);

/// <summary>Request body for creating an account.</summary>
/// <remarks>
/// Nullable with defaults, like every other request DTO here, so an empty body
/// produces our own field-level messages rather than a binding failure.
///
/// Three fields and no more. The handle, the initials and the avatar colour are
/// derived from <paramref name="Name"/> by <see cref="ProfileDefaults"/> —
/// asking for them would be three more things to get wrong on a phone.
/// </remarks>
public sealed record RegisterRequest(string? Name = null, string? Email = null, string? Password = null);

/// <summary>Request body for signing in.</summary>
public sealed record LoginRequest(string? Email = null, string? Password = null);
