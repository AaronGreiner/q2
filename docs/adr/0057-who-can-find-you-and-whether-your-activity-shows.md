# 0057 — Who can find you, and whether your activity shows

**Status:** Accepted
**Date:** 2026-10-20
**Issues:** [#106](https://github.com/AaronGreiner/q2/issues/106)

## Context

Two things about a person were visible to others and could not be turned off.
Anybody signed in found anybody else by any part of their name or handle, and
could send a request; a friend of a friend was offered as a suggestion. And
avatars showed who was online, a friend's row and a direct chat when somebody
was last active. Settings had a "Privatsphäre" row that said these settings did
not exist yet.

## Decision

- **Who can find you** (`Findability`), chosen in settings: **everybody** by
  any part of the name or the handle — the default, as it always was —, **by
  handle**, only the whole handle typed exactly ("@lena.s" or "lena.s", any
  case), or **by link**, nobody through the search at all, only through the
  invite link.
- It is about **strangers**. A friend, or somebody with a request open either
  way, is found as before. Somebody findable only by handle or link is **not
  suggested** as a friend of a friend either: a suggestion is a search nobody
  typed. The invite link works whatever the choice says.
- **Whether your activity shows** (`ShowsActivity`), a switch in settings, on
  by default as it always was. Off, **the server withholds both** "online" and
  "last seen" from every response that would carry them — `Person.IsOnlineAt`
  answers no and `VisibleLastSeenAt` is null — so they reach nobody, not even
  through a push payload or a reader of the API.
- **It is mutual**: somebody who hides theirs does not see anybody else's — no
  dot, no "vor 9 Min aktiv". That half is the **app's** (`useSeesActivity`):
  the others chose to show theirs, and the server has no reader in hand where
  a person's summary is written (`PersonSummary.From` is shared by every
  response). The friend row says the handle instead, the chat header nothing.
- **The defaults stay as they were**, decided in the session: everybody can
  find you and your activity shows until you say otherwise. Privacy by default
  (Art. 25 GDPR) would point the other way; the controller's questions (#74)
  can revisit this, and the migration is the only place that would change.

## Consequences

- Two columns on `People` (`Findability`, `ShowsActivity`), both with the old
  behaviour as their default; `SettingsResponse` and `UpdateSettingsRequest`
  carry them, and an unknown findability is a 400.
- The search reads one more set — my friendships — before its query rather
  than after, to let connected people through.
- `LastSeenAt` is still written only by the seeds (there is no presence
  service), so today the switch mostly governs seeded worlds and what a
  presence service would one day write.
