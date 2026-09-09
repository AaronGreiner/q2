# 0023 — Notifications: a delivery route, not a second product

**Status:** Accepted
**Date:** 2026-09-09

## Context

Two things in q2 stop being useful the moment they are read late. The evening
warning ([0019](0019-warning-and-balance.md)) exists so that somebody can still
act before a window closes; the daily challenge ([0021](0021-daily-challenge.md))
expires at midnight. Both were written to be found by somebody who happened to
open the app.

`QDOS-UEBERNAHME.md` puts notifications last for a reason it states plainly:
**"Push ist ein Zustellweg, kein Neubau."** By this stage the rule, the
recipients and the wording all exist. What is missing is only that the thing
arrives without the app being open. The same document notes that quiet hours
from the settings screen are applied here for the first time — and they were not
there either, because there was nothing to be quiet about.

The notification switches on the settings screen have been stored and honoured
by nothing since the first version, on the stated grounds that a setting has to
survive the day it starts working. This is that day.

## Decision

### Web Push, written out rather than taken from a package

`WebPushCrypto` implements RFC 8291 (encrypting a payload to a browser) and
RFC 8292 (VAPID, identifying this server to a push service) on primitives .NET
already ships: `ECDiffieHellman`, `HKDF`, `AesGcm` and `ECDsa`.

That is a real decision in a repository whose dependency list is four lines
long, and it rests on one fact: **RFC 8291 publishes a worked example with every
intermediate value in it.** The implementation is measured against the
specification's own answer rather than against a reading of it. `Encrypt` takes
its salt and one-off key as parameters precisely so that example can be
reproduced; every caller outside the tests passes fresh randomness.

The check is a *decryption*, not a transcribed constant. The RFC prints the
finished body as 186 characters of base64url, and copying that into a source
file is one slip away from a test failing for a reason having nothing to do with
the code — which is what happened on the first attempt. So the browser's half is
written out in the test from the specification's steps, its four derived values
are asserted against the short intermediates the RFC also publishes, and the
implementation's output is decrypted with it. The two halves share no code.

### The payload carries facts; the service worker writes the sentence

The server sends a kind, a subject and an amount — the same shape the activity
feed already stores. The wording is composed in the service worker from the
message catalogue it was already importing for the offline page.

Three things follow, and all three are why it is done this way rather than
sending a title and a body:

- A notification arrives in **the language the person chose**, decided on the
  device that knows it.
- Adding a language is still one file.
- The wording cannot drift from the feed's, because it is the same catalogue.

The payload is encrypted end to end, so a goal title in it is readable by the
browser and by nobody in between — not by Google's push service, not by
Mozilla's. That is what makes it acceptable to name a goal at all.

### The recipients are whoever would have seen it anyway

The warning goes to **the owner's friends**, which is exactly who the activity
feed shows the same warning to, and never to the owner: the one person who does
not need telling that they are running out of time is the person running out of
time.

The challenge goes to **everybody with a device**, and it is the one
notification in q2 not scoped to somebody's friends — the prompt is deliberately
the same for all of them. It is also the one that would not scale: a deployment
with a hundred thousand people would want this fanned out rather than sent from
one hourly pass, which is a different design rather than a bigger loop.

A recipient set that differed from the feed's would mean telling somebody
something they cannot then go and look at, or missing somebody the app has
already told. Push is a delivery route.

### Three gates, and quiet hours drop rather than hold

Between a notification and a device stand the person's switch for that kind, the
quiet hours in **their own zone**, and a live subscription.

`QuietHours` is a pure rule, like `GoalRisk` and `PauseRules`, and defaults to
22:00–07:00 — **on**. That is not a neutral default: a product that has to be
told not to buzz at three in the morning has already buzzed at three in the
morning for everybody who never opened the settings. It meets the evening
warning rather than cancelling it, because that never goes out before 20:00.

**A notification caught by quiet hours is dropped, not queued.** Holding it
would be the "Neubau" this stage is not, but the real reason is that the thing
itself is already recorded: a notification arriving at seven in the morning
about a window that closed at midnight is a reminder of something that can no
longer be acted on.

A person with no settings row is treated as having the **defaults**, not as
wanting nothing. A row is written at sign-up and created on first read, so an
absent one is an implementation detail rather than an answer.

### A subscription is a device, and no VAPID keys is a supported state

One row per endpoint rather than per person: the same account on a phone and a
laptop is two subscriptions and both should ring. An endpoint that already
belongs to somebody else changes hands rather than colliding — that is a shared
device where the previous person signed out.

The push service's own verdict that a device is gone (404 or 410) deletes it at
once. Everything else counts towards ten consecutive failures, because a device
that has been off for a week produces timeouts rather than a verdict and
dropping it on the first one would unsubscribe somebody for going on holiday.

**Empty VAPID keys turn the feature off, and that is the default.** Nothing
breaks: the browser is told there is no key to subscribe with, the settings
screen says so instead of offering a dead switch, and the workers skip delivery.
The private key is a credential and belongs in the environment, like the Sentry
DSN and the connection string.

### The switch on the settings screen is per device, and asks at the tap

Two kinds of notification control now sit on that screen and they are not the
same thing. The four switches are the account's preference and travel between
devices; the row below them is one browser's permission and subscription, which
cannot.

The permission prompt is raised only from a tap on a control somebody has just
read the label of. A prompt on page load is the fastest route to a permanently
blocked browser, and blocked is the one state the app cannot undo.

## Consequences

- **The notification switches finally do something**, four stages after they
  were added. `NotifyChallenge` is new beside them: somebody who turned off "you
  are about to miss something" said something specific, and taking the one
  cheerful notification in q2 away with it would read more into that than they
  said.
- **`UserSettings` gained quiet hours**, and `Update` takes both ends or
  neither — half a window is not a window.
- **`Challenge` gained `AnnouncedAt`.** A challenge exists days before it runs,
  so "published" and "announced" are different moments; the flag is what stops
  an hourly pass meaning an hourly notification.
- **Sending never happens inside a transaction.** Both workers collect what to
  notify, save, and then send; a database transaction held open across a network
  call is one held open for as long as somebody else's server feels like taking.
  Delivery failures are swallowed, because the pass's real work is already
  committed.
- **A push endpoint is the most identifying thing q2 stores** — a stable handle
  for one browser installation. It is never logged, never sent to Sentry, and
  goes with the account when it is deleted
  ([0022](0022-blocking-reporting-and-erasure.md)).
- **Nothing can ask the server to send a notification.** The three endpoints
  subscribe, unsubscribe and hand out the public key; notifications are produced
  by the two background jobs that already existed. A client that could ask for
  one would be a client that could send somebody else one.
- **The service worker now has a reason to exist beyond installability.** It was
  deliberately doing almost nothing ([0012](0012-installable-pwa.md)); it still
  caches no content, and the push handler reads nothing from a cache.
