# Observability

Logging and Sentry: what each is for, what goes where, and what never leaves
the process.

## The principle

**Sentry is not switched off outside production.**

A monitoring integration that only runs in production is a monitoring
integration nobody has ever seen work. Here the SDK initialises the same way in
every environment; what differs is the environment name, the sampling, and
whether events go to a real endpoint or to a local recorder. That is why the
test suite can assert on real Sentry behaviour, and why "does reporting work?"
is answerable on a laptop.

## Logs versus Sentry

| | Logs | Sentry |
| --- | --- | --- |
| Purpose | what the system did | what went wrong |
| Volume | high, routine | low, actionable |
| Validation failure | `Information` | **never** |
| "Not found" | `Information` | **never** |
| Health check | `Debug`/none | **never** |
| Unexpected exception | `Error`, with stack | **yes, once** |

An expected failure is not an error. A user typing an empty title is normal
use; recording it as an incident buries the real defects.

One failure produces **one** Sentry event. The backend reports from exactly one
place — `GlobalExceptionHandler` — and reports *before* logging, because the
Sentry logging integration also turns `LogError` into an event and whichever
call happens first wins; the SDK's duplicate detection drops the second. That
ordering is what lets the response carry the id of the event that actually
exists.

## Structured logging

Backend: single-line console locally and in ManualTesting, JSON everywhere else
so a log shipper can parse it. Message templates carry named values
(`{GoalId}`, `{SeedProfile}`), never string concatenation.

Sentry Logs is enabled on both halves. The API forwards the `ILogger` levels
already selected by its ordinary logging configuration. The browser and Nitro
forward `console.warn` and `console.error`; `debug` and `info` stay local because
those are the levels at which frontend code is most likely to inspect a whole
application object. Hand-written frontend operational logs use
`Sentry.logger`. `beforeSendLog` applies the same content and identity rules as
events, and a backend log containing a sensitive parameter is dropped whole.

Request logging with headers or bodies is deliberately **not** enabled.

What may be logged: ids, counts, durations, status codes, environment and
release, migration and seed names, which code path ran.

What may **not** be logged: goal titles and descriptions, participant names,
credentials, tokens, connection strings, cookies, headers, request bodies,
email addresses, IP addresses, exact locations.

## Metrics

Sentry Metrics is enabled on both SDKs. The API currently emits the useful
operational counters: goals created and progressed, tasks completed or reopened,
and accounts registered or signed in. Every name is declared in `Q2Metrics`;
`beforeSendMetric` drops any other name. Attributes come from closed values
such as rhythm, `shared` and `done` — never ids, names, titles, addresses or
free text.

The frontend does not duplicate those successful business actions: the API is
the one place that knows they succeeded. It emits one browser-only counter,
`q2.api.failure`, for requests that failed at the UI — including offline calls
that never reached the API — with only error kind, expected/unexpected and HTTP
status. Its `beforeSendMetric` also allow-lists the name and strips SDK-added
identity attributes.

## User feedback

Sentry's User Feedback dialog is wired up in the browser SDK and opened from two
of q2's own controls — the settings screen and the error page — never from the
SDK's injected floating button, which is switched off.

It is the **one path that deliberately sends user-authored text**. A feedback
event has `type: 'feedback'`, and `beforeSend` is only called for error events,
so `scrubEvent` never sees one: what the person typed is what is transmitted.
That is the point of the feature, and it is the reason the form collects nothing
else — no name, no address, no screenshot, nothing read out of the Sentry scope.
The event carries the message, the page URL, the session replay id and a
`q2.feedback_source` tag (`settings` or `error-page`).

What that means for anyone changing it: the guard is the shape of the form, not
a filter downstream. `sentry.feedback.ts` holds it and
`tests/unit/sentryFeedback.spec.ts` asserts it; `tests/e2e/feedback.spec.ts`
asserts the same thing against the envelope on the wire. See
[adr/0014-user-feedback.md](adr/0014-user-feedback.md).

## Profiles

Browser UI Profiling is enabled with `browserProfilingIntegration`,
`profileSessionSampleRate` and `profileLifecycle: 'trace'`. A sampled browser
session is profiled only while a sampled root span runs. The required
`Document-Policy: js-profiling` header is served for every frontend route;
browsers without the Self-Profiling API simply produce no profile.

There is deliberately no separate Nitro or .NET profiler in this version.
Those would add native/legacy profiler packages and operational deployment work,
while the browser is the product's actual interaction surface. Traces, logs and
metrics cover both servers. Add a server profiler when a measured server-side
performance problem makes that dependency worthwhile.

## Projects and environments

Two Sentry projects, so a frontend regression and a backend regression are
never the same issue:

```
q2-app  (frontend: browser + Nitro)
q2-api  (backend)
```

