using Xunit;

// Integration tests run one at a time, on purpose.
//
// The Sentry .NET SDK keeps a *global* hub: every host built by a
// WebApplicationFactory calls UseSentry, and the last initialisation wins
// process-wide. Two hosts alive at once would therefore share one transport,
// and a test asserting "my request produced exactly one event" would be reading
// another test's events. That is not a flake to retry — it is the SDK's design.
//
// Sequential execution keeps exactly one host alive at a time, which restores
// isolation. The whole suite still runs in well under a second, and the unit
// test project (no host, no SDK) stays fully parallel.
[assembly: CollectionBehavior(DisableTestParallelization = true)]
