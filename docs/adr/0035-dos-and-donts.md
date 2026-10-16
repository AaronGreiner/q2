# 0035 — Dos & Don'ts: one name for what you commit to

**Status:** Accepted, the segments revised on 2026-10-16 ([#77](https://github.com/AaronGreiner/q2/issues/77))
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
  **"Dos | Don’ts"** (`?tab=donts`; Dos write nothing). The icon stays.
- **Each segment has two sections, "Deine" and "Von Freunden"**, headed like
  the chat list ([0027](0027-goal-conversations.md)); an empty one is not
  shown. Your own section starts with what is due today, carrying its camera
  (`GoalWindowRow`) or its log button (`DontWindowRow`), sorted by deadline,
  and then the rest as `GoalCard` rows with the window's state in words. A
  friend's row names its owner (`GoalResponse.Owner`) and has nothing to press:
  only the owner hands in a photograph. Whether a row is due comes from the
  server's list of what covers today, not from an open window
  (`goalListSections`).

  This replaced **"Heute | Alle"**, which were two different-looking screens
  over one undivided list in which nothing said whose a goal was, and where a
  friend's Do offered a camera the server always refused (#77).
- **One entry is "ein Do" or "ein Don’t"** ("a Do" / "a Don’t"). Where both
  are meant (creating one, the archive, a profile, a notification, an empty
  state), the copy either says "Dos & Don’ts" or is worded so that it covers
  both ("Was du beendest …", "Du hast es angelegt.").
- **The rename stops at the interface.** It covers every user-facing string,
  the catalogue keys that named the old label (`nav.todos` → `nav.dos`; the
  segments are `goals.tabDos` and `goals.tabDonts` since #77), the tab bar's
  test id and the tab's query value. **What stays `Goal`, on purpose:** the domain term
  and everything built on it — `/api/goals`, the `/goals` route, the tables,
  `ConversationKind.Goal`, the OpenAPI contract, and the `goals` section of the
  catalogue, which is named after the domain and not after a label. A Do and a
  Don't are both a `Goal`; #62 tells them apart with a `Kind`.

  This follows `/search`, which stayed `/search` when its tab became "Freunde"
  ([0032](0032-the-next-step-first.md)). Existing links, push payloads and
  installed iOS builds keep working. `?tab=goals` and `?tab=all`, the
  addresses of the lists from before, are still read and land on the Dos, so
  a bookmark from before still works, but nothing writes them any more.

## Consequences

- Code says `goal` where the screen says "Do". That is the price of not
  renaming a contract that installed apps depend on, and the comments at the
  seams (`AppBottomNav`, `pages/goals/index.vue`) say so.
- A new string about a commitment is written for both kinds unless it is
  about one kind only. "Ziel" belongs to nothing on screen any more.
