# 0035 — Dos & Don'ts: one name for what you commit to

**Status:** Accepted
**Date:** 2026-10-09
**Amends:** [0031 — A To-Dos tab, a shorter start screen](0031-todos-tab-and-a-bell-you-can-empty.md),
which named the middle tab "To-Dos".

## Context

One thing had three names. The middle tab said "To-Dos", the screen under it
was split into "Heute | Ziele", and every sheet, empty state and line in a
conversation said "Ziel". "To-Dos" also sounds like a productivity app, and q2
is about a promise made in front of friends.

q2 is also about to support commitments *not* to do something
([#62](https://github.com/AaronGreiner/q2/issues/62)). "Ziel" does not cover
"no alcohol on weekdays" well, and "To-Do" does not cover it at all.

## Decision

- **"Dos & Don'ts"** is the name for what a person commits to, in German and
  in English alike. It is never written "Do's". On screen the apostrophe is the
  typographic one (`Don’ts`), like every other apostrophe in the catalogue.
- **The tab bar says "Dos".** Five labels at 390 px leave no room for more. The
  screen's heading says "Dos & Don’ts", and its segmented control is
  **"Heute | Alle"**. The icon stays.
- **One entry is "ein Do" or "ein Don’t"** ("a Do" / "a Don’t"). Where both
  are meant (creating one, the archive, a profile, a notification, an empty
  state), the copy either says "Dos & Don’ts" or is worded so that it covers
  both ("Was du beendest …", "Du hast es angelegt.").
- **The rename stops at the interface.** It covers every user-facing string,
  the catalogue keys that named the old label (`nav.todos` → `nav.dos`,
  `goals.tabGoals` → `goals.tabAll`), the tab bar's test id and the tab's
  query value (`?tab=all`). **What stays `Goal`, on purpose:** the domain term
  and everything built on it — `/api/goals`, the `/goals` route, the tables,
  `ConversationKind.Goal`, the OpenAPI contract, and the `goals` section of the
  catalogue, which is named after the domain and not after a label. A Do and a
  Don't are both a `Goal`; #62 tells them apart with a `Kind`.

  This follows `/search`, which stayed `/search` when its tab became "Freunde"
  ([0032](0032-the-next-step-first.md)). Existing links, push payloads and
  installed iOS builds keep working. `?tab=goals` is still read as the "Alle"
  tab, so a bookmark from before still lands, but nothing writes it any more.

## Consequences

- Code says `goal` where the screen says "Do". That is the price of not
  renaming a contract that installed apps depend on, and the comments at the
  seams (`AppBottomNav`, `pages/goals/index.vue`) say so.
- A new string about a commitment is written for both kinds unless it is
  about one kind only. "Ziel" belongs to nothing on screen any more.
