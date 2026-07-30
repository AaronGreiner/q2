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

### 4. Add a smoke check on the server-rendered HTML

Icons silently did not render server-side for a while: the browser filled them
in after hydration, so every screenshot and every E2E assertion looked correct.

Add one E2E test that fetches `/` as plain HTML — no JavaScript — and asserts
the goal titles, the progress values and the icons are present. That is also the
test that tells you whether SSR still works at all.

**Done when:** breaking SSR fails a test rather than degrading quietly.

### 5. Assert the phone layout, not only the behaviour

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

### 6. Authentication and identity

The largest single gap, and the one everything else depends on. Read
[adr/0006-authentication-deferred.md](adr/0006-authentication-deferred.md)
first — it lists the order to do this in and what not to build.

The short version: decide what identity *is* for this product before writing a
table, use an established provider or library, add ownership to `Goal` and
authorisation to the endpoints in the same change, and store the minimum.

**Until this exists the API must not be publicly exposed with real data.** It
has no access control.

### 7. Updating and deleting goals

Progress cannot currently be changed after creation, which makes the product
close to useless: the whole point is tracking progress over time.

- `PATCH /api/goals/{id}` for progress and status, `DELETE` for removal.
- `Goal.UpdateProgress` and `Goal.Archive` already exist and are tested — this
  is mostly endpoints, DTOs and UI.
- Do it **after** authentication, or you ship an API where anyone can change
  anyone's goals.

### 8. A progress history

"Track progress" implies knowing when it changed. A `GoalProgressEntry` (goal,
percent, recorded-at) unlocks the activity feed, the encouragement features and
the "kudos" idea the product is named after.

This is the first change that needs a real migration against existing data —
a good moment to prove that path works.

### 9. Friends, groups and kudos

Only after identity and history exist. This is where the product actually
differentiates, and it is also where the privacy questions get serious: who can
see whose goals, and on what legal basis. Revisit
[privacy.md](privacy.md) before designing it, not after.

---

## Later: platform and operations

### 10. PostgreSQL

Follow the checklist in
[adr/0004-sqlite-first.md](adr/0004-sqlite-first.md#when-postgresql-is-introduced).
The important part: add provider-level integration tests against a real
temporary PostgreSQL instance. SQLite stays valid for fast local tests but must
not stand in for those.

Trigger for doing this: the first time concurrency or a query SQLite cannot
express actually hurts. Not before.

### 11. Deployment

There is no deployment. Decide where the two applications run, then:

- migrations as an explicit deployment step, never on application start
  (`Staging` and `Production` already refuse to do it themselves);
- `ConnectionStrings__Database`, `Sentry__Dsn` and the `NUXT_PUBLIC_*` values
  from the platform's secret store;
- `Urls`/`ASPNETCORE_URLS` from the platform — deliberately not in appsettings;
- health checks wired to the platform's probes;
- source maps uploaded by the release workflow with the same release id.

### 12. Localisation

The UI is English. If German is the target market, add i18n as its own layer
before the copy spreads further — and note that `formatDate` deliberately does
not use `Intl`, because ICU differences between Node and browsers caused
hydration mismatches. Locale-aware formatting has to be solved together with
that.

### 13. Capacitor for Android and iOS

The architecture keeps this possible: a Nuxt frontend talking to an HTTP API
over a configurable base URL, and a UI that is designed and tested at phone
width from the start (item 5). Do it when there is a reason, and expect the real
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
