# 0004 — SQLite first, PostgreSQL-ready

- **Status:** Accepted
- **Date:** 2026-07-29

## Context

The repository must run locally with no external database infrastructure, and
must stay testable without one. PostgreSQL is the likely production choice
later, so today's decisions must not make that switch expensive.

## Decision

**SQLite via EF Core**, for development, manual testing, automated tests and
E2E. No SQLite-specific business logic anywhere.

Rules that keep the door open:

- **No raw SQL** in application code. Everything goes through EF Core LINQ.
- **No provider-specific column types** in the entity configuration.
- **Enums stored as text** (`HasConversion<string>()`), so the database is
  readable and reordering enum members cannot silently rewrite history.
- **Instants normalised to UTC at the storage boundary.** `Goal.CreatedAt` is a
  `DateTimeOffset` in the model and a UTC `DateTime` in the column.
- **Migrations are committed** and applied everywhere, including in test
  fixtures. `EnsureCreated` is not used at all, so the schema under test is the
  schema the migrations produce.
- **The real SQLite provider in tests**, never
  `Microsoft.EntityFrameworkCore.InMemory`.

## Known SQLite limitations, and how they are handled

- **`ORDER BY` on `DateTimeOffset` is not supported.** EF Core throws
  `SQLite does not support expressions of type 'DateTimeOffset' in ORDER BY
  clauses`. This is not hypothetical: listing goals newest-first failed until
  the UTC conversion above was added. It also maps cleanly onto PostgreSQL's
  `timestamp with time zone`.
- **Limited `ALTER TABLE`.** EF rebuilds tables for many schema changes, so
  generated migrations should be read rather than assumed.
- **Dynamic typing.** SQLite does not enforce column types the way PostgreSQL
  does, so a mapping bug can pass locally and fail later. Mitigated by keeping
  the mapping provider-neutral and by asserting round trips in the persistence
  tests.
- **Single writer.** Irrelevant at this scale; relevant on the day concurrency
  matters, which is also the day PostgreSQL arrives.
- **Fewer functions and no `citext`.** Case-insensitive comparison is done in
  the domain (participant names), not by the database.

## Consequences

- `git clone && bun run setup && bun run dev` works with no Docker, no server
  and no credentials.
- Test suites are fast: in-memory SQLite per test class, real SQL, no container
  startup.
- The database file is a real artefact on disk, which is why the destructive
  reset guard exists.
- SQLite's laxness means some classes of bug will only be caught by
  PostgreSQL-specific tests once that provider arrives.

## When PostgreSQL is introduced

1. Add `Npgsql.EntityFrameworkCore.PostgreSQL` and select the provider by
   configuration.
2. Generate a provider-specific migration set; SQLite and PostgreSQL cannot
   share one.
3. **Add provider-level integration tests against a real temporary PostgreSQL
   instance** — Testcontainers or an equivalent. SQLite remains valid for fast
   local tests but must not stand in for those.
4. Review the reset guard: it currently requires the SQLite provider, which is
   correct today and would need widening deliberately, not accidentally.
5. Re-check the UTC mapping and the text-stored enums against
   `timestamptz` and native enum types.

## Alternatives considered

- **PostgreSQL from the start (Docker).** Closer to production, at the cost of
  requiring Docker for every contributor and every test run, and slower
  feedback. Premature while the schema is one table.
- **`Microsoft.EntityFrameworkCore.InMemory`.** Explicitly rejected. It is not
  a relational database: no constraints, no SQL translation, no transactions.
  It would not have caught the `ORDER BY` failure above — which is precisely
  the kind of bug a persistence test exists to catch.
- **LiteDB or a document store.** The data is relational; a relational database
  is the honest fit.
