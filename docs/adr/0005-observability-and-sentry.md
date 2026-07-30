# 0005 — Sentry in every environment, filtered centrally

- **Status:** Accepted
- **Date:** 2026-07-29

## Context

Error reporting has to be trustworthy on the day it matters. The common
pattern — initialise Sentry only in production — produces an integration that
nobody has ever seen work, whose filters have never been exercised, and whose
first real test is an incident.

At the same time, this application handles content that must never leave the
process: goal titles and descriptions are personal and may be health-related.

## Decision

**The SDK initialises identically in every environment.** What differs is the
environment name, the sampling, and whether events reach a real endpoint or a
local recorder.

- **Two projects**, `q2-app` and `q2-api`, so a frontend regression and a
  backend regression are never the same issue.
- **Six environments** per project, mapped from the application environment in
  exactly one place, with a test asserting every mapping is distinct.
- **One release id** shared by both services, `q2@<version-or-commit>`, derived
  deterministically — never random, or Sentry cannot group two starts of the
  same build.
- **One central filter per side** (`SentryEventScrubber`, `sentry.shared.ts`)
  wired in as `beforeSend`/`beforeBreadcrumb`, unit-tested directly so the
  tests exercise the shipped configuration.
- **Expected failures never become issues.** Validation errors, 404s, aborted
  requests, health checks and offline requests are normal use.
- **One failure, one event.** The backend reports from a single place and
  reports *before* logging, because the logging integration also creates events
  and whichever call happens first wins.
- **A recording transport for tests** that replaces only the network call, so
  assertions run against the actual serialised envelope.
- **Missing configuration degrades honestly**: no DSN means the app runs and
  says so; `enabled` without a DSN fails loudly.

Explicitly not done: dropping events by environment. The environment separation
exists to make local and test events *visible*, not to hide them.

## Consequences

- "Is reporting working?" is answerable on a laptop, via `/diagnostics`.
- The privacy filters are covered by unit, integration and E2E tests, so
  "no user content reaches Sentry" is a verified property rather than a hope.
- Fork pull requests validate the full integration without any secret.
- Local development can produce real Sentry traffic if a developer configures a
  DSN — which is the point, but means the `local-development` environment needs
  its own project quota consideration.
- The global-hub design of the Sentry .NET SDK forces backend integration tests
  to run sequentially. Accepted, and documented where it bites.

## Alternatives considered

- **Sentry only in production.** Untested filters, untested configuration, and
  no way to verify the integration before an incident.
- **A wrapper interface around Sentry with a no-op implementation in tests.**
  Tests would then assert that the wrapper was called, which proves nothing
  about what would be transmitted — the interesting part is exactly the SDK
  behaviour the wrapper hides.
- **A hosted mock Sentry endpoint in tests.** Adds a network dependency and a
  service to run, for less fidelity than intercepting the transport.
- **Filtering at the Sentry project level (server-side scrubbing).** The data
  has already left the process by then. Filtering happens before sending.
