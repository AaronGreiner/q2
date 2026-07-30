# 0007 — Real providers, real pipeline, real SDK

- **Status:** Accepted
- **Date:** 2026-07-29

## Context

Tests are only worth their runtime if a passing suite means the application
actually works. The standard shortcuts — an in-memory database provider, a
mocked HTTP pipeline, a stubbed monitoring SDK — buy speed by testing something
other than what ships.

## Decision

At each level, use the real thing and substitute only what genuinely cannot run
in a test.

| Level | Real | Substituted, and why |
| --- | --- | --- |
| Backend unit | the domain, pure functions | nothing |
| Backend integration | ASP.NET Core pipeline, EF Core SQLite provider, Sentry SDK | the database is in memory (isolation); the clock is fixed (determinism); ids are sequential (assertable); the Sentry *transport* records instead of sending (no network) |
| Migrations | a real file, starting empty | nothing |
| Frontend unit | the shipped functions | nothing |
| Frontend component | a real Nuxt environment, real Nuxt UI | nothing |
| E2E | production build, real API, real browser, real SQLite file | the Sentry endpoint is intercepted in the browser |

Concretely:

- **`Microsoft.EntityFrameworkCore.InMemory` is banned.** It is not a
  relational database: no SQL translation, no constraints, no transactions. It
  would not have caught the `ORDER BY DateTimeOffset` failure that shaped the
  current mapping — exactly the class of bug a persistence test exists for.
- **`EnsureCreated` is not used anywhere.** Fixtures migrate, so the schema
  under test is the schema a deployment produces.
- **Sentry tests run through the SDK to a recording transport** and assert on
  the serialised envelope. Asserting that a wrapper was called would prove
  nothing about what would be transmitted.
- **Every test rebuilds its own state.** Backend integration tests reset the
  database, the recorded events and the id sequence before *each* test, not
  once per class.
- **Nothing depends on the current time or on a generated id.** Time and ids
  are injected everywhere, including in seeds.

## Consequences

- A green suite is meaningful: the SQL ran, the middleware ran, the SDK built
  the event.
- Tests are still fast — the whole non-E2E suite is a few seconds — because
  SQLite in memory and a fixed clock are cheap.
- Backend integration tests must run **sequentially**: the Sentry .NET SDK
  keeps a global hub, so two hosts alive at once would share one transport.
  This is the SDK's design, not a flake, and is enforced by an assembly
  attribute with the reason written next to it.
- Per-test resets cost a few milliseconds each and buy order-independence.
- E2E needs the .NET SDK and a browser available, which is why the setup script
  installs Chromium.

## Alternatives considered

- **In-memory provider for speed.** Faster, and tests the wrong thing.
- **Mocking `DbContext`.** Tests the mock's behaviour, not the query.
- **A shared test database.** Cross-test interference, order dependence, and no
  parallelism anyway.
- **Snapshot testing the rendered components.** Brittle against markup changes
  from the UI library, and it asserts appearance rather than behaviour. The
  component tests assert what the user can perceive: text, roles, ARIA state.
