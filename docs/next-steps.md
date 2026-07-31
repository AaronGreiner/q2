# Next steps

Where to go from the initial version, in the order that makes sense. Each item
says what to do, why it comes at that point, and what "done" looks like.

This is a working plan, not a commitment. Re-read it before starting anything —
the earlier items change what the later ones should look like.

---

## Now: close the gaps this repository already knows about

These are small, and every one of them removes a claim that is currently
unverified or a rough edge that was found by hand.

### 1. Make CI actually run

**Status:** the workflows in `.github/workflows/` have never executed. There is
no remote and no CI run behind them.

Push the branch, open a pull request, and watch all four jobs. Expect to fix
something — a runner-only path issue, a missing `--with-deps` dependency for
Chromium, a cache key. Until it has run green once, "CI is set up" is a
statement about files, not about a working gate.

**Done when:** a pull request shows four green jobs, and a deliberately broken
commit shows the right job failing.

### 2. Verify Sentry against a real project

**Status:** no event has ever reached sentry.io. Everything so far used the
local recording transport or an intercepted browser request.

1. Create `q2-app` and `q2-api` in Sentry.
2. Put the **development** DSN in `api/.env` and `app/.env`.
3. Run `bun run dev`, open `/diagnostics`, trigger both failures, and confirm
   two issues appear — in the right projects, in `local-development`, with no
   goal titles, no cookies and no machine name in them.
4. Add `SENTRY_TEST_DSN` as a repository secret and let the release workflow's
   canary run once.

**Done when:** you have looked at a real Sentry issue from this application and
confirmed by eye that the scrubbing holds.

### 3. Automate the accessibility checks that found a real defect

Manual testing found `--ui-text-dimmed` at 2.63:1 — a WCAG AA failure on the
reference id users are asked to quote. The component tests did not catch it
because they assert structure, not colour.

Add `@axe-core/playwright` and run it over the dashboard, the detail page and
the diagnostics page, in both colour schemes. Assert zero violations of
`wcag2a` and `wcag2aa`.

**Done when:** a contrast or ARIA regression fails CI instead of surviving until
someone squints at a screenshot.

### 4. Give a person a time zone

**Status:** instants are stored and reasoned about in UTC, and the browser
shifts a displayed clock into its own zone after hydration
(`useTimeZoneOffset`). Nothing is remembered per person, so a phone that
travels shows different times for the same message, and the server has no way
to render the right one on the first paint.

That is fine while there are no accounts — there is nowhere to keep a zone. It
stops being fine the moment reminders are actually delivered: "07:00" has to
mean seven in the morning where the person is.

Do it together with authentication, not before: a zone belongs to an account.

**Done when:** a reminder time and a message timestamp mean the same thing on
two devices in different zones, and the first server-rendered paint is already
correct.

### 5. Add a smoke check on the server-rendered HTML

Icons silently did not render server-side for a while: the browser filled them
in after hydration, so every screenshot and every E2E assertion looked correct.

Add one E2E test that fetches `/` as plain HTML — no JavaScript — and asserts
the goal titles, the progress values and the icons are present. That is also the
test that tells you whether SSR still works at all.

**Done when:** breaking SSR fails a test rather than degrading quietly.

### 6. Assert the phone layout, not only the behaviour

E2E now runs in one project, `mobile-chromium` at 390 × 844, because q2 ships as
a Capacitor app ([../app/AGENTS.md](../app/AGENTS.md) section 8). Worth knowing
what that did and did not prove: all 15 tests passed unchanged the moment the
viewport shrank from desktop to phone. That is not evidence of a good phone
layout — it is evidence that the suite asserts behaviour and text and never
looks at geometry.

The cheap assertions that would actually bite:

- no horizontal overflow — `document.documentElement.scrollWidth` never exceeds
  the viewport width, on every page. Measured by hand at 390 px when the project
  was switched: the dashboard, the detail page and `/diagnostics` are all clean
  today, so this one starts green and stays a regression guard.
- **interactive elements at least 44 × 44 CSS px** — this one is already
  failing. At 390 px the header links are 24-28 px high, the status filter
  buttons 28 px, the diagnostics buttons 32 px. On a touch screen that is a
  mis-tap waiting to happen, so the fix is UI work, not just a test.
- the primary action of each page reachable without a horizontal scroll.

Best done together with item 3 — axe and layout are the same kind of check, and
one Playwright helper can carry both.

**Done when:** a layout that only holds together in a wide window fails the
suite instead of surviving until someone opens the device toolbar.

---

## Next: the features that make it an application

### 7. Authentication and identity

The largest single gap, and the one everything else depends on. Read
[adr/0006-authentication-deferred.md](adr/0006-authentication-deferred.md)
first — it lists the order to do this in and what not to build.

