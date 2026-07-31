# Working on the q2 backend

Applies to everything under `api/`. The repository-wide rules in
[../AGENTS.md](../AGENTS.md) apply as well; this file adds what is specific to
the API.

---

## 1. Architecture

ASP.NET Core minimal APIs on .NET 10, EF Core with SQLite. One project, one
assembly, organised by **feature** rather than by technical layer:

```
api/
├── q2.slnx                          solution
├── Directory.Build.props            shared build settings (warnings-as-errors in Release)
├── Directory.Packages.props         every NuGet version, centrally pinned
├── openapi/q2-api.json              exported contract (committed)
├── src/Q2.Api/
│   ├── Program.cs                   composition root, ~35 lines
│   ├── appsettings.*.json           one file per environment
│   ├── Features/
│   │   ├── Accounts/                registration, sign-in, the Identity user
│   │   ├── Activity/                the feed, kudos and the leaderboard
│   │   ├── Chats/                   conversations, messages and reactions
│   │   ├── Diagnostics/             health check and the deliberate-failure endpoints
│   │   ├── Goals/                   goals, the tasks under them, and their contracts
│   │   ├── People/                  Person, friendships, badges, CurrentPerson
│   │   ├── Profile/                 the signed-in person's own screen
│   │   ├── Settings/                theme, language and notification preferences
│   │   └── Streaks/                 the one definition of a streak, shared by both
│   └── Infrastructure/
│       ├── ApiRegistration.cs       JSON, ProblemDetails, OpenAPI, CORS, pipeline
│       ├── AuthenticationRegistration.cs  Identity + the session cookie
│       ├── CommandLineRunner.cs     `db …` and `openapi …` commands
│       ├── ApplicationEnvironments.cs
│       ├── Errors/                  exception types + the global handler
│       ├── Observability/           Sentry configuration, scrubbing, recording transport
│       ├── Persistence/             DbContext, options, reset guard, maintenance, migrations
│       │   └── Seeding/             one class per seed profile
│       └── Time/                    IIdGenerator (time comes from TimeProvider)
└── tests/
    ├── Q2.Api.UnitTests/            pure logic, no I/O, parallel
    └── Q2.Api.IntegrationTests/     real pipeline + real SQLite; sequential (see §9)
```

A feature owns its model, contract, validation, persistence configuration and
endpoints. Adding one means adding a folder under `Features/`, not editing five
layer projects.

### Deliberately absent

- No repository or unit-of-work abstraction over EF Core. `DbContext` already
  is both; wrapping it adds indirection without adding a testable behaviour.
  See [../docs/adr/0003-backend-architecture.md](../docs/adr/0003-backend-architecture.md).
- No MediatR, no CQRS split, no separate Domain/Application/Infrastructure
  assemblies.
- No `AutoMapper`. Mapping is a static method on the response DTO.

### Rules

- **Never return an EF Core entity from an endpoint.** DTOs are the contract.
- **No business logic in endpoints.** Bind, validate, delegate, map.
- **No SQLite-specific business logic.** Provider quirks are handled in the EF
  configuration, so switching to PostgreSQL stays a provider change.
- Domain invariants live in the domain type, not in the service.
- `Goal.Create` takes its id and timestamp as arguments. Nothing in the domain
  reads the clock or generates an id.
- **Numbers people see are derived, never stored.** A goal's percentage comes
  from its steps; a streak comes from the days recorded behind it. A stored copy
  is a second source of truth waiting to drift, and it needs a nightly job to
  notice a missed day.
- **Identity goes through `CurrentPerson`.** It resolves the person behind the
  signed-in account and fails loudly rather than guessing; no feature reads a
  claim itself
  ([../docs/adr/0011-authentication-with-identity.md](../docs/adr/0011-authentication-with-identity.md)).
- **Every feature endpoint group carries `RequireAuthorization`.** The guard is
  on the group, not on each route, so a new endpoint is guarded by where it is
  mapped rather than by somebody remembering. `AccountEndpointTests` walks every
  group anonymously and asserts 401.
