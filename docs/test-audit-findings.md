# Findings from the automated-test audit

Captured on 31 July 2026 while extending the automated test suite and resolved
on the same day. These were existing product or infrastructure issues, not test
failures introduced by the coverage gate.

## High priority

### QA-001 — Two SQLite migrations are not atomic

EF Core warns that `PRAGMA foreign_keys = 0` in both `KudosExperience` and
`AccountsAndTwoSidedFriendships` cannot run inside the migration transaction.
If the process stops at that point, the database can be left partially
migrated and require manual recovery.

`AccountsAndTwoSidedFriendships` additionally runs six `SqlOperation`s while a
rebuild of `Friendships` is pending. EF warns that the table may not be in the
expected state and recommends moving those operations to a later migration.

**Resolved:** both published migration ids are retained, so databases that
already applied them are unaffected. Fresh databases now use explicit
create/copy/drop/rename operations that stay inside EF's transaction with
foreign keys enabled. Regression tests assert that the generated SQL never
disables foreign keys, that a forced conversion failure rolls back schema and
data, and that the accounts migration can be reverted and applied again.

## Medium priority

### QA-002 — Several API reads can create cartesian result sets

EF Core emits `MultipleCollectionIncludeWarning` for representative goal,
profile and chat requests. No `QuerySplittingBehavior` is configured, so EF
uses one query for multiple collection navigations. The result is correct in
the tests but can become unnecessarily large and slow as a person's data
grows.

**Resolved:** production and test contexts configure
`QuerySplittingBehavior.SplitQuery`; a persistence test pins that convention.

### QA-003 — Light-mode text and controls fail WCAG AA contrast

The initial automated Axe run recorded these failures:

- Dashboard: amber streak copy; the “Mehr” and “Alle anzeigen” links; completed
  task text; both goal-tile percentages; and the active bottom-navigation label.
- Goal detail: the primary contribution button (about 3.85:1) and amber streak
  label (about 3.18:1).
- Diagnostics: the error action (about 3.15:1) and warning action (about 1.72:1).

The completed-task text also failed in dark mode.

**Resolved:** the dimmed, primary, amber, error and warning colour tokens now
meet AA contrast in both themes. The temporary selector baseline was removed;
all audited pages now assert zero Axe WCAG A/AA violations apart from the
separately documented `meta-viewport` exception in ADR 0013.

### QA-004 — Five visible controls miss the app's 44 px touch-target rule

- Dashboard “Mehr”: 32 × 44 px.
- Both goal segmented controls: 171 × 40 px each.
- Diagnostics server-error action: 174 × 32 px.
- Diagnostics client-error action: 169 × 32 px.

The task checkboxes and Kudos button have smaller visible artwork but already
use a pseudo-element to provide a real target of at least 44 × 44 px; they are
not findings.

**Resolved:** links, segmented controls and diagnostic actions now expose at
least a 44 × 44 CSS-pixel target. The phone-layout test requires an empty list
of undersized representative controls.

## Low priority

### QA-005 — Production builds emit avoidable tool warnings

- Nuxt/PWA reports that `inlineDynamicImports` is deprecated and recommends
  `codeSplitting: false`.
- The E2E server reports that `NO_COLOR` is ignored because `FORCE_COLOR` is
  also set, which makes otherwise successful build output noisy.

**Resolved:** the service worker is emitted as one IIFE bundle, avoiding the
deprecated Rollup path, and Playwright removes the contradictory inherited
`NO_COLOR` setting before it starts coloured child processes.

## Functional result

No functional regression was found in the existing automated flows. Every
finding above now has either a focused regression test or a stricter existing
quality assertion. The full validation gate passes with 453 backend tests, 210
frontend tests and 81 mobile E2E tests.
