# Findings from the automated-test audit

Captured on 31 July 2026 while extending the automated test suite. These are
existing product or infrastructure issues, not test failures introduced by the
new coverage gate. Step 1 deliberately records them without changing product
behaviour.

## High priority

### QA-001 — Two SQLite migrations are not atomic

EF Core warns that `PRAGMA foreign_keys = 0` in both `KudosExperience` and
`AccountsAndTwoSidedFriendships` cannot run inside the migration transaction.
If the process stops at that point, the database can be left partially
migrated and require manual recovery.

`AccountsAndTwoSidedFriendships` additionally runs six `SqlOperation`s while a
rebuild of `Friendships` is pending. EF warns that the table may not be in the
expected state and recommends moving those operations to a later migration.

## Medium priority

### QA-002 — Several API reads can create cartesian result sets

EF Core emits `MultipleCollectionIncludeWarning` for representative goal,
profile and chat requests. No `QuerySplittingBehavior` is configured, so EF
uses one query for multiple collection navigations. The result is correct in
the tests but can become unnecessarily large and slow as a person's data
grows.

### QA-003 — Light-mode text and controls fail WCAG AA contrast

The automated Axe baseline records these failures:

- Dashboard: amber streak copy; the “Mehr” and “Alle anzeigen” links; completed
  task text; both goal-tile percentages; and the active bottom-navigation label.
- Goal detail: the primary contribution button (about 3.85:1) and amber streak
  label (about 3.18:1).
- Diagnostics: the error action (about 3.15:1) and warning action (about 1.72:1).

The completed-task text also fails in dark mode. The exact selectors are pinned
in `app/tests/e2e/quality.spec.ts`: adding a violation fails the suite, and
removing one requires this debt baseline to be shortened.

### QA-004 — Five visible controls miss the app's 44 px touch-target rule

- Dashboard “Mehr”: 32 × 44 px.
- Both goal segmented controls: 171 × 40 px each.
- Diagnostics server-error action: 174 × 32 px.
- Diagnostics client-error action: 169 × 32 px.

The task checkboxes and Kudos button have smaller visible artwork but already
use a pseudo-element to provide a real target of at least 44 × 44 px; they are
not findings.

## Low priority

### QA-005 — Production builds emit avoidable tool warnings

- Nuxt/PWA reports that `inlineDynamicImports` is deprecated and recommends
  `codeSplitting: false`.
- The E2E server reports that `NO_COLOR` is ignored because `FORCE_COLOR` is
  also set, which makes otherwise successful build output noisy.

## Functional result

No functional regression was found in the existing automated flows. The full
validation gate passes; the open items above were discovered through warnings
and the new visual-quality assertions.
