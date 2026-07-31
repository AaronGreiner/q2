# CLAUDE.md — backend

**Read [AGENTS.md](AGENTS.md) in this folder first**, and
[../AGENTS.md](../AGENTS.md) for the repository-wide rules. This file only adds
Claude Code specifics.

## Fast orientation

```
src/Q2.Api/Program.cs                                composition root, read this first
src/Q2.Api/Features/Goals/Goal.cs                    the domain model and its rules
src/Q2.Api/Features/Goals/GoalEndpoints.cs           the HTTP surface
src/Q2.Api/Infrastructure/ApiRegistration.cs         services + middleware order
src/Q2.Api/Infrastructure/AuthenticationRegistration.cs  Identity + the session cookie
src/Q2.Api/Features/People/CurrentPerson.cs         who is asking, and the only place that answers
src/Q2.Api/Infrastructure/Errors/GlobalExceptionHandler.cs   every error response
src/Q2.Api/Infrastructure/Persistence/DatabaseResetGuard.cs  why a reset is refused
src/Q2.Api/Infrastructure/Observability/SentryEventScrubber.cs  what never reaches Sentry
```

## Traps

- **`--no-launch-profile` is required** when running the API with an explicit
  environment. `launchSettings.json` sets `ASPNETCORE_ENVIRONMENT=Development`
  and silently wins otherwise.
- **`Urls` is not in `appsettings.json`.** A value there beats
  `ASPNETCORE_URLS`, which would stop E2E and deployments from choosing their
  own address. Development and ManualTesting pin a port; everything else uses
  the environment variable.
- **EF scaffolding generates block-scoped namespaces**, which the style gate
  rejects as an error. `bun run db:add-migration` runs `dotnet format`
  afterwards — use it rather than calling `dotnet ef` directly.
- **Integration tests are sequential by design** (global Sentry hub). Do not
  "optimise" that by re-enabling parallelisation.
- **`ApiTestBase.Client` arrives signed in.** `AnonymousClient` is the one
  without a session, and `ClientForAsync(email)` is how a test becomes the other
  end of a friendship. There is no fake authentication handler and adding one
  would defeat the tests that matter.
- **The seeded password hash is a constant** in `SeedAccounts`, because seeds
  are pure and Identity's hasher salts randomly. `SeedAccountTests` is what
  would notice it going stale.
- **Relative `Data Source` paths** are resolved against `api/.data` by
  `DatabaseLocation`, not against the process working directory. `Q2_DATA_DIR`
  overrides it.
- **The OpenAPI export briefly starts the host** on port 0, because routes only
  reach the endpoint data sources once the app starts.

## Verifying a change

```bash
bun run validate
```

Backend only, when iterating:

```bash
dotnet test api/tests/Q2.Api.UnitTests --nologo
```

```bash
dotnet test api/tests/Q2.Api.IntegrationTests --nologo
```

Report the commands you actually ran and their real output — never a summary of
checks that were not executed.