- **Every read is scoped to the caller.** `Goal` and `GoalTask` have an
  `OwnerPersonId`; a conversation is scoped by its participants. Where the
  existence of a row is itself private, the answer is 404 rather than 403.
- **Nothing about authenticating is written by hand** — see `Features/Accounts`,
  which is `UserManager` and `SignInManager` and no cryptography.
- **Sentences are composed by the client.** The API sends `kind`, `subject` and
  `amount`; it never sends a line of prose. The app ships in two languages
  ([../docs/adr/0010-german-first-interface.md](../docs/adr/0010-german-first-interface.md)).

## 2. Feature modules

Every feature has the same shape: entities and their invariants, an EF
configuration, contracts with their mapping, a service, and endpoints. Taking
`Features/Goals` as the reference:

| File | Responsibility |
| --- | --- |
| `Goal.cs` | the domain model and every invariant |
| `GoalParticipant.cs` | a participant, and the days a goal was worked on |
| `GoalTask.cs` | one thing to do on a day, and which days it falls on |
| `GoalStatus.cs` `GoalRhythm.cs` | the two enums, stored as text |
| `GoalContracts.cs` `GoalTaskContracts.cs` | responses, requests, mapping |
| `GoalRequestValidators.cs` | request-shape validation with good messages |
| `GoalService.cs` `GoalTaskService.cs` | application logic: read, write, map |
| `GoalConfiguration.cs` | EF Core mapping |
| `GoalEndpoints.cs` | the HTTP surface for `/api/goals` and `/api/tasks` |

The others follow it: `People` (with `CurrentPerson` and `FriendsService`),
`Activity` (feed, kudos, leaderboard, and `ActivityRecorder`, which goals and
tasks both publish through), `Chats`, `Profile`, `Settings`. `Streaks` is the
odd one out — a single static class, because "how many days in a row" is one
definition that both a person and a goal are counted with, and two copies of it
would drift.

## 3. EF Core conventions

- Configuration through `IEntityTypeConfiguration<T>`, discovered by assembly
  scanning. Never inline in `OnModelCreating`.
- Enums are stored **as text** (`HasConversion<string>()`) so the database is
  readable and reordering members cannot rewrite history.
- Instants are normalised to UTC at the storage boundary. `Goal.CreatedAt` is a
  `DateTimeOffset` in the model and a UTC `DateTime` in the column — SQLite
  cannot `ORDER BY` a `DateTimeOffset` at all, and UTC maps cleanly onto
  PostgreSQL's `timestamptz` later.
- Explicit lengths and indexes. Foreign keys cascade where a child cannot exist
  alone, and a reference that must survive its target (a pinned goal on a
  conversation) is `SetNull` instead.
- Queries that only read use `AsNoTracking()`.
- **Every GUID key is `ValueGeneratedNever`**, set once in
  `Q2DbContext.OnModelCreating`. EF Core's default is `ValueGeneratedOnAdd`, and
  with it a *new* child discovered inside a tracked aggregate — a check-in on a
  `Person`, a kudos on an `ActivityEvent` — reads as a row that already exists.
  Nothing fails; the insert is silently downgraded and a streak simply never
  grows. `PersistenceTests` has a regression test for it.

## 4. Migrations

```bash
bun run db:add-migration <Name>
```

Scaffolds into `Infrastructure/Persistence/Migrations/` and runs
`dotnet format` afterwards, because EF generates block-scoped namespaces that
the style gate rejects.

- Migrations are committed and are never edited after being applied anywhere
  beyond a local machine.
- `MigrationTests` applies every migration to a genuinely empty file, seeds,
  writes and reads back — and asserts the model has no changes lacking a
  migration.
- `EnsureCreated` is not used anywhere. Test fixtures migrate.
- **SQLite limitations to keep in mind:** no `ORDER BY` on `DateTimeOffset`
  (hence the conversion above); limited `ALTER TABLE`, so EF rebuilds tables
  for many schema changes — review generated migrations rather than assuming.

## 5. Seed profiles

`Infrastructure/Persistence/Seeding/`, one class per profile, all implementing
`ISeedDataSource`:

| Class | Profile | Content |
| --- | --- | --- |
| `DevelopmentSeed` | `Development` | the demonstration world, inserted only into an empty database |
| `ManualTestingSeed` | `ManualTesting` | the same world plus completed, overdue, archived and a title long enough to wrap |
| `AutomatedTestSeed` | `AutomatedTest` | the smallest world that still covers every branch |
| `E2ESeed` | `E2E` | stable ids and unique, non-overlapping titles for Playwright |

`KudosWorld` composes the world Development and ManualTesting share, so "what a
working q2 looks like" does not have two slightly different answers.

A seed is a **pure function** of a `SeedContext` — no clock, no randomness, no
network, no real personal data, no secrets — built through `SeedBuilder`, which
hands out ids so a seed only has to say what exists. `SeedIds` gives each
profile its own GUID prefix and each kind of row the group after it, so two
profiles can never collide and a row's origin is obvious at a glance.

Properties every profile has to keep, all covered by `SeedDataTests`:

- **every person has exactly one account, and every account one address.**
  Identity finds an account by its normalised address, so a duplicate would make
  signing in a coin toss — and a person without an account cannot be signed in
  as, which is what the two-sided tests need;
- **no two people are connected twice.** One friendship row per pair, either way
  round; a second would hit the unique index and take the whole seed with it;
- **every goal and task belongs to somebody in the same world;**
- **AutomatedTest and E2E contain no weekday-dependent task.** A `Weekdays` or
  `Weekly` task would make "how many tasks are on today's list" depend on the
  day the suite runs, and a test that passes on Tuesday and fails on Saturday is
  worse than no test.

The seeded password lives in `SeedAccounts`, along with the reason its hash is a
committed constant rather than something computed while seeding.

`DatabaseSeeder` is the only thing that writes seed rows.

## 6. Test database strategy

| Scenario | Database |
| --- | --- |
| unit tests | none |
| API integration tests | SQLite **in memory**, one per test class, real provider |
| signing a test in | the real `/api/auth/login`, keeping the cookie (`SignIn.AsAsync`) |
| persistence tests | SQLite in memory, migrated |
| migration tests | SQLite **file** in the OS temp directory, starting empty |
| E2E | SQLite file, unique per run, created and seeded by the API at startup |

`Microsoft.EntityFrameworkCore.InMemory` is **not a dependency and must not
become one**. It is not a relational database: it would not have caught the
`ORDER BY DateTimeOffset` failure that shaped the mapping above.

An in-memory SQLite database exists only while a connection is open, so
fixtures hold one for their lifetime (`SqliteTestDatabase`, `Q2ApiFactory`).

## 7. API contract and DTOs

- `GoalResponse` is the contract. Changing it changes the OpenAPI document and
  the generated frontend types — regenerate with `bun run api:openapi`; both
  belong to the same change.
- Request DTOs have nullable properties with defaults, so a missing field
  produces our own field-level message instead of a model-binding failure.
- Enums travel as names; `JsonNumberHandling.Strict` is set so numeric fields
  keep one honest type in the OpenAPI 3.0 schema.
- Diagnostics endpoints are excluded from the document on purpose: they only
  exist outside Staging and Production, and a contract that changes depending
  on where it was exported would be worse than none.

## 8. Validation and error handling

Two layers, on purpose:

1. `CreateGoalRequestValidator` turns a request into field-level messages,
   reading its limits from the domain constants so the numbers have one source.
2. `Goal.Create` enforces the same invariants regardless of caller.

`GlobalExceptionHandler` is the only place that maps an exception to a
response, and the only place that reports to Sentry:

| Exception | Response | Sentry |
| --- | --- | --- |
| `DomainValidationException` | 400 + field errors | no |
| `ResourceNotFoundException` | 404 | no |
| `AuthenticationRequiredException` | 401 + a machine-readable `reason` | no |
| `AccessDeniedException` | 403 | no |
| `DatabaseResetNotAllowedException` | 403 | no |
| `BadHttpRequestException` | 400 | no |
| anything else | 500, generic detail, `traceId` + `errorId` | yes, once |