The short version: decide what identity *is* for this product before writing a
table, use an established provider or library, add ownership to `Goal` and
authorisation to the endpoints in the same change, and store the minimum.

**Until this exists the API must not be publicly exposed with real data.** It
has no access control.

### 8. Updating and deleting goals

Progress cannot currently be changed after creation, which makes the product
close to useless: the whole point is tracking progress over time.

- `PATCH /api/goals/{id}` for progress and status, `DELETE` for removal.
- `Goal.UpdateProgress` and `Goal.Archive` already exist and are tested — this
  is mostly endpoints, DTOs and UI.
- Do it **after** authentication, or you ship an API where anyone can change
  anyone's goals.

### 9. A progress history

"Track progress" implies knowing when it changed. A `GoalProgressEntry` (goal,
percent, recorded-at) unlocks the activity feed, the encouragement features and
the "kudos" idea the product is named after.

This is the first change that needs a real migration against existing data —
a good moment to prove that path works.

### 10. Friends, groups and kudos — built, but not yet safe to share

The screens exist: friends with requests and suggestions, direct and group
chats, kudos on a feed, a leaderboard. What does **not** exist is the part that
makes them safe with more than one person in the database.

There is one person the API answers as
([adr/0009-single-known-person.md](adr/0009-single-known-person.md)), so "who
may see this" has one answer today and no enforcement behind it. A conversation
somebody is not in answers 404, and that is the only rule of its kind.

Before a second person can sign in:

- every read has to be scoped to the caller, not to "the current person";
- who may see whose goals needs an answer, and a legal basis — revisit
  [privacy.md](privacy.md) before designing it, not after;
- message content and friendships are personal data with a retention question
  attached, which the initial version does not answer.

This comes **after** authentication, and the two are one piece of work.

---

## Later: platform and operations

### 11. PostgreSQL

Follow the checklist in
[adr/0004-sqlite-first.md](adr/0004-sqlite-first.md#when-postgresql-is-introduced).
The important part: add provider-level integration tests against a real
temporary PostgreSQL instance. SQLite stays valid for fast local tests but must
not stand in for those.

Trigger for doing this: the first time concurrency or a query SQLite cannot
express actually hurts. Not before.

### 12. Deployment — done, for Staging

A tag `v*` builds and deploys both applications to one Linux host. See
[deployment.md](deployment.md) and
[adr/0008-deployment-topology.md](adr/0008-deployment-topology.md). All five
points below are in place: migrations are an explicit deployment step, the
configuration comes from repository secrets, `ASPNETCORE_URLS` comes from the
environment, `/health` gates the deployment, and the source maps are uploaded
under the same release id the services report.

What is still open:

- **No production environment.** The host runs `Staging`. Production is a
  second host and a second set of secrets.
- **No database backup.** SQLite on a test host with no real data. The first
  time the data matters, this is the first gap to close — before, not after.
- **A few seconds of downtime per deployment.** Services stop, files swap,
  services start. Revisit when that stops being acceptable, not before.
- **No synthetic error trigger on the deployed host.** `/api/diagnostics/*` and
  `sentry canary` are both guarded against Protected environments, so Staging
  has neither. Real errors report normally; if a deliberate trigger there turns
  out to be wanted, that is a decision about the guard, not a bug.

### 13. A third language

German and English are hand-written catalogues in `app/app/i18n/messages.ts`,
switched from the settings screen
([adr/0010-german-first-interface.md](adr/0010-german-first-interface.md)). That
is deliberate for two languages and stops being enough for three, or for one
with real plural rules — Polish and Russian have several, and the catalogue's
`one / many` cannot express them.

At that point `@nuxtjs/i18n` earns its place. Two things move with it: the
number and date helpers in `app/utils/display.ts`, which format by hand because
ICU data differs between Node and browsers and caused hydration mismatches, and
the decimal separator, which currently lives in the catalogue.

### 14. Capacitor for Android and iOS

The architecture keeps this possible: a Nuxt frontend talking to an HTTP API
over a configurable base URL, and a UI that is designed and tested at phone
width from the start (item 6). Do it when there is a reason, and expect the real
work to be in native concerns — push notifications, offline behaviour, safe-area
insets, app store requirements — not in the frontend.

---

## Before any production launch

Not optional, and not engineering work that can be substituted for it. The full
list is [privacy.md](privacy.md) section 8; the items that block a launch:

- a legal basis per purpose, and a privacy notice;
- a data processing agreement with Sentry and every other processor;
- a retention policy, with automated deletion;
- working erasure and export paths — **tested**, not documented;
- access control (item 6 above);
- a DPIA, which is likely required: goal content can be health-related;
- a breach process.

---

## How to keep this file honest

Delete an item when it is done, rather than ticking it. Add an item when you
find a gap. If something here turns out to be a bad idea, replace it and say
why — the point is that the next person reads a current plan, not an archive.