Six environments in each, mapped from the application environment:

| ASP.NET Core / `NUXT_PUBLIC_APP_ENV` | Sentry environment |
| --- | --- |
| `Development` | `local-development` |
| `ManualTesting` | `manual-testing` |
| `AutomatedTest` | `automated-test` |
| `E2E` | `e2e` |
| `Staging` | `staging` |
| `Production` | `production` |

The backend mapping lives in `SentryEnvironments.cs` and nowhere else; a test
asserts every mapping is distinct, so a local run can never land in the same
environment as production. An unknown environment maps to `unknown-<name>`
rather than to anything resembling production.

## Releases

Both services report the same identifier, `q2@<version-or-commit>`, so an issue
in `q2-app` lines up with one in `q2-api` and a source map upload attaches to
the right release.

Resolution order (backend, `ReleaseIdentity`): `Sentry:Release` →
`Q2_RELEASE` → `q2@<short git sha>` → `q2@<assembly version>`. The frontend
uses `NUXT_PUBLIC_SENTRY_RELEASE`.

**Never random.** Two starts of the same build must report the same value or
Sentry cannot group them. In CI the value is computed once and passed to both
builds and to the source map upload.

Additional tags: `service.name` (`q2-app` / `q2-api`), plus `test.run_id`,
`seed.profile`, `git.sha` and `ci.run_id` when the harness supplies them.

## Sampling

| Environment | Error events | Traces | Browser profile sessions |
| --- | --- | --- | --- |
| `local-development` | 1.0 | 1.0 | 1.0 |
| `manual-testing` | 1.0 | 1.0 | 1.0 |
| `automated-test` | 1.0 | 1.0 | 1.0 |
| `e2e` | 1.0 | 1.0 | 1.0 |
| `staging` | 1.0 | 1.0 | 1.0 |
| `production` | 1.0 | 0.1 | budget decision before launch |

Error events are never sampled away: volume is low, and a test waiting for a
specific event must not lose it to chance. Trace volume is the part that needs
a budget. A dynamic sampler additionally drops health checks and `favicon.ico`
so they never consume it.

Values are configurable per environment (`Sentry__TracesSampleRate`,
`NUXT_PUBLIC_SENTRY_TRACES_SAMPLE_RATE`,
`NUXT_PUBLIC_SENTRY_PROFILE_SESSION_SAMPLE_RATE`) and are not scattered through
the code. Profiling follows tracing, so its effective rate cannot exceed the
trace rate even when the session rate is 1.

## Behaviour without configuration

- **No DSN** → the SDK initialises, sends nothing, and the application logs
  once at startup that monitoring is off. It never fails to start.
- **Enabled without a DSN** (`SENTRY_ENABLED=true`,
  `NUXT_PUBLIC_SENTRY_ENABLED=true`) → configuration **fails loudly** with a
  readable message. Asking for reporting and silently not getting it is worse
  than not asking.
- **A malformed DSN** → fails at startup, and the error message does not echo
  the value.
- **The recording transport in Staging or Production** → refused outright.
- **Missing Sentry secrets in CI** → builds, unit tests, integration tests and
  fork pull requests all still pass. Only the optional canary and the source
  map upload are skipped.

## Filtering

One central filter per side — `SentryEventScrubber` (backend),
`sentry.shared.ts` (frontend) — wired in as `beforeSend` / `beforeBreadcrumb`.
Both are unit-tested directly, so the tests exercise the shipped configuration
rather than a copy of it.

**Dropped entirely:**

- health checks (`/health`, `/healthz`) and `favicon.ico`
- aborted requests (`OperationCanceledException`)
- expected failures (anything implementing `IExpectedFailure`)
- known browser noise: `Failed to fetch`, `AbortError`, `ResizeObserver loop…`
- `ui.input` breadcrumbs — they record what was typed into a goal title

**Removed from every event and log:**

- cookies, and all request headers except a small allow-list (`Accept`,
  `Content-Type`, `User-Agent`, `traceparent`, `tracestate`, `X-Request-Id`)
