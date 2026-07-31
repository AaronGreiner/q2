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
│   ├── assets/css/main.css   the design tokens — the only file with a hex value
│   ├── components/           chats/ friends/ goals/ home/ layout/ profile/
│   │                         settings/ social/ ui/
│   ├── composables/          state and side effects
│   ├── i18n/messages.ts      every user-facing word, in both languages
│   ├── layouts/              default (with the tab bar) and plain (without)
│   ├── pages/
│   └── utils/display.ts      pure presentation logic
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

**No component contains a literal user-facing string.** `const t =
useMessages()` and `{{ t.goals.heading }}`; the words live in
`app/i18n/messages.ts` (see §10).

Current components, by folder:

| Component | Responsibility |
| --- | --- |
| `AppAvatar` | initials on a colour, with an optional presence dot |
| `AppProgressBar` `AppProgressRing` | the two shapes progress is drawn in |
| `AppSegmented` `AppToggle` | a radio group and a switch, both keyboard-operable |
| `AppStateMessage` | the shared shell for empty/error/not-found states |
| `AppErrorState` | renders an `ApiFailure` for a person |
| `AppBottomNav` `AppScreenHeader` | the shell around every screen |
| `StreakHero` `TodayProgressCard` | the two cards the start screen opens on |
| `GoalCard` `GoalTile` | one goal in the list, and in the horizontal strip |
| `GoalTaskRow` | one task with its tick box |
| `GoalCreateSheet` | the bottom sheet that creates a goal |
| `ActivityRow` `LeaderboardCard` | the feed and the ranking |
| `FriendRow` `FriendRequestRow` `FriendSuggestionRow` | the three friend states |
| `ChatListRow` `ChatBubble` `ChatComposer` `ChatGoalBanner` | the chat screens |
| `BadgeGrid` | the badge collection, earned and not |
| `SettingsSection` `SettingsToggleRow` | the settings list |

## 3. The API layer

```
app/api/generated/schema.d.ts   GENERATED — never edit
app/api/types.ts                named re-exports of the contract
app/api/errors.ts               ApiError, ApiFailure, normalisation
app/api/client.ts               the fetch wrapper that normalises every failure
app/api/goals.ts                goals and tasks
app/api/social.ts               feed, kudos, leaderboard, friends, profile
app/api/chats.ts                conversations, messages and settings
app/api/diagnostics.ts          the diagnostics endpoints (hand-written, see below)
app/composables/useQ2Api.ts     the configured client
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

The message itself is **not** part of `ApiError`. The app speaks two languages,
so the words are chosen from `kind` at render time (`t.errors[failure.kind]`)
and normalisation stays language-free.

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

**`useAsyncData` returns a *shallow* ref.** Assigning to a property of
`data.value` changes the object without telling anything watching it: the
request succeeds and the screen does not move. Always replace the whole
payload — `data.value = { ...data.value, feed: … }`.

## 6. Sentry

- `sentry.shared.ts` holds `beforeSend` and `beforeBreadcrumb` for both
  runtimes, so the browser and Nitro filter identically. It is unit-tested
  directly, which means the tests exercise the shipped configuration.
- Sentry is **not** disabled outside production. Without a DSN it simply does
  not send; with one, it reports tagged with the local environment.
- `ui.input` breadcrumbs are dropped entirely — they record what was typed into
  a goal title.
- Session Replay records **every** session, and `sendDefaultPii` is on, so the
  browser SDK attaches the IP address. Both are deliberate; both are frontend
  only. Two consequences when touching this code: the sample rates do nothing
  without `Sentry.replayIntegration()` in `sentry.client.config.ts`, and the
  masking options (`maskAllText`, `maskAllInputs`, `blockAllMedia`) are what
  keeps goal content out of a recording — do not remove them to "see more".
- Do not reinstate `delete event.user` in `scrubEvent`. It would silently undo
  `sendDefaultPii` while the configuration still claims to be on.
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
([../docs/next-steps.md](../docs/next-steps.md), item 14).

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
([../docs/next-steps.md](../docs/next-steps.md), item 6). Looking at the change
yourself remains part of the work.

## 10. The message catalogue

`app/app/i18n/messages.ts` holds every user-facing word, in German and English.
The product speaks German by default; the settings screen switches it, and the
choice is remembered on the server
([../docs/adr/0010-german-first-interface.md](../docs/adr/0010-german-first-interface.md)).

Three rules keep it working:

- **No literal user-facing text outside this file.** Not in a template, not in
  a composable, not in an `aria-label`. A German string in a component cannot be
  translated and, worse, nobody will find it.
- **`en` is typed as `Messages`**, the type inferred from `de`, so a missing key
  is a build error. A unit test covers what the type cannot: empty strings,
  functions whose signatures drifted apart, and a "translation" that was pasted
  rather than translated.
- **The API sends structure, not sentences.** The feed sends `kind`, `subject`
  and `amount`; `activitySentence` in `app/utils/display.ts` composes the line.
  Anything the server phrases is a line that can only ever be one language.

Presentation helpers take the catalogue as an argument rather than reaching for
a composable, which is what keeps them pure and unit-testable.

## 11. Commands

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

## 12. Before finishing a frontend change

1. `bun run lint`
2. `bun run typecheck`
3. `bun run test`
4. `bun run build`
5. `bun run test:e2e` if a user-visible flow changed
6. if the API surface changed: `bun run api:openapi`, keeping both artefacts
7. check the loading, empty and error states, not just the happy path
8. look at the change at 390 × 844 — E2E runs there too, but it never asserts
   layout (section 8)
9. confirm no user content reaches Sentry

Or `bun run validate` from the root, and report what it actually printed.