Never put an exception message into a response: it can contain connection
strings, file paths or user input.

A 401 carries `reason` — one of `AuthenticationFailures` — as a problem-details
extension. Structure, not prose: the client picks the sentence, because the app
speaks two languages.

## 9. Tests

```bash
bun run test:api            # everything
bun run test:unit           # backend + frontend unit tests
bun run test:integration
bun run test:migrations     # --filter Category=Migrations
bun run test:sentry         # --filter Category=Sentry
```

Traits in use: `Unit`, `Integration`, `Persistence`, `Migrations`, `Seed`,
`Sentry`.

**Integration tests run sequentially.** `[assembly: CollectionBehavior(
DisableTestParallelization = true)]` is set because the Sentry .NET SDK keeps a
*global* hub: every host built by a `WebApplicationFactory` calls `UseSentry`,
and the last initialisation wins process-wide. Two hosts alive at once would
share one transport, and "my request produced exactly one event" would be
reading another test's events. Unit tests stay fully parallel.

`ApiTestBase` rebuilds the database, the recorded events and the id sequence
before **every** test, so any test can run alone, repeatedly, in any order.

## 10. Sentry and logging

- `SentrySettings.FromConfiguration` resolves everything and fails loudly on
  contradictory configuration (enabled without a DSN; recording transport in a
  protected environment).
- `SentryEventScrubber` is the single `BeforeSend`/`BeforeBreadcrumb`. It drops
  health checks, aborted requests and expected failures, and removes cookies,
  headers, query strings, bodies, user id/email/username, machine name and
  anything matching a credential or coordinate pattern.
- **`SendDefaultPii` is `true`, so the IP address is reported on purpose.** Do
  not reinstate `User.IpAddress = null` in `ScrubUser`: it would silently undo
  the option while the configuration still reads true. Everything else about
  the user is still cleared. See [../docs/privacy.md](../docs/privacy.md).
- **Debug symbols and sources are uploaded to Sentry** by the MSBuild targets
  configured in `Q2.Api.csproj`, gated on `SENTRY_AUTH_TOKEN` being present, so
  a build without secrets never invokes `sentry-cli`. If a stack trace in Sentry
  has no line numbers, that gate is the first thing to check. A *wrong* token
  behaves differently from a missing one: the build fails with
  `sentry reported an error: Invalid org token`, which is a credential problem,
  not a compilation one.
- `RecordingTransport` replaces only the network call, so tests exercise the
  real SDK end to end. It refuses to be enabled in Staging or Production.
- Structured logging: single-line console locally, JSON elsewhere. Request
  logging with headers or bodies is deliberately not enabled.

## 11. Commands

```bash
dotnet restore api/q2.slnx
dotnet build api/q2.slnx -c Release          # warnings are errors
dotnet format api/q2.slnx                    # fix formatting
dotnet format api/q2.slnx --verify-no-changes
dotnet test api/tests/Q2.Api.UnitTests
dotnet test api/tests/Q2.Api.IntegrationTests
```

Database and contract, through the root scripts:

```bash
bun run db:migrate
bun run db:seed
bun run db:reset --env ManualTesting
bun run db:reset-and-seed --env ManualTesting
bun run db:add-migration <Name>
bun run api:openapi
```

Directly, if you need to see the raw output:

```bash
dotnet run --project api/src/Q2.Api --no-launch-profile -- db migrate
```

`--no-launch-profile` matters: `launchSettings.json` pins
`ASPNETCORE_ENVIRONMENT=Development` and would silently override the
environment you set.

## 12. Before finishing a backend change

1. `dotnet format api/q2.slnx --verify-no-changes`
2. `dotnet build api/q2.slnx -c Release`
3. `dotnet test api/tests/Q2.Api.UnitTests`
4. `dotnet test api/tests/Q2.Api.IntegrationTests`
5. if the EF model changed: `bun run db:add-migration <Name>`, keeping the migration
6. if the API surface changed: `bun run api:openapi`, keeping both artefacts
7. confirm no expected failure creates a Sentry issue, and no sensitive data
   reaches one

Or simply `bun run validate`, and report what it actually printed.
