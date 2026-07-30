# Working on the q2 frontend

Applies to everything under `app/`. The repository-wide rules in
[../AGENTS.md](../AGENTS.md) apply as well; this file adds what is specific to
the Nuxt application.

---

## 1. Architecture

Nuxt 4 with Vue 3, TypeScript in strict mode and Nuxt UI. Server-side rendered
by default.

```
app/
├── nuxt.config.ts            modules, runtime config, Sentry, source maps
├── sentry.client.config.ts   browser SDK
├── sentry.server.config.ts   Nitro SDK
├── sentry.shared.ts          the filters both use — unit-tested directly
├── vitest.config.ts          two projects: unit and component
├── playwright.config.ts      E2E, own ports, own database
├── app/                      ← Nuxt 4 puts application source here
│   ├── app.vue error.vue
│   ├── api/                  the HTTP layer (see §3)
│   ├── components/goals/     feature components
│   ├── components/ui/        generic building blocks
│   ├── composables/          state and side effects
│   ├── layouts/ pages/
│   └── utils/goalDisplay.ts  pure presentation logic
└── tests/{unit,component,e2e}
```

`app/app/` is not a mistake: Nuxt 4's source directory is `app/` inside the
project.

**Component names come from the file name, not the folder** — `pathPrefix:
false` is set, so `components/goals/GoalCard.vue` is `<GoalCard>`. File names
must be unique across the component tree.

## 2. Component rules

Nuxt UI is a foundation, not an architecture. A page made of raw `U*`
components with logic inline is not "using the design system well".

- **Pages compose.** A page wires state to components and handles intent. It
  contains no rendering detail worth reusing and no business rule.
- **Components have one job**, typed props, typed events, and no data fetching.
  If a component needs to fetch, the page should have passed the data in.
- Feature logic does not couple to Nuxt UI. `GoalCard` renders a `Goal`;
  swapping `UCard` for something else must not change what a goal *is*.
- Split when a part has its own responsibility, not to make files shorter. A
  component that exists only to hold three lines of markup is worse than
  inline markup.
- Every state a component can be in is renderable from props alone — which is
  what makes the component tests short.

Current components:

| Component | Responsibility |
| --- | --- |
| `GoalCard` | one goal in a list |
| `GoalList` | the four list states: loading, empty, error, loaded |
| `GoalProgress` | the progress bar and its accessible description |
| `GoalStatusBadge` | status, plus an overdue marker |
| `GoalStatusFilter` | filter by status (a radio group, not a select) |
| `GoalCreateForm` | the create form, including server-side field errors |
| `AppStateMessage` | the shared shell for empty/error/not-found states |
| `AppErrorState` | renders an `ApiFailure` for a person |

## 3. The API layer

```
app/api/generated/schema.d.ts   GENERATED — never edit
app/api/types.ts                named re-exports of the contract
app/api/errors.ts               ApiError, ApiFailure, normalisation
app/api/goals.ts                the typed goal endpoints
app/api/diagnostics.ts          the diagnostics endpoints (hand-written, see below)
app/composables/useGoalsApi.ts  the configured client
```

- **Nothing outside `app/api/` builds a URL or reads a response body.**
- The base URL comes from runtime config (`NUXT_PUBLIC_API_BASE_URL`). No
  environment URL is ever hard-coded.
- `schema.d.ts` is generated from `api/openapi/q2-api.json`. Regenerate with
  `bun run api:openapi` (exports the contract too) or `bun run api:types`
  (types only, from the committed contract). Both artefacts are committed and
  CI checks them.
- The diagnostics endpoints are hand-typed on purpose: they are excluded from
  the OpenAPI document because they only exist outside production.

## 4. TypeScript

- Strict mode. `@typescript-eslint/no-explicit-any` is an error.
- Contract types come from the generated schema — never redeclare a DTO.
- Type-aware lint rules are deliberately off (they need a full TS program per
  lint run); `bun run typecheck` covers types properly with `vue-tsc`.

## 5. Error handling

Every failure is normalised once, in `app/api/errors.ts`, into an `ApiError`
with a `kind`:

| kind | Cause | Expected? |
| --- | --- | --- |
| `validation` | 400 | yes |
| `notFound` | 404 | yes |
| `conflict` | 409 | yes |
| `unauthorized` | 401/403 (for later) | yes |
| `network` | offline, DNS, CORS, timeout | yes |
| `server` | 5xx | **no** |
| `unknown` | anything else | **no** |

*Expected* failures get a friendly message and **never** create a Sentry issue.

Components receive **`ApiFailure`** — plain data, not the class. Only what
`useAsyncData` returns is serialised into the SSR payload, so a class instance
would arrive in the browser without its prototype and a `ref` set during server
rendering would simply be empty after hydration. Failures therefore travel
*inside* the async data.

