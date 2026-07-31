using Q2.Api.Features.Accounts;
using Q2.Api.Features.People;

namespace Q2.Api.Infrastructure.Persistence.Seeding;

/// <summary>
/// The credentials seeded people sign in with.
/// </summary>
/// <remarks>
/// Every seeded person gets an account, and every account gets the same
/// password. That is what keeps the suites and the manual flows workable now
/// that there is a sign-in in front of everything: a test can be *anybody* in
/// the world it seeded, which is the only way to check that a friendship, a
/// request or a group chat looks right from both ends.
///
/// The hash is a constant rather than something computed while seeding.
/// Identity's hasher salts randomly — correct for real passwords, and fatal to
/// the property this repository depends on: a seed is a pure function of its
/// <see cref="SeedContext"/>, so that rebuilding a database twice produces the
/// same rows twice (AGENTS.md section 7). <c>SeedAccountTests</c> asserts the
/// constant still verifies, so a future Identity change cannot quietly leave
/// every seeded account unable to sign in.
///
/// This is not a secret. It protects nothing: seed profiles only ever run in
/// Development, ManualTesting, AutomatedTest and E2E, and Staging and
/// Production seed nothing at all. The password is in README.md on purpose —
/// a credential you cannot look up is a credential somebody works around.
/// </remarks>
public static class SeedAccounts
{
    /// <summary>
    /// The password every seeded account has. Long rather than complicated,
    /// which is also what <see cref="AccountPolicy"/> asks of a real one.
    /// </summary>
    public const string Password = "kudos-demo-2026";

    /// <summary>
    /// A reserved domain (RFC 2606), so a seeded address cannot possibly be
    /// somebody's real one and nothing sent to it could ever leave.
    /// </summary>
    public const string EmailDomain = "kudos.example";

    /// <summary>
    /// ASP.NET Core Identity V3: PBKDF2-HMAC-SHA512, 100 000 iterations, over
    /// <see cref="Password"/>. Generated once with the framework's own hasher.
    /// </summary>
    public const string PasswordHash =
        "AQAAAAIAAYagAAAAEAMUJTZHWGl6i5ytvs/g8QIkjZE3eHAzbyu+Zrw+fqTHTx1Ouq206+Ew5Y3zcqbLAA==";

    /// <summary>The address for a person, derived from their handle.</summary>
    /// <remarks>
    /// Derived rather than written out per person, so a seed says a handle once
    /// and the address cannot drift away from it.
    /// </remarks>
    public static string EmailFor(string handle) => $"{handle.TrimStart('@')}@{EmailDomain}";

    /// <summary>
    /// Builds the account for <paramref name="person"/>.
    /// </summary>
    /// <remarks>
    /// The stamps are derived from the account id rather than generated.
    /// Identity only requires them to exist and to change when credentials do;
    /// nothing here ever changes credentials, and a <c>Guid.NewGuid()</c> would
    /// make the same seed produce different rows on every run.
    /// </remarks>
    public static AppUser For(Guid accountId, Person person)
    {
        var email = EmailFor(person.Handle);

        return new AppUser
        {
            Id = accountId,
            PersonId = person.Id,
            UserName = email,
            NormalizedUserName = email.ToUpperInvariant(),
            Email = email,
            NormalizedEmail = email.ToUpperInvariant(),

            // No mail is sent anywhere in this version, so there is no
            // confirmation flow a seeded account could be waiting on.
            EmailConfirmed = true,
            PasswordHash = PasswordHash,
            SecurityStamp = $"seed-security-{accountId:N}",
            ConcurrencyStamp = $"seed-concurrency-{accountId:N}",
        };
    }
}
