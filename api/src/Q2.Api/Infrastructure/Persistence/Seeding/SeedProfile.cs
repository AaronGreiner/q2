namespace Q2.Api.Infrastructure.Persistence.Seeding;

/// <summary>
/// Which set of synthetic data to insert. One profile per environment, so a
/// seed can never be "the wrong size" for the situation it runs in.
/// </summary>
public enum SeedProfile
{
    /// <summary>Insert nothing.</summary>
    None = 0,

    /// <summary>Small, optional data set for local development.</summary>
    Development,

    /// <summary>Rich, human-readable data set for exploratory manual testing.</summary>
    ManualTesting,

    /// <summary>Minimal deterministic data set for API and persistence tests.</summary>
    AutomatedTest,

    /// <summary>Deterministic data set with stable ids that Playwright relies on.</summary>
    E2E,
}
