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
├── nuxt.config.ts            modules, runtime config, Sentry, source maps, PWA
├── sentry.client.config.ts   browser SDK
├── sentry.server.config.ts   Nitro SDK
├── sentry.shared.ts          the filters both use — unit-tested directly
├── vitest.config.ts          two projects: unit and component
├── playwright.config.ts      E2E, own ports, own database
├── public/                   the icons — generated, committed, never edited
├── scripts/generate-icons.ts what generates them (§9a)
├── service-worker/sw.ts      not application source: worker scope, own tsconfig
├── app/                      ← Nuxt 4 puts application source here
│   ├── app.vue error.vue
│   ├── api/                  the HTTP layer (see §3)
│   ├── assets/css/main.css   the design tokens — almost the only file with a hex
│   ├── components/           chats/ friends/ goals/ home/ layout/ profile/
│   │                         settings/ social/ ui/
│   ├── composables/          state and side effects
│   ├── middleware/           auth.global.ts — the route guard
│   ├── i18n/messages.ts      every user-facing word, in both languages
│   ├── layouts/              default (with the tab bar) and plain (without)
│   ├── pages/
│   ├── utils/display.ts      pure presentation logic
│   └── utils/themeColors.ts  the two colours main.css cannot serve (§9a)
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
`app/i18n/messages.ts` (see §11).

**Corners come from `--q2-radius-*`, never from `rounded-xl`.** Nuxt UI
rescales Tailwind's radius utilities off `--ui-radius`, so in this app
`rounded-md` is 18px, `rounded-xl` is **36px**, `rounded-2xl` is 48px and
`rounded-3xl` does not exist. Nuxt UI's own components land on 18px — a
UButton is drawn at exactly the radius a `q2-card` is — so a `rounded-xl`
written in the belief that it means Tailwind's 12px comes out at three times
that, right beside them. The four named values in `main.css` are the whole
scale; `rounded-full` for anything that is meant to be a pill.

Current components, by folder:

| Component | Responsibility |
| --- | --- |
| `AppAvatar` | initials on a colour, with an optional presence dot |
| `AppProgressBar` `AppProgressRing` | the two shapes progress is drawn in |
| `AppSearchField` | the box under a screen's title — one height, one shape |
| `AppSegmented` `AppToggle` | a radio group and a switch, both keyboard-operable |
| `AppStateMessage` | the shared shell for empty/error/not-found states |
| `AppErrorState` | renders an `ApiFailure` for a person |
| `AppBottomNav` `AppScreenHeader` | the shell around every screen |
| `AuthScreen` | the frame the sign-in and sign-up screens share |
| `AppConfirmDialog` | the question in front of something that cannot be undone |
| `StreakHero` `TodayProgressCard` | the two cards the start screen opens on |
| `GoalCard` `GoalTile` | one goal in the list, and in the horizontal strip |
| `GoalTaskRow` | one task with its tick box |
| `GoalCreateSheet` | the bottom sheet that creates a goal |
| `ActivityRow` `LeaderboardCard` | the feed and the ranking |
| `FriendRow` `FriendRequestRow` `SentRequestRow` `FriendSuggestionRow` | the four friend states |
| `PersonSearchRow` | a search result, and the one action its `state` implies |
| `ChatListRow` `ChatBubble` `ChatComposer` `ChatGoalBanner` | the chat screens |
| `ChatCreateSheet` | starting a direct chat, or making a group |
| `BadgeGrid` | the badge collection, earned and not |
| `SettingsSection` `SettingsToggleRow` | the settings list |

## 3. The API layer

```
app/api/generated/schema.d.ts   GENERATED — never edit
app/api/types.ts                named re-exports of the contract
app/api/errors.ts               ApiError, ApiFailure, normalisation
app/api/client.ts               the fetch wrapper that normalises every failure
app/api/accounts.ts             register, sign in, sign out, session
app/api/goals.ts                goals and tasks
app/api/social.ts               feed, kudos, leaderboard, friends, profile
app/api/chats.ts                conversations, messages and settings
app/api/diagnostics.ts          the diagnostics endpoints (hand-written, see below)
app/composables/useQ2Api.ts     the configured client
```

- **Nothing outside `app/api/` builds a URL or reads a response body.**
- **The session is a cookie, and it does not travel by itself.** In the browser
  the client sends `credentials: 'include'`, because the API is a different
  origin; during server rendering it copies the `cookie` header off the incoming
  request, which is what keeps the first paint of a page the signed-in one.
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

## 5. Signing in

`middleware/auth.global.ts` guards every route. It is global rather than
per-page on purpose: a new screen is a new file, and a guard somebody has to
remember to add is the one that will be missing from exactly the screen that
needed it. `/login`, `/register` and `/diagnostics` opt out, explicitly and
visibly.

It is convenience, not security — the API refuses every request without a
session on its own. All the middleware does is take somebody to the sign-in
screen instead of showing them five error states.