Users never see a backend `detail`, an exception name or a stack trace. When
the backend recorded the incident, its `errorId` is shown as a reference so a
support request maps onto a Sentry issue.

`app/error.vue` is the last line of defence for unhandled render and server
errors. It does **not** capture to Sentry — the Nuxt integration already did,
and capturing again would duplicate the issue.

## 6. Sentry

- `sentry.shared.ts` holds `beforeSend` and `beforeBreadcrumb` for both
  runtimes, so the browser and Nitro filter identically. It is unit-tested
  directly, which means the tests exercise the shipped configuration.
- Sentry is **not** disabled outside production. Without a DSN it simply does
  not send; with one, it reports tagged with the local environment.
- `ui.input` breadcrumbs are dropped entirely — they record what was typed into
  a goal title.
- Session Replay is off and stays off until it has had its own privacy review.
- `useErrorReporter` decides what deserves an issue: expected failures never do;
  a 5xx that already carries an `errorId` becomes a breadcrumb rather than a
  second issue for one incident.

## 7. Accessibility

Not optional, and checked in the component tests:

- Correct heading order (`h1` per page, `h2` per section, `h3` per card).
- Status is never conveyed by colour alone — always a label, usually an icon.
- Loading regions use `aria-busy` and announce themselves; error states use
  `role="alert"`, informational states `role="status"`.
- Every input has a real label via `UFormField`; errors are associated with
  their field.
- A skip link is the first tab stop.
- `prefers-reduced-motion` is respected globally in `main.css`.
- Interactive elements are reachable and operable by keyboard.

## 8. Mobile format

**q2 is a phone application that currently happens to run in a browser.** The
browser is the development and test surface; what ships to a person is a
Capacitor build for Android and iOS. That is why the frontend is a plain Nuxt
application talking to an HTTP API over a configurable base URL — see
[../docs/architecture.md](../docs/architecture.md).

The consequence for everyday work:

- **Test in the browser, always in a mobile viewport.** 390 × 844 is the
  reference (iPhone-14 class). Turn on the device toolbar — Chrome's device
  mode, Firefox's responsive design mode — *before* looking at the change, not
  after. A screenshot of a maximised desktop window says nothing about the
  product.
- **Mobile first in markup.** Base classes are the phone layout; `sm:`/`md:`
  utilities may widen it. Never write the desktop layout first and squeeze it
  down afterwards.
- **Assume touch, not a pointer.** Hover is never the only route to an action,
  and nothing may depend on a cursor, a right-click, a URL bar or a second
  browser window.
- Wide viewports still have to work — the app runs in a browser today — but
  when the two conflict, the phone wins.

Nothing Capacitor-specific exists in the repository yet, and nothing should be
added ahead of the requirement. This section is about the format the UI is
designed and verified in, not about adding a native layer now
([../docs/next-steps.md](../docs/next-steps.md), item 13).

## 9. Tests

```bash
bun run test             # unit + component
bun run test:unit
bun run test:component
bun run test:sentry
bun run test:e2e
```

- **unit** — pure functions (`goalDisplay`, `errors`, `sentry.shared`). Node
  environment, no Nuxt, milliseconds.
- **component** — real Nuxt environment via `mountSuspended`, so auto-imports,
  Nuxt UI and runtime config behave as in the app.
- **e2e** — Playwright against the production build and a real API on ports
  3001/5081, with its own freshly seeded temporary SQLite database.

Anything time-dependent takes the date as a parameter (`today`), so tests never
depend on when they run.

**Viewport:** the single Playwright project is `mobile-chromium` — the Pixel 7
descriptor (Chromium, touch, mobile emulation) with the viewport pinned to the
390 × 844 reference from section 8. There is no desktop project; a flow that
only works wide is not exercised anywhere.

That covers the *width* the assertions are made at, not the layout itself: the
suite asserts behaviour and text, never geometry, so an overflowing card or an
unreachable tap target would still be green
([../docs/next-steps.md](../docs/next-steps.md), item 5). Looking at the change
yourself remains part of the work.

## 10. Commands

```bash
bun run dev
bun run build
bun run preview
bun run lint
bun run lint:fix
bun run typecheck
```

From the repository root, `bun run dev`, `bun run validate` and
`bun run api:types` cover the same ground with the right environment.

## 11. Before finishing a frontend change

1. `bun run lint`
2. `bun run typecheck`
3. `bun run test`
4. `bun run build`
5. `bun run test:e2e` if a user-visible flow changed
6. if the API surface changed: `bun run api:openapi` and commit both artefacts
7. check the loading, empty and error states, not just the happy path
8. look at the change at 390 × 844 — E2E runs there too, but it never asserts
   layout (section 8)
9. confirm no user content reaches Sentry

Or `bun run validate` from the root, and report what it actually printed.
