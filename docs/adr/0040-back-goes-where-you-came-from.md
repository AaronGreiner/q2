# 0040 — Back goes where you came from, and finds the screen as it was left

**Status:** Accepted
**Date:** 2026-10-16
**Issue:** [#118](https://github.com/AaronGreiner/q2/issues/118)

## Context

- **Every back arrow had a fixed address.** `AppScreenHeader` drew it as a link
  to `backTo`: a Do went "back" to the Dos tab, a profile to Friends, a thread
  to the chat list — regardless of where it had been opened from. A Do opened
  on the start screen, from a chat's banner, from a photograph or from a
  notification all came back out on the Dos tab.
- **The arrow pushed.** Being a link, it added an entry on top of the screen it
  was leaving. After Home → Do → arrow, the system's back went into the Do
  again, and the browser's history filled with the same few screens in a loop.
- **A screen came back blank.** The window never scrolls in q2 — both layouts
  are one viewport tall with an inner scroll area (`layouts/default.vue`) — so
  the router's own scroll restoration, which saves `window.scrollY`, had
  nothing to restore. A list left halfway down came back at the top; the term
  typed into the Friends search and an open chat info sheet were gone.

## Decision

### One history, walked backwards

The arrow steps back through the history (`useGoBack`, used by
`AppBackLink`). There is one history for the whole app rather than one per
tab: Home → Do → its chat, back, back is Home again, whichever tabs were
touched in between. Per-tab stacks, the way a native tab bar keeps them, were
considered and rejected — they would fight the browser's own back button,
which only knows one history, and every main screen is a tab with no arrow of
its own anyway.

`backTo` stays, as the **fallback**: where a screen sits when there is nothing
in q2 before it — it was opened from a push notification, a link or a reload.
The router records the previous address in `history.state.back` on every
in-app navigation and leaves it null on the first entry it renders, so that is
the test. The fallback *replaces* the entry: a screen opened from a
notification, left by its arrow, does not come back when the system's back is
pressed next.

The arrow is still an `<a href>` to the fallback, so it is announced as a link,
the server renders a real address for it, and a modified click opens that
address in a new tab as a browser would.

### How a screen was left travels with the visit

What is handed back on return — the content panel's scroll position, the
Friends and Chats search terms, whether a thread's info sheet was open — is
written into the history entry being left (`useReturnState`), under a `q2:`
key next to the router's own fields:

- **Not in the address.** Unlike the Dos segment (`?tab=donts`), these are not
  something one would share or reload into. A search term is often a person's
  name, and an address travels into share sheets, the browser's own history
  list and Sentry (scrubbed there, but not sent is better than scrubbed).
- **Not in a store.** A value in memory belongs to the screen, not to the
  visit: the Friends tab opened afresh from the tab bar would come up with
  yesterday's search. In the history entry, going back finds it and a new
  visit does not.
- **Written once, on the way forward.** Only as the screen is left by a push
  or replace, never per keystroke (Safari throws after a hundred history writes
  in thirty seconds). When the screen is left by going back, the browser has
  already moved to the earlier entry by the time the router's guard runs, so
  nothing is written — it would land on the wrong screen.
- **Not during hydration.** The server cannot know the entry's state, so a
  server-rendered first paint never restores; a reload starts clean.

The scroll position is restored by `restoreScroll`: a screen gone back to is
rendered again and refetches its data, so it waits — at most a second and a
half — for the content to be tall enough, and gives up as soon as the person
touches, scrolls or types.

### Leaving a screen for good

Leaving a group goes back to where the thread was opened from (the list as the
fallback) rather than pushing the list over a conversation that no longer
exists for this person. Blocking somebody *replaces* their profile with
Friends: their profile is gone, and so may be the conversation that came before
it ([0022](0022-blocking-reporting-and-erasure.md)), so neither may be left in
the history to go back into.

## Consequences

- A screen that is pushed gets its arrow from `AppScreenHeader`'s `backTo` or
  from `AppBackLink` directly, and `backTo` names its **parent**, not its
  predecessor. A new pushed screen that draws its own link to a fixed address
  brings the old behaviour back.
- Anything else on a screen that should survive a round trip — a sheet, a
  filter — goes through `useReturnState`, not the address and not a `useState`.
- Restoring is best effort: a list that came back shorter ends as far down as
  it now goes, and a slow connection longer than the patience above leaves it
  where the content allowed.
- The chat thread keeps its own scroll behaviour — it opens on the newest
  message ([kudos E2E](../../app/tests/e2e/kudos.spec.ts)) — and is not part
  of this.
- The iOS app has no edge swipe yet; when it gets one it walks the same history
  ([#119](https://github.com/AaronGreiner/q2/issues/119)). So will Android's
  hardware back button ([#49](https://github.com/AaronGreiner/q2/issues/49)).
