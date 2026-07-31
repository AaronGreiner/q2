# Architecture

How q2 is put together, and why. Decisions with a lasting consequence get their
own record in [adr/](adr/).

## The shape

```
┌──────────┐   HTTP    ┌──────────────┐   HTTP    ┌───────────────┐  EF Core  ┌────────┐
│ browser  │ ────────► │ Nuxt server  │ ────────► │ ASP.NET Core  │ ────────► │ SQLite │
│ (Vue)    │ ◄──────── │ (Nitro, SSR) │ ◄──────── │ API           │ ◄──────── │        │
└──────────┘           └──────────────┘           └───────────────┘           └────────┘
     │                                                    ▲
     └────────────────── HTTP (client-side) ──────────────┘
```

Two applications, one contract. They share no code, no database and no
deployment. The only coupling is the OpenAPI document, and it is generated from
the backend and consumed by the frontend as generated types — so a breaking
change is a compile error, not a runtime surprise.

The frontend calls the API both from the server (during SSR) and from the
browser (after hydration), which is why `NUXT_PUBLIC_API_BASE_URL` has to be
reachable from both.

## Who owns what

| Concern | Owner |
| --- | --- |
| business rules, invariants | the API, in the domain model |
| persistence, migrations, seeds | the API |
| derived facts (`isOverdue`, `progressPercent`, a streak, "is this on today's list") | the API, so every client agrees |
| the contract | the API, exported as OpenAPI |
| rendering, composition, navigation | the frontend |
| presentation logic (formatting, phrasing, language) | the frontend, in pure functions |
| error *classification* | the API (status codes) |
| error *wording for humans* | the frontend |

The frontend never re-implements a rule the server already enforces. It does
not decide whether a goal is overdue, how far along it is, or whether a task
belongs on today's list; it renders the answer.

The line runs the other way too: the API never sends a sentence. It sends the
parts of one — `kind`, `subject`, `amount` — because the app speaks German and
English and a phrase composed on the server could only ever be one of them.

## Backend

Minimal APIs on .NET 10, organised by feature.

```
Features/Activity/       the feed, kudos and the leaderboard
Features/Chats/          conversations, messages, reactions
Features/Goals/          goals and the tasks under them
Features/People/         Person, friendships, badges, CurrentPerson
Features/Profile/        the signed-in person's own screen
Features/Settings/       theme, language, notification preferences
Features/Streaks/        one definition of "days in a row", shared by both
Features/Diagnostics/    health check, deliberate-failure endpoints
Infrastructure/          persistence, errors, observability, time, CLI, registration
```

Each of those holds a model, its contracts, validation, a service, an EF
configuration and endpoints.

A feature owns its whole vertical. Adding one means adding a folder, not
editing a stack of layer projects. There is no repository abstraction over EF
Core, no MediatR and no separate Domain/Application/Infrastructure assemblies —
see [adr/0003-backend-architecture.md](adr/0003-backend-architecture.md).

The domain model is the authority on its own rules. `Goal.Create` validates
everything and takes its id and timestamp as **arguments** rather than reading
them from ambient state, which is what makes seeds and tests reproducible.

Numbers people see are **derived, not stored**: a goal's percentage comes from
its steps, a streak from the days recorded behind it, "done today" from the day
a task was last completed. A stored copy would be a second source of truth, and
a stored streak would need a nightly job to notice a missed day.

Identity goes through one type, `CurrentPerson`, because there is no
authentication yet and exactly one row is flagged as the signed-in person —
see [adr/0009-single-known-person.md](adr/0009-single-known-person.md).

`Program.cs` is about thirty-five lines and reads as a table of contents:
observability, persistence, API services, pipeline, endpoints, run.

## Frontend

Nuxt 4 with SSR. Layers, from the outside in:

```
pages/         compose a view, own the wiring, hold no rules
layouts/       the phone shell: default (with the tab bar) and plain (without)
components/    render; typed props in, typed events out, no fetching
composables/   state and side effects (useHome, useQ2Api, useErrorReporter)
api/           the only place that speaks HTTP
i18n/          every user-facing word, in German and English
utils/         pure presentation logic
```

