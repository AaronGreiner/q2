# Testing

What is tested, how, and the properties every test here has to have.

## The levels

| Level | Where | Runs against | Speed |
| --- | --- | --- | --- |
| Backend unit | `api/tests/Q2.Api.UnitTests` | nothing — pure functions | ms |
| Backend integration | `api/tests/Q2.Api.IntegrationTests` | real pipeline, SQLite in memory | ~1 s total |
| Persistence | same project, `Category=Persistence` | SQLite in memory, migrated | ms |
| Migrations | same project, `Category=Migrations` | SQLite **file**, starting empty | ms |
| Sentry | both projects, `Category=Sentry` | the real SDK + recording transport | ms |
| Frontend unit | `app/tests/unit` | pure functions, Node environment | ms |
| Frontend component | `app/tests/component` | real Nuxt environment | ~2 s |
| E2E | `app/tests/e2e` | production build, real API, real browser | ~15 s |

```bash
bun run test              # everything except E2E
bun run test:unit
bun run test:component
bun run test:coverage      # backend lines and all frontend metrics >= 80%
bun run test:integration
bun run test:migrations
bun run test:sentry
bun run test:e2e
bun run validate          # the whole gate, in order
```

## Properties every test must have

- **Independent.** Runnable alone, in any order. Backend integration tests
  rebuild the database, the recorded Sentry events and the id sequence before
  *every* test (`ApiTestBase`), not once per class.
- **Repeatable.** The same result on the tenth run as on the first.
- **Free of ambient time.** Anything time-dependent takes the instant or the
  date as a parameter. `Goal.IsOverdue(today)`, `describeTargetDate(goal,
  today)`, `SeedContext.Now`. Integration tests inject a fixed `TimeProvider`
  so seeds and derived fields agree on one instant.
- **Signed in for real.** There is no fake authentication handler: the
  integration fixture posts to `/api/auth/login` and keeps the cookie, and the
  E2E suite fills in the real form once in `globalSetup`. A stub would let every
  test pass while the property the tests exist to protect — a request without a
  session gets nothing, one with a session gets exactly that person's data — was
  never exercised.
- **Free of ambient identity.** Ids are injected. Integration tests use a
  sequential generator, so a created goal has a predictable id.
- **Free of production secrets.** No test needs a DSN, a token or a live
  service. The Sentry tests use a placeholder DSN and a local transport.
- **Self-provisioning.** No test assumes a database exists. Every fixture
  creates its own.

## Databases in tests

The rule: **always the real EF Core SQLite provider.**
`Microsoft.EntityFrameworkCore.InMemory` is not a dependency and must not
become one — it is not a relational database and would not have caught the
`ORDER BY DateTimeOffset` failure that shaped the current mapping.

### API and persistence tests — SQLite in memory

`Q2ApiFactory` boots the real application with
`WebApplicationFactory<Program>` against
`Data Source=q2-automated-test-<guid>;Mode=Memory;Cache=Shared`. One database
per test class, so classes are isolated.

An in-memory SQLite database exists only while a connection to it is open, so
the fixture holds one open for its lifetime. The order is:

1. open the keep-alive connection
2. configure the test host (environment `AutomatedTest`, connection string,
   fixed `TimeProvider`, sequential `IIdGenerator`)
3. apply the **real migrations**
4. insert the `AutomatedTest` seed
5. run the test
6. dispose the host and the connection

Only three things are substituted, each for a stated reason. The Sentry
transport is *not* one of them: the `AutomatedTest` environment configures the
recording transport through ordinary configuration, so tests exercise the same
wiring a developer would.

### Migration tests — a real file

`MigrationTests` uses a temporary **file** under the OS temp directory,
starting genuinely absent, because "a fresh deployment works" is what it is
asserting:

1. the file does not exist
2. every migration applies, in order
3. the seed inserts
4. a row is written
5. it is read back through a new connection

