# 0002 — Nuxt with a component architecture on top of Nuxt UI

- **Status:** Accepted
- **Date:** 2026-07-29

## Context

The frontend must be component-oriented, understandable, testable and
accessible. Nuxt UI is a required part of the stack.

A component library invites a particular failure: pages built directly out of
`UCard`, `UButton` and `UBadge`, with the fetching, the formatting and the
business rules inline. It looks tidy at first and stops being changeable as
soon as a second page needs the same thing rendered slightly differently.

## Decision

Nuxt UI is treated as a **foundation, not an architecture**. Four layers, with
one rule each:

| Layer | Rule |
| --- | --- |
| `pages/` | compose a view and own the wiring; hold no rules and no reusable markup |
| `components/` | one responsibility, typed props in, typed events out, no fetching |
| `composables/` | state and side effects (`useGoals`, `useGoalsApi`, `useErrorReporter`) |
| `api/` | the only place that speaks HTTP |
| `utils/` | pure presentation logic, no Vue |

Supporting choices:

- **Component names come from the file name**, not the folder
  (`pathPrefix: false`), so `components/goals/GoalCard.vue` is `<GoalCard>`.
  Folders can group by feature without that grouping leaking into every
  template. The cost is that file names must be unique.
- **Every state is renderable from props alone**, which is what keeps the
  component tests short and honest.
- **Presentation logic lives in pure functions** (`app/utils/goalDisplay.ts`)
  that take `today` as an argument. No Vue, no clock, unit-testable in
  milliseconds, identical in SSR and in the browser.
- **`ApiFailure`, not `ApiError`, crosses component boundaries.** Only what
  `useAsyncData` returns is serialised into the SSR payload; a class instance
  arrives in the browser without its prototype, and a `ref` set during server
  rendering is empty after hydration. Failures therefore travel *inside* the
  async data as plain objects.
- **No date formatting through `Intl`.** ICU data differs between Node versions
  and browsers (`Sep` vs `Sept`), which produces hydration mismatches.

## Consequences

- `GoalCard` can be rendered in a test with a plain object and no network.
- Swapping a Nuxt UI primitive changes one component, not a feature.
- The rules about goals exist once, on the server; the frontend renders answers
  rather than recomputing them.
- Slightly more files than the inline approach. Worth it at the first change
  that would otherwise have to be made in three places.
- File-name uniqueness across the component tree is a real constraint; the
  `vue/multi-word-component-names` rule keeps names descriptive enough that it
  has not bitten yet.

## Alternatives considered

- **Pages built directly from `U*` components.** Fastest to write, and the
  first refactor pays it all back with interest.
- **Pinia for state.** No cross-page state exists yet. `useAsyncData` plus
  composables is sufficient, and a store would be indirection without a
  consumer.
- **A UI kit wrapping every Nuxt UI component.** A layer of pass-through
  components with no behaviour is cost without benefit; wrapping happens where
  there is something to add, as in `AppStateMessage`.
