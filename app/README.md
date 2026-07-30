# q2 frontend

The web frontend for Kudos (q2): Nuxt 4, Vue 3, TypeScript, Nuxt UI.

For the product context and full setup, start at the
[repository README](../README.md). For the rules on changing this code, read
[AGENTS.md](AGENTS.md).

## Running it

From the repository root, which also starts the API:

```bash
bun run dev
```

Frontend only, against an API that is already running:

```bash
bun run dev:app
```

<http://localhost:3000>

## What it does

- **`/`** — the dashboard: goal list with a status filter, summary counts, and
  a form to create a goal. Renders loading, empty, error and loaded states.
- **`/goals/{id}`** — a single goal with its progress, target date and
  participants.
- **`/diagnostics`** — synthetic failure triggers for verifying error handling
  and Sentry. Only present when `NUXT_PUBLIC_DIAGNOSTICS_ENABLED` is set.

## Structure

```
app/api/          the typed HTTP layer; the only place that talks to the API
app/components/   goals/ (feature) and ui/ (generic)
app/composables/  state and side effects
app/utils/        pure presentation logic
app/pages/        routes; they compose, they do not implement rules
```

## Commands

```bash
bun run dev
bun run build
bun run preview
bun run lint
bun run typecheck
bun run test
bun run test:e2e
```

## Configuration

Copy `.env.example` to `.env`. Everything prefixed `NUXT_PUBLIC_` is compiled
into the browser bundle and is therefore public — never put a secret behind
that prefix. `SENTRY_AUTH_TOKEN` is a build-time CI secret and must never be
exposed to the client.
