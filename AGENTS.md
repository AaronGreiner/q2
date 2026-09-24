# Working in this repository

Instructions for anyone changing code here — human or AI agent. Sub-projects
add their own: [`app/AGENTS.md`](app/AGENTS.md),
[`api/AGENTS.md`](api/AGENTS.md).

---

## 1. What q2 is

Qdos (short name **q2**, spoken "kudos") is an application for keeping the
commitments you make to yourself, by making them in front of people: you say
what you intend to do, your friends are invited to it, and they see whether it
happened.

This repository holds the **initial version**. Read [README.md](README.md)
sections 1 and 2 for exactly what exists and what is deliberately absent; what
is still to be built is in [GitHub Issues](https://github.com/AaronGreiner/q2/issues).

**The look is not negotiable per screen.** Warm neutrals, dark by default, one
selectable accent, the flame gradient only for a streak, red only for something
final, and no emoji anywhere in the interface —
[docs/adr/0028-ruhe-design-system.md](docs/adr/0028-ruhe-design-system.md).

The important consequence: **do not build ahead of the requirement**. No
permissions engine, no event sourcing, no microservices, no generic
abstractions waiting for a second use case. When something genuinely needs to
exist, it gets built then, with the real requirement in hand.

**All work goes through
[GitHub Issues](https://github.com/AaronGreiner/q2/issues)**: planned work,
known gaps, bugs, and the record of what was done and why. Work starts from an
issue and ends with the issue saying what happened — section 11 has the rules.
Read the issue before starting — it may already say why the obvious approach is
the wrong one.

## 2. Repository structure

```
/
├── AGENTS.md CLAUDE.md README.md      this guidance
├── package.json                       root task runner (bun scripts)
├── start-dev.command                  macOS one-click start; wraps those scripts
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
- Numbers people see are **derived, not stored**. A streak is counted from the
  windows behind it and a balance from their outcomes, so the figure on screen
  can never disagree with what it is a figure of.
- **A deadline is a whole day or a whole period, never a clock time**, and it is
  counted in the *owner's* time zone through `LocalCalendar` — the only place
  instants and local days are converted into each other. `DateOnly.FromDateTime(
  now.UtcDateTime)` is "today" only for somebody living in UTC
  ([docs/adr/0016](docs/adr/0016-windows-instead-of-steps.md)).
- Every automation is documented. A script that does something surprising is a
  bug in the script or in the documentation.

## 5. Conventions

- **Language.** Code, comments, documentation and commit messages are English.
  **User-facing text is not** — the product speaks German by default and English
  on request, and every word of it lives in `app/app/i18n/messages.ts`. No
  component or page may contain a literal user-facing string; see
  [docs/adr/0010-german-first-interface.md](docs/adr/0010-german-first-interface.md).
  The one exception is a mail, which is read with no app running to word it:
  its German and English live in
  `api/src/Q2.Api/Features/Accounts/PasswordResetMail.cs`
  ([docs/adr/0026-mail-and-password-reset.md](docs/adr/0026-mail-and-password-reset.md)).
- **Comments** explain *why*, not *what*. A comment that restates the code is
  noise; a comment that records a decision, a constraint or a trap is valuable.
- **Backlog.** Remaining work is a GitHub issue, never a list in a document, a
  planning paper or a comment. A document says what exists and why; where it
  has to mention a gap, it links the issue rather than describing its status.
  Unfinished work in code is marked `TODO(#123): what is missing`, always with
  its issue, and the TODO goes when the issue is closed. How an issue is
  opened, kept current and closed is section 11.
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
Playwright, `@sentry/nuxt`, `@vite-pwa/nuxt` with the `workbox-*` runtime it
needs ([docs/adr/0012-installable-pwa.md](docs/adr/0012-installable-pwa.md)),
`@microsoft/signalr` for the live connection
([docs/adr/0024-one-notification-pipeline.md](docs/adr/0024-one-notification-pipeline.md));
ASP.NET Core (SignalR included), EF Core, SQLite, `Sentry.AspNetCore`, MailKit
for SMTP ([docs/adr/0026-mail-and-password-reset.md](docs/adr/0026-mail-and-password-reset.md)),
xunit, and `Microsoft.AspNetCore.SignalR.Client` in the integration tests only.

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

## 11. Issues, branches and commits

### Issues

GitHub Issues is where q2's work is planned, argued, recorded and closed. A
document explains what exists; an issue says what is being done about what does
not. Nothing else is a backlog — not a Markdown file, not a checklist in a pull
request, not a note in a commit message, not the memory of an agent.

**Before starting.** Every change belongs to an issue. Search first, open and
closed alike — a closed issue may already record why something was left out:

```bash
gh issue list --state all --search "<words from the task>"
```

- **One fits:** read it with its comments, and work against it. If what it says
  is out of date — files moved, part of it already built, the scope no longer
  right — correct it *before* building on it: edit the description, retitle it
  if the remaining work is different, and add a comment saying what changed.
- **None fits:** open one before the work starts, in the shape the existing ones
  use — **Why** (the problem, from the person's side), **Scope** (what is in,
  with the files and seams it touches), **Done when** (checkable, at
  390 × 844 where it is visible), **Context** (current state, permalinks,
  related issues and ADRs).
- **Labels.** One kind — `enhancement`, `bug` or `documentation` — plus, where
  it applies, `launch-blocker` (must be done before q2 processes real users'
  data) or `deferred` (waits for the trigger the issue names; say the trigger).

A typo or a broken link noticed along the way may ride with the issue it was
noticed in. Anything bigger gets its own.

**While working.** The issue is where the state lives:

- A decision made along the way that an ADR does not record goes on the issue
  as a comment.
- Something found that is out of scope — a bug, stale documentation, a missing
  test — becomes a new issue linked to this one. It is neither fixed silently
  nor written into a document as a to-do.
- If the scope changes, the description changes with it; the comments say why.
- A change that also advances another issue says so there, and if it finishes
  part of it, that issue is narrowed to what remains.

**When finishing.** Comment on the issue: what changed and where, which
commands were run and what they printed, what was deliberately left out, and
what remains. That comment is the report of section 15, in the place the next
person will look for it. Work that is left uncommitted says so there, too.

**Closing.** An issue is closed by the commit that finishes it, through
`Closes #123` in the commit message, when that commit reaches `main`. A commit
that advances an issue without finishing it says `Refs #123`. An issue that
turns out to be already done, obsolete or a duplicate is closed with a comment
that gives the evidence — the commit, the file, or the other issue — never
without one.

Opening, editing, labelling and commenting on issues in this repository is part
of the work, and an agent does it without asking. Closing one is not, unless
the issue is demonstrably done on `main`, obsolete or a duplicate, as above.

### Branches and commits

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
  characters. The body explains *why*, and ends with its issue: `Closes #123`
  or `Refs #123`.
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
2. Find the issue the work belongs to, or open it, and bring it up to date
   (section 11).
3. Install/restore dependencies.
4. Check formatting (`dotnet format --verify-no-changes`, `eslint .`).
5. Run linting and static analysis.
6. Run the relevant unit tests.
7. Run the relevant component/integration tests.
8. Build both projects.
9. Run E2E if a user-visible flow changed, and look at the change yourself at
   phone width — E2E runs there, but it asserts behaviour, not layout.
10. Check migrations if the EF model changed, and add one if needed.
11. Check error handling and Sentry capture for the paths you touched.
12. Confirm no sensitive data is logged or sent to Sentry.
13. Update the documentation for any changed architecture or behaviour.
14. Open an issue for everything found out of scope.
15. **Report the commands you actually ran and their real results** — to the
    person who asked, and as a comment on the issue.

`bun run validate` covers 4-11 in one command.

## 14. Definition of done

- [ ] the change belongs to an issue, and does what was asked, and nothing
      beyond it
- [ ] the issue says what was done, what was verified and what remains;
      anything found out of scope is an issue of its own
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
