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
│   │   ├── Goals/                   the domain model, DTOs, validation, service, endpoints
│   │   └── Diagnostics/             health check and the deliberate-failure endpoints
│   └── Infrastructure/
│       ├── ApiRegistration.cs       JSON, ProblemDetails, OpenAPI, CORS, pipeline
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

## 2. Feature modules

Today there is one: `Features/Goals`.

| File | Responsibility |
| --- | --- |
| `Goal.cs` | the domain model and every invariant |
| `GoalParticipant.cs` | a participant (a display name for now) |
| `GoalStatus.cs` | Active / Completed / Archived |
| `GoalContracts.cs` | `GoalResponse`, `CreateGoalRequest`, mapping |
| `CreateGoalRequestValidator.cs` | request-shape validation with good messages |
| `GoalService.cs` | application logic: read, write, map |
| `GoalConfiguration.cs` | EF Core mapping |
| `GoalEndpoints.cs` | the HTTP surface |

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
  alone.
- Queries that only read use `AsNoTracking()`.

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
| `DevelopmentSeed` | `Development` | 3 goals, inserted only into an empty database |
| `ManualTestingSeed` | `ManualTesting` | 10 goals: shared, overdue, archived, 0%/99%/100%, max-length title, long description, non-ASCII |
| `AutomatedTestSeed` | `AutomatedTest` | 3 goals, minimal and deterministic |
| `E2ESeed` | `E2E` | 4 goals with stable ids and unique, non-overlapping titles |

A seed is a **pure function** of a `SeedContext`. No clock, no randomness, no
network, no real personal data, no secrets. Ids come from `SeedIds`, which
gives each profile its own GUID prefix so two profiles can never collide.

`DatabaseSeeder` is the only thing that writes seed rows.

## 6. Test database strategy

| Scenario | Database |
| --- | --- |
| unit tests | none |
| API integration tests | SQLite **in memory**, one per test class, real provider |
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
| `DatabaseResetNotAllowedException` | 403 | no |
| `BadHttpRequestException` | 400 | no |
| anything else | 500, generic detail, `traceId` + `errorId` | yes, once |

Never put an exception message into a response: it can contain connection
strings, file paths or user input.

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
