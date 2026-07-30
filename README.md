# Kudos (q2)

Kudos — internal short name **q2** — is a self-care application. People set
personal goals, pursue them together with friends or inside a community, follow
each other's progress and give each other credit for it.

This repository is the **initial version**: a small, working, fully tested
reference slice of that idea. It is deliberately not a finished product. It
exists so that the next feature can be built on something that already works,
is already documented, and is already verified end to end.

- **`app/`** — the web frontend: Nuxt 4, Vue 3, TypeScript, Nuxt UI
- **`api/`** — the backend: ASP.NET Core on .NET 10, EF Core, SQLite
- **`docs/`** — architecture, testing, observability and privacy decisions
- **`scripts/`** — the task runner behind every `bun run …` command

---

## 1. What is already implemented

A vertical slice around one concept, **goals**:

| Area | What exists |
| --- | --- |
| Domain | `Goal` with title, description, status, progress, creation date, optional target date and participants; all invariants enforced in the model |
| API | `GET /api/goals` (with status filter), `GET /api/goals/{id}`, `POST /api/goals`, `GET /health`, OpenAPI document, Problem Details for every error |
| Frontend | Dashboard with goal list, status filter, create form and a goal detail page; loading, empty, error and not-found states; mobile-first (developed and tested at phone width), responsive and keyboard accessible |
| Contract | OpenAPI exported from the code, TypeScript types generated from it, both committed |
| Database | SQLite via EF Core, one initial migration, six environments, four seed profiles, guarded destructive resets |
| Errors | Central exception handling, no internal detail in responses, a correlation id the user can quote |
| Observability | Sentry in frontend and backend, separate projects and environments, privacy filters, a local recording transport for tests |
| Tests | 187 backend + 116 frontend + 15 E2E, all green |
| CI | GitHub Actions for pull requests and pushes |
| Deployment | A `v*` tag builds, verifies and deploys both applications to a Staging host, with health-gated rollback — see [docs/deployment.md](docs/deployment.md) |

## 2. What is deliberately missing

These are absent on purpose, not by oversight:

- **Authentication and accounts.** Participants are display names. Adding real
  identity is a security-relevant design step, not a quick patch — see
  [docs/adr/0006-authentication-deferred.md](docs/adr/0006-authentication-deferred.md).
- **Permissions, friendships, communities.** No roles, no sharing rules.
- **Updating or deleting goals.** Only reading and creating.
- **Location features.** Nothing collects or stores a position. See
  [docs/privacy.md](docs/privacy.md).
- **Mobile apps.** No Capacitor packaging and no native abstractions exist yet.
  The *format* is not missing, though: q2 is designed for a phone, and the
  browser is only where it is developed and tested — always at phone width. See
  [AGENTS.md](AGENTS.md) section 5.
- **PostgreSQL.** SQLite is the initial provider; no SQLite-specific business
  logic exists, so the switch is a provider change, not a rewrite.
- **Localisation.** The UI is English. Real i18n belongs behind its own layer.

---

## 3. Prerequisites

