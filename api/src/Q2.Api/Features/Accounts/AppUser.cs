using Microsoft.AspNetCore.Identity;
using Q2.Api.Features.People;

namespace Q2.Api.Features.Accounts;

/// <summary>
/// The credentials somebody signs in with.
/// </summary>
/// <remarks>
/// Deliberately separate from <see cref="Person"/>, and deliberately empty
/// beyond the link between them. ASP.NET Core Identity owns everything to do
/// with authenticating — the password hash, the security stamp, lockout,
/// concurrency — and none of that is the domain's business. The domain owns who
/// somebody <em>is</em>: their name, their handle, their streak, their goals.
///
/// Keeping the two apart means the profile model never grows an Identity base
/// class, and a person can exist without an account. That second property is
/// what makes the seeds work: a world can contain people whose accounts nobody
/// needs to sign in as.
///
/// Nothing is stored here that Identity does not require. The display name,
/// the handle and the avatar live on <see cref="Person"/>, once
/// (docs/adr/0011-authentication-with-identity.md).
/// </remarks>
public sealed class AppUser : IdentityUser<Guid>
{
    /// <summary>The person this account signs in as. One account, one person.</summary>
    public Guid PersonId { get; set; }
}
