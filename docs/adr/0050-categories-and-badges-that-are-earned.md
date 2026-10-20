# 0050 — Five categories instead of icons, and badges that are earned

**Status:** Accepted
**Date:** 2026-10-19
**Issues:** [#18](https://github.com/AaronGreiner/q2/issues/18),
[#22](https://github.com/AaronGreiner/q2/issues/22)
**Amends:** [0032 — The next step first](0032-the-next-step-first.md), which
hid the badges "until something awards them", and
[0015 — The Qdos design language](0015-qdos-design-language.md), whose goal
icons were a free choice of twelve.

## Context

- **A goal's icon told q2 nothing.** The create sheet offered twelve icons; a
  medal could be a run or Dry January. Qdos sorts what people commit to into
  five categories, and the takeover plan asked for them.
- **No badge could be earned.** The profile once showed six badges, and only
  the seeds ever awarded one, so ADR 0032 took the shelf away. Two criteria
  ("finished a reading goal", "completed a long-distance running goal") were
  written for goals with steps, which windows replaced, and needed a kind of
  goal to refer to — which is what the categories give them.

## Decision

### Categories

- **`GoalCategory`: Sport, Lernen, Lesen, Achtsamkeit, Sonstiges**, stored as
  text on the goal, on a waiting Do and on a recorded edit. Chosen in the
  create sheet's first step and in the edit sheet, as five labelled pills
  (`GoalCategoryPicker`); an idea fills it. "Sonstiges" when nothing is said.
- **The icon is derived, never chosen.** `GoalCategories.IconFor` gives each
  category one Lucide icon, and every response keeps its `icon` field with
  that value, so a card, a tile, a photograph and a goal's conversation draw
  it as before. The client mirrors the five names (`categoryIcons`) only for
  the picker and the ideas, before there is a goal to ask; a unit test holds
  that mirror to the icon bundle.
- **Changing the category applies at once** and is a line in the
  conversation ("Kategorie: Sonstiges → Sport"), like the title — it lets
  nobody off anything.
- **Migration `GoalCategories`** maps the old icons: a medal or a trophy was
  sport, an open book was reading, everything else — a droplet, a sunrise, a
  flame — said nothing and becomes "Sonstiges". The icon columns are dropped.

### Badges

Each of the six is earned with windows, photographs or kudos
(`BadgeRules`, the numbers there):

| Badge | Takes |
| --- | --- |
| Streak-Held | a streak of 14 days, at any time |
| Frühaufsteher | a believed photograph delivered before 07:00, on 5 different days |
| Bücherwurm | 10 kept windows on one goal in "Lesen" |
| Kudos-Geber | 100 kudos given |
| Marathon | 42 kept windows on one goal in "Sport" |
| Wochensieger | a whole week, Monday to Sunday, with every window due in it kept |

- **`BadgeAwarder` asks the record and awards what is missing.** Idempotent
  and catching up, like `GoalMaintenance`: it runs after a believed photograph
  is saved, after kudos are given, and for the owners of the goals the
  maintenance pass or a read moved — which is what notices that a week ended.
  A badge whose moment nothing noticed is awarded the next time one of these
  runs. Once earned it stays, even if kudos are taken back.
- **The profile shows all six again** (`BadgeGrid`), earned or dimmed, each
  with what it takes and "Noch nicht verdient" in words. A badge is a state:
  no accent.

## Consequences

- The contract: `category` on goals, waiting Dos and their requests;
  `categoryFrom`/`categoryTo` instead of `iconFrom`/`iconTo` on an edit. An
  unknown category name does not bind and is a 400.
- Earning a badge is not announced — no notification, no line in the feed.
  That would be a decision of its own.
- Badges are shown on your own profile only, as before.
