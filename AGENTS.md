# Working in this repository

Instructions for anyone changing code here — human or AI agent. Sub-projects
add their own: [`app/AGENTS.md`](app/AGENTS.md),
[`api/AGENTS.md`](api/AGENTS.md).

---

## 1. What q2 is

Kudos (short name **q2**) is a self-care application: personal goals, pursued
together with friends or a community, with progress, encouragement and credit.

This repository holds the **initial version** — five working screens built from
the Kudos design: goals and the tasks under them, chats about them, friends,
and a profile. Read [README.md](README.md) sections 1 and 2 for exactly what
exists and what is deliberately absent.

The important consequence: **do not build ahead of the requirement**. No
permissions engine, no event sourcing, no microservices, no generic
abstractions waiting for a second use case. When something genuinely needs to
exist, it gets built then, with the real requirement in hand.

Planned work and known gaps are in
[docs/next-steps.md](docs/next-steps.md). Check it before starting something
substantial — it may already say why the obvious approach is the wrong one.

## 2. Repository structure

```
/
├── AGENTS.md CLAUDE.md README.md      this guidance
├── package.json                       root task runner (bun scripts)
├── scripts/                           the implementation behind those scripts
├── docs/                              architecture, testing, observability, privacy
│   └── adr/                           why decisions were made
├── app/                               Nuxt frontend
├── api/                               ASP.NET Core backend
├── deploy/                            server bootstrap and the deployment script
└── .github/workflows/                 CI and release
```

Frontend and backend are separate applications that share nothing but the HTTP
contract. They are developed, tested and built independently, and the root
scripts are the only place that knows about both.

## 3. How the two halves fit together

```
browser ──HTTP──> Nuxt server ──HTTP──> ASP.NET Core API ──EF Core──> SQLite
```

- The API owns all business rules and is the only thing that touches data.
- The frontend renders and composes; it never re-implements a rule the server
  already enforces. `isOverdue`, for example, is computed server-side so every
  client agrees on it.
- The contract is the OpenAPI document exported from the API code
  (`api/openapi/q2-api.json`), from which the frontend's TypeScript types are
  generated (`app/app/api/generated/schema.d.ts`). Both are committed.

**Changing an endpoint or a DTO means running `bun run api:openapi`; both
regenerated artefacts belong to the same change.** CI fails otherwise.

## 4. Architecture principles

- Prefer understandable code over clever code.
- Prefer explicit configuration over implicit behaviour. If something happens
  automatically, say where and why in a comment.
- Small modules with one clear responsibility, over large files that "have
  everything in one place".
- Pragmatic structure over ceremonial layering. There is no Clean Architecture
  project pyramid here, and adding one is not an improvement by itself.
- No global mutable state. No hidden side effects.
- Time and identifiers are injected (`TimeProvider`, `IIdGenerator`), never read
  from ambient state inside domain logic. This is what makes seeds and tests
  reproducible. EF Core is told so explicitly — `Q2DbContext` marks every GUID
  key `ValueGeneratedNever`, because the default silently downgrades an insert
  of a new child inside a tracked aggregate.
- Numbers people see are **derived, not stored**. A goal's percentage comes from
  its steps and a streak from the days behind it, so the figure on screen can
  never disagree with what it is a figure of.
- Every automation is documented. A script that does something surprising is a
  bug in the script or in the documentation.

## 5. Conventions

- **Language.** Code, comments, documentation and commit messages are English.
  **User-facing text is not** — the product speaks German by default and English
  on request, and every word of it lives in `app/app/i18n/messages.ts`. No
  component or page may contain a literal user-facing string; see
  [docs/adr/0010-german-first-interface.md](docs/adr/0010-german-first-interface.md).
- **Comments** explain *why*, not *what*. A comment that restates the code is
  noise; a comment that records a decision, a constraint or a trap is valuable.
- **Naming.** Say what a thing is for. `DatabaseResetGuard`, not `Helper`.
- **Formatting** is not a discussion: `dotnet format` for the backend, ESLint
  (with stylistic rules) for the frontend. Both run in `bun run validate`.
