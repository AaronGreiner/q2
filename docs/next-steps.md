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

**Status: done.** Playwright runs Axe over the dashboard, goal detail and
diagnostics in both colour schemes and requires zero WCAG A/AA violations. The
contrast defects it exposed were corrected in the shared design tokens. The
`meta-viewport` rule is the one explicit exception: pinch zoom remains disabled
for the installed-app behaviour, with its WCAG cost recorded in
[adr/0013-app-like-input.md](adr/0013-app-like-input.md).

### 4. Let a person choose their time zone

**Status:** half done. `Person.TimeZoneId` exists and every deadline is counted
in it — a window starts at local midnight and ends at local midnight, in the
*owner's* zone, through `LocalCalendar`
([adr/0016](adr/0016-windows-instead-of-steps.md)). That was not optional once a
deadline could decide whether somebody keeps a streak.

What is still missing is the other half: **nothing sets it to anything but the
configured default** (`Q2:TimeZone`, `Europe/Berlin`). There is no screen for it
and no detection from the browser, so a person who travels or who lives
somewhere else keeps German days.

Message timestamps and the reminder clock are still shifted client-side by
`useTimeZoneOffset` after hydration, so the first server-rendered paint of a
clock can still be a moment behind.

**Done when:** a person can see and change the zone their days are counted in, a
new account gets a sensible guess from the browser rather than the deployment
default, and the first server-rendered paint of a time is already correct.

### 5. Add a smoke check on the server-rendered HTML

Icons silently did not render server-side for a while: the browser filled them
in after hydration, so every screenshot and every E2E assertion looked correct.

Add one E2E test that fetches `/` as plain HTML — no JavaScript — and asserts
the goal titles, the progress values and the icons are present. That is also the
test that tells you whether SSR still works at all.

**Done when:** breaking SSR fails a test rather than degrading quietly.

### 6. Assert the phone layout, not only the behaviour

**Status: done.** The only Playwright project is `mobile-chromium` at 390 × 844.
The quality spec checks every signed-in screen for horizontal overflow and
requires representative interactive controls to expose at least a 44 × 44
CSS-pixel touch target. The undersized controls found by the first run were
corrected.

---

## Next: the features that make it an application

### 7. A way back into an account

**Status:** accounts exist
([adr/0011-authentication-with-identity.md](adr/0011-authentication-with-identity.md)),
and there is no password reset, no email confirmation and no two-factor. A
forgotten password currently means a new account.

All three need to send mail, which is why they were left out — "nothing that
needs a running service to develop against" is a rule this repository has. That
rule is right for a reference implementation and wrong the moment somebody who
is not a developer signs up, so this is the first thing to do before that
happens.

Decide the mail path first (a transactional provider, an SMTP relay, something
else), then reset, then confirmation. Identity has the token providers for all
of it; `AddDefaultTokenProviders` is deliberately absent today and is where this
starts.

**Done when:** somebody who has forgotten their password can get back in without
a developer touching the database, and the flow is covered end to end.

### 8. Deleting an account

Editing a profile is done: `PUT /api/profile` changes the display name (and the
initials with it), and a profile picture can be uploaded, replaced and deleted
([adr/0017-image-storage.md](adr/0017-image-storage.md)). The handle stays fixed
on purpose — it is what somebody's friends searched for and wrote down.

**Deleting an account is still not possible at all**, and photographs have moved
that from a gap to a deadline: it is Art. 17 over a face
([privacy.md](privacy.md)).

Erasure is the harder half in two ways now. A person's goals and check-ins go
with them, but their messages are part of somebody else's conversation — decide
what a deleted person looks like in a thread before writing the delete. And
their images are files outside the database and outside any transaction, so the
delete has to reach the store as well as the rows, and has to be able to finish
after a half-completed attempt.

**Done when:** a person can delete their account with a documented, tested
answer for everything that pointed at them, files included.

### 9. Editing a goal

**Status: half done.** Stopping and deleting exist — a goal can be set aside,
carried through, given up, and deleted for good from the archive
([adr/0020](adr/0020-pause-and-archive.md)). What is missing is changing one
that is still running: its title, its description, its schedule, who is invited.

