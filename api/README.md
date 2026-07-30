# q2 API

The backend for Kudos (q2): ASP.NET Core on .NET 10, EF Core, SQLite.

For the product context and the full setup, start at the
[repository README](../README.md). For the rules on changing this code, read
[AGENTS.md](AGENTS.md).

## Running it

From the repository root:

```bash
bun run dev:api
```

The API listens on <http://localhost:5080>. The OpenAPI document is served at
<http://localhost:5080/openapi/v1.json> outside Staging and Production.

Directly, if you prefer:

```bash
dotnet run --project api/src/Q2.Api --no-launch-profile
```

## Endpoints

| Method | Path | Purpose |
| --- | --- | --- |
| `GET` | `/api/goals` | List goals, newest first. Optional `?status=Active\|Completed\|Archived`. |
| `GET` | `/api/goals/{id}` | A single goal. 404 as Problem Details if unknown. |
| `POST` | `/api/goals` | Create a goal. 400 with field-level errors if invalid. |
| `GET` | `/health` | Liveness and database connectivity. |
| `GET` | `/api/diagnostics/sentry` | Whether Sentry would send. Never reveals the DSN. Non-production only. |
| `GET` | `/api/diagnostics/boom` | Throws on purpose. Non-production only. |

Every error response is
[RFC 9457 Problem Details](https://datatracker.ietf.org/doc/html/rfc9457) and
carries a `traceId`; unexpected failures additionally carry an `errorId`, which
is the Sentry event id.

## Projects

```
src/Q2.Api                  the application
tests/Q2.Api.UnitTests      pure logic, no I/O
tests/Q2.Api.IntegrationTests   real pipeline, real SQLite, real Sentry SDK
```

## Common commands

```bash
dotnet build api/q2.slnx -c Release
dotnet test api/tests/Q2.Api.UnitTests
dotnet test api/tests/Q2.Api.IntegrationTests
dotnet format api/q2.slnx --verify-no-changes
```

```bash
bun run db:migrate
bun run db:seed
bun run db:add-migration <Name>
bun run api:openapi
```

## Configuration

Copy `.env.example` to `.env` and adjust. Every setting can also be supplied as
an ordinary environment variable — `ConnectionStrings__Database`,
`Sentry__Dsn`, and so on — which is how Staging and Production configure it.

The database file location, the startup policy and the reset rules are
described in [../README.md](../README.md) sections 6-12.