Additional tests assert the EF model has no changes lacking a migration, the
generated SQL never disables foreign keys, a failed table conversion rolls
back both schema and data, and the accounts migration can be reverted and
applied again. Forgetting `bun run db:add-migration` or introducing a
non-atomic SQLite rebuild therefore fails CI rather than production.

`EnsureCreated` is not used anywhere in this repository. Test fixtures migrate,
which means the schema under test is the schema the migrations produce.

### E2E — a temporary file per run

Frontend, backend and Playwright are separate processes, so they need a file.
Each run gets its own directory under the OS temp directory and a unique name,
`q2-e2e-<run-id>.db`, which is also what the reset guard requires before it
will delete anything.

The API creates, migrates and seeds it at startup (`Database:ResetOnStartup` in
the E2E environment) — deliberately not in Playwright's `globalSetup`, because
Playwright starts the web servers *before* that hook runs and two processes
would fight over the same file. `globalSetup` instead verifies the suite is
talking to a freshly seeded E2E server, which turns a misconfiguration into one
clear message rather than a dozen confusing assertion failures.

`globalTeardown` removes the directory. No database file survives a run, and
none is ever cached.

E2E uses ports 5081/3001 rather than 5080/3000, so a running `bun run dev`
cannot collide with a test run and a test can never reach the development
database.

## Viewport

q2 is tested in a browser but ships as a Capacitor app, so **phone width — 390 ×
844 — is the format that counts**, and manual checks, bug reproductions and
screenshots are done there rather than in a maximised desktop window
([../app/AGENTS.md](../app/AGENTS.md) section 8).

Playwright has one project, `mobile-chromium`: the Pixel 7 descriptor — Chromium
because that is the one engine CI installs, plus touch and mobile emulation —
with the viewport pinned to 390 × 844. There is deliberately no desktop project.

The quality spec additionally checks horizontal overflow on every signed-in
screen and measures representative touch targets. Those checks guard basic
geometry, not visual hierarchy or spacing quality; manual inspection at the
same viewport remains necessary.

## Parallelism

- Backend **unit** tests: fully parallel.
- Backend **integration** tests: **sequential**, by assembly attribute. The
  Sentry .NET SDK keeps a *global* hub — every host built by a
  `WebApplicationFactory` calls `UseSentry` and the last initialisation wins
  process-wide. Two hosts alive at once would share one transport, and "my
  request produced exactly one event" would be reading another test's events.
  That is the SDK's design, not a flake to retry.
- Frontend unit and component tests: parallel, in separate Vitest projects.
- E2E: one worker; the suite shares one database.

## What is actually covered

### Backend (453 tests)

- **Domain** — every `Goal` invariant: title required and bounded, description
  bounded, progress 0-100, status transitions, archived goals staying archived,
  participant de-duplication and limits, `IsOverdue` at the boundary.
- **Validation** — every field, and that all problems are reported at once.
- **Reset guard** — one test per condition, each disabling exactly one so the
  "do not rely on a single boolean" claim is demonstrated rather than asserted.
- **Database location** — relative, absolute and in-memory resolution.
- **Seeds** — determinism, id stability, no overlap between profiles, the
  states the UI needs, no `@` and no secret-looking strings, and the specific
  guarantees each profile advertises.
- **API** — list, filter, empty filter, get by id, 404 as Problem Details,
  create (persisted, trimmed, participants), validation errors, malformed JSON,
  health.
- **Error handling** — no internal detail, correlation ids, the API still
  serving afterwards.
- **Persistence** — full round trip, UTC normalisation, ordering by creation
  date, status stored as text, cascade delete, the unique index enforced by the
  database itself.
- **Migrations** — empty file to working database, idempotence, sortable names,
  no pending model changes, foreign keys never disabled, atomic rollback on a
  failed conversion, and downgrade/re-apply support.
- **Sentry** — SDK initialised, exactly one event per failure, environment and
  release present, no event for validation errors / 404s / health checks, no
  credentials or location data or goal content, no machine name, no user id,
  email or username — but the IP address *present*, because `SendDefaultPii` is
  deliberately on ([privacy.md](privacy.md) section 4) and the assertion exists
  so it cannot be turned off again unnoticed — and a broken transport not
  breaking the API.