| Tool | Version used here | Why |
| --- | --- | --- |
| [Bun](https://bun.com) | 1.3.9+ | package manager, JavaScript runtime, task runner |
| [.NET SDK](https://dotnet.microsoft.com/download) | 10.0.x | backend build, test and run |
| Git | any recent | — |

Nothing else. No database server, no Docker, no global npm packages. The
`dotnet-ef` tool is pinned in `.config/dotnet-tools.json` and restored by the
setup step.

## 4. Installing

```bash
bun run setup
```

This installs the frontend dependencies, restores the .NET tools and packages,
creates `.env` files from the committed `.env.example` templates, creates the
local development database, and installs the Chromium build Playwright uses.

## 5. Running

```bash
bun run dev
```

- frontend: <http://localhost:3000>
- backend: <http://localhost:5080>
- OpenAPI document: <http://localhost:5080/openapi/v1.json>

Only one side, when that is all you need:

```bash
bun run dev:api
```

```bash
bun run dev:app
```

## 6. The development database

`bun run dev` uses a persistent SQLite file at `api/.data/q2-development.db`.
It is **never** deleted automatically. Starting the app applies pending
migrations and, if the database is empty, inserts a small development seed.

```bash
bun run db:migrate
```

```bash
bun run db:seed
```

`bun run db:reset` exists, but is **refused** in Development on purpose — your
local data is not something a script gets to throw away as a side effect. To
start over, delete the file yourself and migrate again:

```bash
rm api/.data/q2-development.db && bun run db:migrate && bun run db:seed
```

## 7. The manual testing environment

When you want a known, reproducible state to click through:

```bash
bun run test:manual:start
```

Every start of this environment **deletes** `api/.data/q2-manual-testing.db`,
recreates it, applies all migrations and inserts the ManualTesting seed — ten
goals covering shared, overdue, archived, boundary and long-text cases. Your
development database is untouched.

The destructive step is performed by the API itself (the ManualTesting
environment sets `Database:ResetOnStartup`), so the guarantee holds however it
is started. It still has to pass the reset guard described in section 12.

To see the empty state, reset without seeding:

```bash
bun run db:reset --env ManualTesting
```

## 8. Tests

```bash
bun run test          # everything except E2E
bun run test:unit     # backend + frontend unit tests
bun run test:component
bun run test:integration
bun run test:migrations
bun run test:sentry
bun run test:e2e
bun run validate      # the full gate, see section 19
```

### Integration tests use SQLite in memory

API integration tests boot the real application with
`WebApplicationFactory<Program>` against a **private SQLite in-memory
database** per test class — the real EF Core SQLite provider, not
`Microsoft.EntityFrameworkCore.InMemory`, which is not a relational database
and would not catch the SQL these tests exist to check.

An in-memory SQLite database exists only while a connection to it is open, so
the fixture holds one open for its lifetime. The schema is applied with the
**real migrations**, then the AutomatedTest seed is inserted. Everything is
rebuilt before each test, so any test can be run alone, repeatedly, in any
order.

### E2E tests use a temporary SQLite file

Frontend, backend and Playwright run as separate processes, so they need a
database on disk. Each run gets its own directory under the OS temp directory
with a unique file name (`q2-e2e-<run-id>.db`). The API creates it, applies all
migrations and inserts the E2E seed at startup; Playwright's global setup then
verifies it is really talking to a freshly seeded E2E server; the teardown
deletes the directory. No database file survives a run, and none is ever cached.

E2E uses ports 5081/3001 rather than 5080/3000, so a running `bun run dev`
cannot collide with a test run — and a test can never reach the development
database.

## 9. Migrations

Migrations are source code and are committed.

```bash
bun run db:add-migration AddGoalReminders
```

This scaffolds the migration into
`api/src/Q2.Api/Infrastructure/Persistence/Migrations/` and formats it, so
generated code matches the rest of the codebase. Apply it with:

```bash
bun run db:migrate
```

A test asserts that the EF Core model has no changes without a matching
migration, so forgetting this step fails CI rather than production.

## 10. Seeds

All seed data lives in
`api/src/Q2.Api/Infrastructure/Persistence/Seeding/`, one class per profile:

| Profile | Used by | Content |
| --- | --- | --- |
| `Development` | `bun run dev` | 3 goals; only inserted into an empty database |
| `ManualTesting` | `bun run test:manual:start` | 10 goals incl. boundary and awkward cases |
| `AutomatedTest` | API integration tests | 3 goals, minimal and deterministic |
| `E2E` | Playwright | 4 goals with stable ids and unique titles |

Seeds are **pure functions** of a `SeedContext`: no `DateTime.UtcNow`, no
`Guid.NewGuid()`, no randomness. Ids are fixed per profile
(`e2e00000-0000-4000-8000-000000000001` and so on). Dates are expressed
relative to an injected reference instant, so a goal that is meant to be
upcoming stays upcoming instead of silently becoming overdue as the calendar
moves on; a test that needs byte-identical output injects a fixed
`TimeProvider`.

To add data, edit the seed class for that profile. Never insert rows from
`Program.cs`, from a migration or from a test.

## 11. Environments

| ASP.NET Core environment | Database | On startup | Sentry environment |
| --- | --- | --- | --- |
| `Development` | `api/.data/q2-development.db` | migrate, seed if empty | `local-development` |
| `ManualTesting` | `api/.data/q2-manual-testing.db` | **delete**, migrate, seed | `manual-testing` |
| `AutomatedTest` | SQLite in memory, per test class | applied by the test fixture | `automated-test` |
| `E2E` | temp file, per run | **delete**, migrate, seed | `e2e` |
| `Staging` | supplied via configuration | nothing | `staging` |
| `Production` | supplied via configuration | nothing | `production` |

## 12. Protection against accidental data loss

A destructive reset is refused unless **five independent conditions** all hold:

1. the environment is `ManualTesting`, `AutomatedTest` or `E2E`
   (`Staging` and `Production` are rejected by name, and `Development` is not
   on the list either);
2. `Database:AllowDestructiveReset` is explicitly on and does not contradict
   the environment;
3. the provider is SQLite;
4. the target is in memory, or a file inside the repository data directory or
   the OS temp directory whose name follows the `q2-<purpose>.db` convention
   and is recognisable as a test database;
5. the configured seed profile matches the environment.

See `api/src/Q2.Api/Infrastructure/Persistence/DatabaseResetGuard.cs` and the
tests in `api/tests/Q2.Api.UnitTests/Persistence/DatabaseResetGuardTests.cs`.

---

## 13. Sentry, locally

Sentry is **not** switched off outside production. It initialises the same way
in every environment; what differs is the environment name, the sampling, and
whether events go to a real endpoint or to a local recorder.

Without a DSN the application starts normally and logs, once, that monitoring
is off. Setting `SENTRY_ENABLED=true` (or `NUXT_PUBLIC_SENTRY_ENABLED=true`)
without a DSN fails at startup with a readable message rather than pretending
to work.

To report from your machine, put a **development** DSN in `api/.env` and
`app/.env`:

```bash
# api/.env
Sentry__Dsn=https://<public-key>@<host>/<project-id>
```

```bash
# app/.env
NUXT_PUBLIC_SENTRY_DSN=https://<public-key>@<host>/<project-id>
```

Never use a production DSN locally. Events are tagged
`environment=local-development` and carry no developer name, machine name or
file path.

To check the wiring without waiting for a real incident, start the app and open
<http://localhost:3000/diagnostics>. It reports whether the backend would send
anything, and offers two buttons that trigger a synthetic server error and a
synthetic client error. The page only exists when
`NUXT_PUBLIC_DIAGNOSTICS_ENABLED` is set, and the matching backend routes are
not mapped in Staging or Production at all.

## 14. Sentry environments

Two projects, six environments:

```
q2-app          q2-api
├─ local-development
├─ manual-testing
├─ automated-test
├─ e2e
├─ staging
└─ production
```

The mapping from the ASP.NET Core environment lives in
`SentryEnvironments.cs`; the frontend derives it from
`NUXT_PUBLIC_APP_ENV`. A test asserts every mapping is distinct, so a local run
can never land in the same environment as production.

Both services report the same release id — `q2@<version-or-commit>` — so an
issue in `q2-app` can be lined up with one in `q2-api`.

## 15. Sentry in automated tests

Tests do not stub Sentry out and do not assert that a wrapper was called. They
run the **real SDK** with a local recording transport that intercepts the
serialised envelope instead of sending it. Assertions run against that payload,
so "this value is not in the event" means it would genuinely not have been
transmitted.

The tests cover: the SDK initialises in the test environment; an unexpected
error produces exactly one event; the environment and release are set;
validation failures and health checks produce none; credentials, cookies,
location data and goal content never appear; and a failing Sentry transport
does not take the API down.

```bash
bun run test:sentry
```

## 16. Source maps

The frontend build emits hidden source maps — referenced by the Sentry upload,
not by the shipped bundles. The release workflow uploads them under the same
release id as the build and deletes them from the deployed artefact afterwards.

`SENTRY_AUTH_TOKEN` is a CI secret only. It is never prefixed `NUXT_PUBLIC_`,
never committed, and never printed. Without it the upload is skipped, so local
builds and fork pull requests still succeed.

## 17. What must never be sent to Sentry

- passwords, tokens, API keys, `Authorization` headers, cookies
- database connection strings and any other secret
- full request or response bodies
- goal titles and descriptions, and any other user-authored content
- email addresses and user names
- exact location data, in events, breadcrumbs, tags or traces
- machine names, developer names and local file paths

This is enforced centrally — `SentryEventScrubber` on the backend,
`sentry.shared.ts` on the frontend — and covered by tests in both.

**Two deliberate exceptions, in the frontend only:** `sendDefaultPii` is on, so
the browser SDK attaches the IP address, and Session Replay records every
session with text and inputs masked. The backend still reports no user identity
at all. The reasoning, and what has to be decided before real users are
involved, is in [docs/privacy.md](docs/privacy.md) section 4.

---

## 18. Configuration

| File | Scope |
| --- | --- |
| `.env.example` | repository-level defaults, read by the root scripts |
| `api/.env.example` | backend settings |
| `app/.env.example` | frontend settings, loaded by Nuxt |

All three contain placeholders only. `.env` files are git-ignored. Configuration
is validated at startup: missing optional values are handled with a clear
message, and contradictory or explicitly-enabled-but-incomplete configuration
fails loudly.

## 19. Validating a change

```bash
bun run validate
```

Runs, in order and stopping at the first failure:

1. `dotnet format --verify-no-changes` — backend formatting
2. `eslint .` — frontend linting and formatting
3. `nuxt typecheck` — TypeScript in strict mode
4. backend unit tests
5. backend integration tests (API, persistence, migrations, Sentry)
6. frontend unit tests
7. frontend component tests
8. backend release build, warnings as errors
9. frontend production build
10. E2E tests against a freshly created and seeded temporary database

If the change touches the API surface, regenerate the contract first:

```bash
bun run api:openapi
```

Both `api/openapi/q2-api.json` and `app/app/api/generated/schema.d.ts` are
committed, and CI fails if they do not match the code.

---

## Further reading

- [AGENTS.md](AGENTS.md) — conventions, expectations and the definition of done
- [docs/next-steps.md](docs/next-steps.md) — what to build next, and in what order
- [docs/architecture.md](docs/architecture.md)
- [docs/testing.md](docs/testing.md)
- [docs/observability.md](docs/observability.md)
- [docs/privacy.md](docs/privacy.md)
- [docs/deployment.md](docs/deployment.md) — how a tag becomes a running release
- [docs/adr/](docs/adr/) — why things are the way they are
