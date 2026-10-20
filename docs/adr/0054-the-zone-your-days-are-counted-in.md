# 0054 — The zone your days are counted in

**Status:** Accepted
**Date:** 2026-10-20
**Issues:** [#9](https://github.com/AaronGreiner/q2/issues/9)
**Amends:** [0016 — Windows instead of steps](0016-windows-instead-of-steps.md),
whose deadlines are counted in the owner's zone — which until now was the
deployment's for everybody.

## Context

Every deadline is counted in its owner's zone (`LocalCalendar`), but every
account got `Q2:TimeZone` (Europe/Berlin) and nothing could change it. Somebody
living elsewhere kept German days: their windows closed at the wrong midnight,
and that decided whether they kept a streak. Server-rendered times were painted
in UTC and moved after hydration.

## Decision

- **A new account is counted in the zone its browser is in**
  (`Intl.DateTimeFormat().resolvedOptions().timeZone`, sent with the
  registration). One the server does not know is ignored, and the account gets
  the deployment's — registration is no moment to fail.
- **The zone is shown and changed in settings** ("Deine Tage zählen in"), from
  the browser's own list of zones, with one tap to take the device's when they
  differ. One the server does not know is refused (`unknownOption`) rather than
  counted, silently, in UTC (`TimeZoneResolver.IsKnown`).
- **A change counts from the next window.** A window already open keeps its
  deadline, which is stored as an instant; the next one is laid out in the new
  zone's days — the same rule as an edit of the schedule
  ([0044](0044-editing-a-running-goal.md)). The reminder, the evening warning,
  the quiet hours and the streak's "today" follow the zone at once, because
  they read it each time.
- **The first paint is in the stored zone.** The session carries the zone and
  its current offset from UTC; the client's offset starts from it, on the
  server and in the first render alike, and only then becomes the browser's
  own. For somebody whose device is where their days are counted, nothing
  moves after hydration.

## Consequences

- `SessionResponse` gains `timeZone` and `utcOffsetMinutes`, `SettingsResponse`
  and `UpdateSettingsRequest` gain `timeZone`, and `RegisterRequest` an optional
  one. No migration: `People.TimeZoneId` existed.
- Somebody who travels and does not change the setting keeps their home days —
  on purpose: a streak should not depend on an airport.