- query strings, replaced with `[redacted]`; URLs keep only their path
- request and response bodies
- user id, email and username — but **not the IP address**, on either side.
  `sendDefaultPii` is on, and removing the address in a scrubber would silently
  undo it; see [privacy.md](privacy.md#4-sentry-rules)
- `server_name`, and the device name from the device context
- any key matching a credential, session, connection-string or location pattern
- any key carrying goal, task, chat, message or person content

**Redacted inside free text** (messages, exception values, breadcrumbs):
connection strings, `Bearer` tokens, JWTs, email addresses, coordinate pairs,
and `key=value` pairs whose key looks sensitive. Order matters: the specific
token shapes run *before* the generic `key=value` rule, which stops at the
first whitespace and would otherwise leave the token itself in the payload.

What is deliberately **not** filtered: local and test events. The environment
separation exists to make them visible, not to hide them.

## Verifying it locally

Put a development DSN in `api/.env` and `app/.env`, start the app, and open
<http://localhost:3000/diagnostics>. The page reports whether the backend would
send anything — without revealing the DSN — and offers two buttons that trigger
a synthetic server error and a synthetic client error.

It is not a production endpoint and does not become one by accident: the page
requires `NUXT_PUBLIC_DIAGNOSTICS_ENABLED`, and the backend routes are not
mapped in Staging or Production at all.

## Sentry in automated tests

Tests run the **real SDK**. `RecordingTransport` replaces only the network
call: the event is still built, `beforeSend` still runs, environment, release
and tags are still attached, and the envelope is still serialised. Assertions
run against that serialised payload, so "this value is not in the event" means
it genuinely would not have been transmitted.

With a file path configured, the transport also appends each event as one JSON
line, which is how the Playwright suite inspects events produced by a separate
server process.

Covered: initialisation, exactly one event per failure, environment and release,
no event for validation errors / 404s / health checks, no credentials, no
location data, no goal content, no user identity (backend) or machine name, and
a failing transport not taking the API down.

```bash
bun run test:sentry
```

## Sentry in CI

Every run initialises the real SDK with the recording transport and asserts
environment, release, filtering and event creation. No secret is required, so
fork pull requests are fully validated.

For trusted branches an optional **canary** may additionally send one
synthetic event to a dedicated non-production test DSN, tagged with the CI run
id and commit sha. It is never a prerequisite for a local test run, and it must
not reach a production project or trigger production alerting.

## Symbols and source maps

Both halves upload what Sentry needs to turn a minified or compiled stack trace
back into readable source. Without it an issue shows a frame like
`at n (CFLG7f9j.js:1:7975)` — technically an error report, practically useless.

**Frontend.** The build emits `hidden` client source maps: generated for the
upload, not referenced by the shipped bundles, so browsers are never served
them. The release workflow uploads them under the same release id as the build
and then deletes them from the artefact — everything under `.output/public` is
publicly fetchable, so a map left behind would be readable by anyone.

**Backend.** `Q2.Api.csproj` sets `SentryUploadSymbols` and
`SentryUploadSources`, and the MSBuild targets that ship with the Sentry package
call `sentry-cli` after the build. Debug symbols give line numbers; the sources
give the surrounding code in the issue view. MSBuild reads environment variables
as properties, which is how `SENTRY_ORG`, `SENTRY_PROJECT_API` and
`SENTRY_AUTH_TOKEN` reach it without being written into the project file.

Each half uploads into its own project, hence two project slugs.

`SENTRY_AUTH_TOKEN` is a CI secret only — never prefixed `NUXT_PUBLIC_`, never
committed, never printed. **Both uploads are gated on it being present**, which
is what keeps local builds and fork pull requests working with no secrets: the
tooling is not invoked at all, rather than invoked and failing.

Note the asymmetry, because the two failure modes look nothing alike:

| Token | Backend build |
| --- | --- |
| absent | succeeds, no upload, workflow warns |
| present but wrong | **fails** — `sentry-cli` returns `401 Invalid org token` and MSBuild reports it as an error |

So a release that dies in `Publish` with a Sentry 401 is a bad token, not a bad
build. An expired token stops releases; it does not degrade them quietly.

Uploading sources means Sentry holds a copy of the backend source code. Nothing
secret is in it, but it is a disclosure to a processor — see
[privacy.md](privacy.md) section 8.

## Session Replay

**On for every session** — `replaysSessionSampleRate` and
`replaysOnErrorSampleRate` are both `1`, so a failure can be replayed rather
than reconstructed from a stack trace.

Two things are easy to get wrong here:

- **The sample rates alone do nothing.** `Sentry.replayIntegration()` has to be
  in the `integrations` array in `sentry.client.config.ts`. Without it the
  configuration looks complete and records nothing.
- **Selective privacy is part of the configuration.** `maskAllText` is false so
  headings, labels, navigation, empty states and error messages remain useful.
  `data-q2-private` masks personal text, while `data-q2-block` replaces messages,
  progress, activity and avatars whose geometry or state is itself personal.
  Personal browser-tab titles are masked through `head > title`; the two
  name-bearing Nuxt UI toasts use Replay's built-in `.sentry-mask` because the
  library teleports their DOM outside the calling component.
  `maskAllInputs` remains true. `blockAllMedia` is false because q2 has no user
  photographs or uploads; its icons are application UI, while avatars are
  blocked explicitly.

Replay is browser-only; there is no server-side equivalent.

The retention decision and the privacy notice this needs before real users are
involved are listed in [privacy.md](privacy.md) section 8.