### Frontend (210 tests)

- **Presentation logic** — clamping, progress descriptions, day arithmetic at
  the boundaries, date formatting, participant phrasing.
- **Error normalisation** — every status to every kind, field errors, the
  backend `detail` never reaching the user, immutability, malformed payloads.
- **Sentry filters** — the exact functions wired into `Sentry.init`: URL query
  stripping, tokens, JWTs, emails, coordinates, request data, `ui.input`
  breadcrumbs, ignored browser noise, and that the two deliberate privacy
  exceptions stay as configured — `sendDefaultPii` on and replay recording
  every session ([privacy.md](privacy.md) section 4). Those assertions exist so
  the switches cannot be flipped back silently in either direction.
- **Components** — `GoalCard` (content, link, progress semantics, overdue,
  absent optional fields, heading level), `GoalList` (the four states, mutually
  exclusive, retry), `GoalCreateForm` (emitted payload, trimming, participant
  parsing, server field errors, double-submit prevention, reset).

### E2E (81 tests)

Authentication and registration; seeded goals, tasks and all their states;
goal creation and contribution; chats and messages; friendships and search;
profile and settings; expected and unexpected errors; Sentry privacy; PWA
assets; server-rendered content; and the phone shell. The quality spec also
requires zero reviewed WCAG A/AA findings, checks horizontal overflow on every
signed-in screen and verifies representative touch targets.

## Coverage gate

`bun run test:coverage` is a permanent gate, not a one-off report:

- Backend integration coverage instruments the production API assembly and
  requires at least 80% line coverage. The pure backend unit suite still runs
  separately because coverage does not replace its focused invariant tests.
- Frontend coverage requires at least 80% for statements, branches, functions
  and lines across the HTTP layer, composables, components, message catalogue,
  utilities and shared Sentry configuration. Route pages and layouts are
  exercised as complete screens by Playwright rather than counted as isolated
  Vitest modules.

`bun run validate` runs both thresholds, all unit tests, both production builds
and the complete E2E suite.

## What automated tests did not catch

Six defects were found by starting the application and using it, after the whole
suite was green. Worth reading before trusting a green run:

| Defect | Why the tests missed it |
| --- | --- |
| Participant order differed between the create response and later reads | Both the API test and the E2E test happened to assert names that were already in alphabetical order |
| `--ui-text-dimmed` measures 2.63:1 and fails WCAG AA | The component tests assert structure and ARIA; nothing measured contrast |
| Icons never rendered server-side; 18 "failed to load icon" per render | No test looked at the server-rendered HTML, and the browser fills them in after hydration so screenshots looked fine |
| The error panel read "Something went wrong" followed by "Something went wrong on our side" | Assertions used `toContainText`, which is true of duplicated text |
| The diagnostics page claimed "reported to Sentry" with no DSN configured | The E2E test runs *with* a DSN, so the honest-message branch was never exercised |
| The status filter was not in the URL, so it could not be shared and the back button ignored it | Nothing asserted on the URL |

The pattern: the suite verified what was expected to happen. It did not
notice repeated copy, unmeasured colour, missing server-side output, or a
missing capability. Each of those now has a regression test — but the lesson is
that a green suite is a floor, not a ceiling. Run the app.

## Writing a new test

- Assert behaviour, not implementation. "A validation failure produces no
  Sentry event" is a requirement; "the scrubber was called" is not.
- Prefer one clear assertion per behaviour over a single test that checks
  twelve things.
- Name the test after the guarantee, not the method:
  `TheOptInFlagIsNecessary`, not `TestEvaluate2`.
- If a comment is needed to explain *why* an assertion matters, write it — the
  next person will not have the context you have now.
- Sentry tests must run through the real SDK to the recording transport.
  Asserting that a wrapper method was called proves nothing about what would be
  transmitted.