- **Mobile format.** The browser is where q2 is developed and tested; the
  product that ships is a phone app, packaged with Capacitor. So the UI is
  designed, looked at and verified **at phone width — 390 × 844** — and never
  only in a maximised desktop window: a layout that holds together solely
  because the window is wide is not finished. Details for the frontend are in
  [`app/AGENTS.md`](app/AGENTS.md) section 8.

## 6. Dependencies

Add one only when it earns its place *now*.

Wanted, and already present: Nuxt, Nuxt UI, Vue, TypeScript, ESLint, Vitest,
Playwright, `@sentry/nuxt`; ASP.NET Core, EF Core, SQLite, `Sentry.AspNetCore`,
xunit.

Not wanted without a discussion first:

- state-management libraries (`useState` and composables are enough so far)
- a UI library alongside Nuxt UI
- an ORM or repository layer on top of EF Core
- `Microsoft.EntityFrameworkCore.InMemory` — **never**; it is not a relational
  database and hides real SQL problems. Tests use the real SQLite provider.
- date/time libraries; `DateTimeOffset`, `DateOnly` and `TimeProvider` suffice
- anything that needs a running service to develop against

Backend versions are pinned centrally in `api/Directory.Packages.props`;
frontend versions are pinned exactly in `app/package.json`.

## 7. Database and seeds

The rules that matter:

- **The Development database is never wiped automatically.** Only
  `ManualTesting`, `AutomatedTest` and `E2E` may be reset, and only through
  `DatabaseResetGuard`.
- **Seeds are central and deterministic.** One class per profile in
  `api/src/Q2.Api/Infrastructure/Persistence/Seeding/`, composed with
  `SeedBuilder` so ids are handed out rather than written by hand. No
  `DateTime.UtcNow`, no `Guid.NewGuid()`, no randomness, no real personal data,
  no secrets.
- **Every seeded person has an account**, all sharing one documented password
  (`SeedAccounts`, README.md section 10). That is what makes it possible to sign
  in as *either* end of a friendship or a group chat, which is the only way to
  test that they look right from both.
- **The seeded password hash is a committed constant.** Identity's hasher salts
  randomly, and a seed has to be a pure function of its context. `SeedAccountTests`
  asserts the constant still verifies, so a framework change cannot quietly lock
  every seeded account out.
- **The AutomatedTest and E2E seeds contain no weekday-dependent task.** "How
  many tasks are on today's list" would otherwise depend on the day the suite
  runs.
- **Never insert rows** from `Program.cs`, from a migration or inline in a test.
- **Migrations are committed** and are applied in every environment, including
  test fixtures. `EnsureCreated` is not used anywhere.

See [README.md](README.md) sections 6-12 and
[docs/adr/0004-sqlite-first.md](docs/adr/0004-sqlite-first.md).

## 8. Security and privacy

- No secret is ever committed. `.env` files are ignored; `.env.example` holds
  placeholders only.
- No DSN, auth token or connection string appears in source, in a log, in a
  Sentry event or in a report.
- Internal exception detail never reaches an API client. Unexpected failures
  return a generic message plus a correlation id.
- Input is validated at the boundary *and* in the domain.
- Data minimisation is the default. If a field is not needed, it is not
  collected. Location data is not processed at all in this version.
- **Nothing about authenticating is written by hand.** The password hash, the
  security stamp, lockout and the session cookie are ASP.NET Core Identity's
  ([docs/adr/0011-authentication-with-identity.md](docs/adr/0011-authentication-with-identity.md)).
- **Every feature endpoint group carries `RequireAuthorization`,** and every
  read is scoped to the caller. A new feature is guarded by the group it is
  mapped into, not by a check somebody has to remember to write.
- **Identity goes through `CurrentPerson`.** No feature reads a claim itself.

[docs/privacy.md](docs/privacy.md) is binding, not advisory.

## 9. Logging and Sentry

- Logs describe *what happened*; Sentry collects *what went wrong*.
- Expected failures (validation, "not found", offline) are **not** errors. They
  do not produce error logs and do not create Sentry issues.