`useSession()` holds the answer to "who is this?" under one `useState` key, so
the layout, the middleware and the settings screen ask once. It never stores a
token, because there is none: the session is an http-only cookie the browser
sends by itself. Signing out clears the Nuxt data cache as well — without that
the next person to sign in on the same device would see the previous one's goals
until the first request came back.

The sign-in screen offers to fill in a seeded account when
`NUXT_PUBLIC_DEMO_EMAIL` and `NUXT_PUBLIC_DEMO_PASSWORD` are both set. They are
empty by default; the button therefore does not exist in Staging or Production
without a second flag to forget.

## 6. Error handling

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

## 7. Sentry

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

## 8. Accessibility

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

**One deliberate exception, and it is a real one.** Pinch-zoom is disabled so
that an installed q2 behaves like an app, which **fails WCAG 1.4.4 (Resize
Text, AA)**. It is written down rather than forgotten
([../docs/adr/0013-app-like-input.md](../docs/adr/0013-app-like-input.md)), and
it is the reason the layout has to hold at the largest text rather than lean on
a person being able to zoom out of a squeeze. Do not add a second exception
without the same treatment.

## 9. Mobile format

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
([../docs/next-steps.md](../docs/next-steps.md), item 15).

## 9a. Installable, and deliberately not offline

q2 installs from the browser: a web app manifest, a set of sparkles icons and a
service worker. What that buys is the standalone window, the home screen icon
and a start that does not wait for the network. It does **not** buy offline use,
and that is a decision rather than an omission
([../docs/adr/0012-installable-pwa.md](../docs/adr/0012-installable-pwa.md)).

```
nuxt.config.ts               the `pwa` block: manifest, precache patterns
service-worker/sw.ts         the worker — outside app/, own tsconfig, own lib
service-worker/tsconfig.json WebWorker types; `bun run typecheck` runs it too
public/                      the icons, committed
scripts/generate-icons.ts    what drew them — `bun run icons`
app/utils/themeColors.ts     the two hex values main.css cannot provide
```

The rules that matter:

- **The service worker caches the build output and nothing else.** Every screen
  in q2 is somebody's signed-in one, so a cached document is one person's goals
  left on a device the next person may pick up. Navigations are `NetworkOnly`;
  API responses are never touched. `tests/e2e/pwa.spec.ts` asserts this against
  what the browser actually stored — do not relax it without reading the ADR.
- **The PWA is off in `bun run dev`** (`devOptions.enabled: false`). A worker
  caching assets while Vite hot-reloads them is its own debugging session. Try
  it with `bun run build && bun run preview`, which is what E2E does.
- **An icon change is `bun run app:icons`,** and the regenerated files are part
  of the same change. Never hand-edit anything in `public/`.
- **`--ui-bg` has one copy**, in `app/utils/themeColors.ts`, because a manifest
  is JSON and a status bar is painted before any stylesheet exists. Change them
  together; main.css says so too.
- **The offline page lives in the worker**, takes its words from the catalogue
  like everything else, and names no colour — it uses the `Canvas` and
  `CanvasText` system colours, which follow the OS setting on their own.

Three shell behaviours come with being installed, and all three are invisible
in a desktop run ([../docs/adr/0013-app-like-input.md](../docs/adr/0013-app-like-input.md),
`tests/e2e/phoneShell.spec.ts`):

- **pinch-zoom is off** — `touch-action: pan-x pan-y` plus `user-scalable=no`.
  This is the accessibility exception in section 8; read it before touching
  either half.
- **content is not selectable**, and `input`, `textarea`, `select` and
  `[contenteditable]` opt back in. A new control that holds typed text has to
  be in that list or it cannot be corrected.
- **`--q2-safe-bottom` is the room the bottom edge needs**, and it *caps*
  `env(safe-area-inset-bottom)` rather than using all of it — the full inset
  leaves a compact tab bar hovering above the screen edge. Use the token; do
  not reach for `env()` directly.

## 10. Tests

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

**Every spec starts signed in.** `globalSetup` signs in once through the real
form and Playwright's `storageState` carries that session into every test;
`authentication.spec.ts` opts out with its own `test.use`, because it is the one
spec that is about signing in. A broken sign-in therefore fails once, in the
setup, with a clear message — rather than failing forty tests with "expected the
goal list, found the login page".

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

## 11. The message catalogue

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

## 12. Commands

```bash
bun run dev
bun run build
bun run preview
bun run lint
bun run lint:fix
bun run typecheck
bun run icons
```

From the repository root, `bun run dev`, `bun run validate`, `bun run api:types`
and `bun run app:icons` cover the same ground with the right environment.

## 13. Before finishing a frontend change

1. `bun run lint`
2. `bun run typecheck`
3. `bun run test`
4. `bun run build`
5. `bun run test:e2e` if a user-visible flow changed
6. if the API surface changed: `bun run api:openapi`, keeping both artefacts
7. check the loading, empty and error states, not just the happy path
8. look at the change at 390 × 844 — E2E runs there too, but it never asserts
   layout (section 9)
9. confirm no user content reaches Sentry, and none reaches the service worker's
   cache either (section 9a)

Or `bun run validate` from the root, and report what it actually printed.
