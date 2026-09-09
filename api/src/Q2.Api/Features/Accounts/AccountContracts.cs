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
///
/// <paramref name="InviteCode"/> is the fourth and is never typed: it comes off
/// the link somebody followed to get here. A code that means nothing is ignored
/// rather than refused — registration is the worst possible moment to fail over
/// a stale link somebody was forwarded.
/// </remarks>
public sealed record RegisterRequest(
    string? Name = null,
    string? Email = null,
    string? Password = null,
    string? InviteCode = null);

/// <summary>Request body for signing in.</summary>
public sealed record LoginRequest(string? Email = null, string? Password = null);

/// <summary>Request body for deleting an account.</summary>
/// <remarks>
/// The password again, and that is the whole reason this has a body. Deleting
/// an account is the one irreversible thing in q2, and a session cookie is not
/// enough to authorise it — a borrowed phone with an open app should not be
/// able to erase somebody's year.
/// </remarks>
public sealed record DeleteAccountRequest(string? Password = null);

/// <summary>What was erased, as counts.</summary>
/// <remarks>
/// Returned so the deletion can be seen to have happened rather than merely
/// reported — the same reason a release is verified rather than announced. It
/// carries numbers only: what was deleted is, by construction, no longer there
/// to describe.
/// </remarks>
public sealed record AccountDeletionResponse(
    int Goals,
    int Images,
    int Conversations,
    int Messages,
    int ChallengeEntries);