- One failure produces one Sentry event. Do not capture the same exception in
  two layers.
- Health checks produce no events.
- Never log or report user content — a goal title is personal.

[docs/observability.md](docs/observability.md) has the full rules.

## 10. Tests

Every change ships with the tests that would have caught the bug it fixes or
the regression it risks.

- **Unit** — pure logic, no I/O. Fast, parallel.
- **Component** — Vue components rendered in a real Nuxt environment.
- **Integration** — the real ASP.NET Core pipeline against SQLite in memory.
- **Migration** — every migration applied to a genuinely empty database.
- **E2E** — the real user flow through a real browser.

Tests must be independent, repeatable, order-independent, free of production
secrets, and deterministic. See [docs/testing.md](docs/testing.md).

## 11. Branches and commits

**An agent does not commit.** Leave the work in the working tree and say what
changed. Committing is the author's decision — it is where the change is
reviewed, and an agent that commits on its own removes the moment that review
would have happened. This holds for `git commit`, `git push`, tags and branches
alike, and it holds even when the change is finished, tested and obviously
correct.

The exception is an explicit instruction in the current conversation: "commit
this", "open a PR". A general "make it good", "do it properly" or "get it done"
is **not** that instruction. If a task seems to need a commit to be useful —
a release tag, say — describe the command and let the author run it.

The rest of this section is for whoever does commit:

- Work on a branch; `main` stays green.
- One logical change per commit. Subject in the imperative, under ~72
  characters. The body explains *why*.
- A commit that changes the API surface also contains the regenerated
  `api/openapi/q2-api.json` and `app/app/api/generated/schema.d.ts`.
- A commit that changes the EF model also contains the migration.
- Never commit a database file, a `.env`, a secret or a build artefact.

## 12. Central commands

```bash
bun run setup              # install everything, create the dev database
bun run dev                # API + frontend
bun run test               # everything except E2E
bun run test:e2e           # E2E with a fresh temporary database
bun run test:manual:start  # rebuild + seed the manual testing environment
bun run api:openapi        # regenerate the contract and the frontend types
bun run validate           # the full gate
```

`bun scripts/tasks.ts --list` prints every pipeline and what it runs.

## 13. Before you call a change done

An agent must actually do these, not describe them:

1. Read the relevant `AGENTS.md` — root, and `app/` or `api/`.
2. Install/restore dependencies.
3. Check formatting (`dotnet format --verify-no-changes`, `eslint .`).
4. Run linting and static analysis.
5. Run the relevant unit tests.
6. Run the relevant component/integration tests.
7. Build both projects.
8. Run E2E if a user-visible flow changed, and look at the change yourself at
   phone width — E2E runs there, but it asserts behaviour, not layout.
9. Check migrations if the EF model changed, and add one if needed.
10. Check error handling and Sentry capture for the paths you touched.
11. Confirm no sensitive data is logged or sent to Sentry.
12. Update the documentation for any changed architecture or behaviour.
13. **Report the commands you actually ran and their real results.**

`bun run validate` covers 3-10 in one command.

## 14. Definition of done

- [ ] the change does what was asked, and nothing beyond it
- [ ] `bun run validate` passes
- [ ] new behaviour has tests; changed behaviour has updated tests
- [ ] anything user-visible was checked at phone width, not only in a wide window
- [ ] the API contract and generated types are regenerated if the surface changed
- [ ] a migration exists if the EF model changed
- [ ] errors are handled, and expected failures do not create Sentry issues
- [ ] no secret, database file or personal data has been committed
- [ ] documentation and, for a significant decision, an ADR are updated
- [ ] the report names the commands run and their actual output
- [ ] the change is left uncommitted unless a commit was explicitly asked for
      (section 11)

## 15. Honesty

Do not claim a test, a build or a check succeeded unless it was actually
executed. If something could not be run, say so, say exactly which step it was,
and give the command to reproduce it. A plausible-sounding summary of work that
was not done is worse than no summary.