The catalogue is why the API sends structure rather than sentences: the feed
sends `kind`, `subject` and `amount`, and the client composes the line in
whichever language is selected
([adr/0010-german-first-interface.md](adr/0010-german-first-interface.md)).

Nuxt UI is a foundation, not an architecture: feature components render domain
objects and happen to use `U*` primitives, rather than being defined by them.

See [adr/0002-frontend-architecture.md](adr/0002-frontend-architecture.md).

## The contract

```
api/src/Q2.Api/Features/**            endpoints and DTOs
  └─► api/openapi/q2-api.json         exported, committed
        └─► app/app/api/generated/schema.d.ts   generated, committed
```

`bun run api:openapi` regenerates both. The export briefly starts the host on
port 0, because routes only reach the endpoint data sources once the
application starts.

Two deliberate choices in the contract:

- **OpenAPI 3.0, not 3.1** — best supported by client generators, and nothing
  here needs 3.1.
- **`JsonNumberHandling.Strict`** — the ASP.NET Core web defaults also accept
  numbers written as strings, which makes every numeric property a
  string-or-number union that OpenAPI 3.0 cannot express; it drops the type and
  the generated client ends up with `unknown`.

Diagnostics endpoints are excluded from the document: they exist only outside
Staging and Production, and a committed contract that changes depending on
where it was exported from would be worse than none.

## Errors

One path, on each side.

**Backend** — `GlobalExceptionHandler` is the only place that maps an exception
to a response and the only place that reports to Sentry. Expected failures
become 4xx and are never reported. Unexpected ones become a 500 with a generic
message, a `traceId` and an `errorId` (the Sentry event id) — never an
exception message, which can contain connection strings, paths or user input.

**Frontend** — `normalizeApiError` turns anything a fetch can throw into an
`ApiError` with a `kind`. Components receive `ApiFailure`, plain data, because
only what `useAsyncData` returns survives the SSR payload; a class instance
would arrive without its prototype.

## Data

SQLite through EF Core, chosen so the repository runs with no infrastructure at
all. Nothing in the business logic is SQLite-specific, so PostgreSQL later is a
provider change — see [adr/0004-sqlite-first.md](adr/0004-sqlite-first.md).

Six environments, each with its own database and startup policy; four seed
profiles; destructive resets guarded by five independent conditions. The detail
is in [../README.md](../README.md) sections 6-12.

## Observability

Sentry in both applications, in **separate projects** (`q2-app`, `q2-api`) with
**one release id** so issues correlate. It is not disabled outside production;
what changes per environment is the environment name, the sampling, and whether
events go to a real endpoint or a local recorder.

See [observability.md](observability.md) and
[adr/0005-observability-and-sentry.md](adr/0005-observability-and-sentry.md).

## What the architecture keeps possible

- **Capacitor for Android and iOS.** The frontend is a Nuxt application talking
  to an HTTP API over a configurable base URL, with no server-only assumptions
  in the UI layer. No mobile abstractions have been added now — there is
  nothing to abstract yet. What *is* already treated as fixed is the form
  factor: the UI is built and tested at phone width, because that is what the
  packaged app will be ([../app/AGENTS.md](../app/AGENTS.md) section 8).
- **PostgreSQL.** Provider-neutral EF configuration, committed migrations, no
  raw SQL.
- **Authentication.** The domain has no user concept to unpick, and the error
  taxonomy already has an `unauthorized` kind. See
  [adr/0006-authentication-deferred.md](adr/0006-authentication-deferred.md).
- **More features.** Each is a folder in `Features/` and a folder in
  `components/`.

## What it deliberately does not do

No microservices, no message bus, no event sourcing, no CQRS, no plugin system,
no generic `Repository<T>`, no abstraction with a single implementation and no
second use case in sight. Every one of those would be real work today for a
benefit nobody has asked for yet.