- `PATCH /api/goals/{id}` for those four, and nothing else. Progress is not on
  that list and never will be again: a window is closed by a photograph other
  people believe, not by an edit ([adr/0018](adr/0018-proof-and-vote.md)).
- **A changed schedule is the hard part.** Windows are already open against the
  old one, and a streak counted over them has to survive the change or it is a
  way of quietly resetting a record. The honest answer is probably that the
  current window keeps its own deadline and the new schedule takes effect from
  the next one — which is what `GoalMaintenance` would do anyway if it were
  simply left alone.
- `Goal.OwnerPersonId` and `Goal.IsVisibleTo` already say who may read one; who
  may *change* one is the question this item has to answer. "The owner" is the
  obvious answer and is probably wrong for a shared goal.

### 10. A progress history

"Track progress" implies knowing when it changed. A `GoalProgressEntry` (goal,
percent, recorded-at) unlocks the activity feed, the encouragement features and
the "kudos" idea the product is named after.

This is the second change that needs a real migration against existing data.
`AccountsAndTwoSidedFriendships` was the first and is worth reading before
writing it: it converts rather than deletes, and it says in a comment what it
could not convert and why.

### 11. Notification delivery

The switches on the settings screen are stored and honoured by nothing. The
screen says so, which is the honest interim state, but it is the last thing in
q2 that is visibly a promise rather than a feature.

Push notifications are a Capacitor concern (item 15) and reminders are a
scheduling one; a weekly review is neither. Pick the one with a real user asking
for it rather than building the general mechanism first.

---

## Later: platform and operations

### 12. PostgreSQL

Follow the checklist in
[adr/0004-sqlite-first.md](adr/0004-sqlite-first.md#when-postgresql-is-introduced).
The important part: add provider-level integration tests against a real
temporary PostgreSQL instance. SQLite stays valid for fast local tests but must
not stand in for those.

Trigger for doing this: the first time concurrency or a query SQLite cannot
express actually hurts. Not before.

### 13. Deployment — done, for Staging

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

### 14. A third language

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

### 15. Capacitor for Android and iOS

**Status:** the browser half is done. q2 installs as a PWA — manifest, icons,
a service worker that precaches the build and serves an offline page, and
nothing else ([adr/0012-installable-pwa.md](adr/0012-installable-pwa.md)). That
is a standalone window and a home screen icon, not a native app: no push
notifications, no store listing, no offline use.

The architecture keeps the rest possible: a Nuxt frontend talking to an HTTP API
over a configurable base URL, and a UI that is designed and tested at phone
width from the start (item 6). One thing to look at first: the session is a
cookie, and a WebView on a `capacitor://` origin is a harder place to keep a
cross-site cookie than a browser tab. Identity can issue bearer tokens from the
same setup, so that is an addition at that point rather than a rewrite — see
[adr/0011-authentication-with-identity.md](adr/0011-authentication-with-identity.md). Do it when there is a reason, and expect the real
work to be in native concerns — push notifications, offline behaviour, safe-area
insets, app store requirements — not in the frontend.

The icons carry over: `app/public/` is generated by `bun run app:icons` from one
drawing, and the native icon sets come from the same source rather than from a
second one that will drift. The service worker does not carry over — a Capacitor
WebView already serves its assets from the bundle.

---

## Before any production launch

Not optional, and not engineering work that can be substituted for it. The full
list is [privacy.md](privacy.md) section 8; the items that block a launch:

- a legal basis per purpose, and a privacy notice;
- a data processing agreement with Sentry and every other processor;
- a retention policy, with automated deletion;
- working erasure and export paths — **tested**, not documented;
- account recovery and deletion (items 7 and 8 above);
- a DPIA, which is likely required: goal content can be health-related;
- a breach process.

---

## How to keep this file honest

Delete an item when it is done, rather than ticking it. Add an item when you
find a gap. If something here turns out to be a bad idea, replace it and say
why — the point is that the next person reads a current plan, not an archive.
