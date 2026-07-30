import type { Goal, GoalStatus } from '~/api/types'

/**
 * Presentation logic for goals.
 *
 * Pure functions, no Vue, no fetch, no `new Date()` without an argument — so
 * they are trivially unit-testable and produce the same output in a component
 * test, in SSR and in the browser.
 */

export interface StatusPresentation {
  label: string
  /** Nuxt UI colour alias. */
  color: 'primary' | 'success' | 'neutral'
  icon: string
}

export function goalStatusPresentation(status: GoalStatus): StatusPresentation {
  switch (status) {
    case 'Completed':
      return { label: 'Completed', color: 'success', icon: 'i-lucide-circle-check' }
    case 'Archived':
      return { label: 'Archived', color: 'neutral', icon: 'i-lucide-archive' }
    case 'Active':
    default:
      return { label: 'Active', color: 'primary', icon: 'i-lucide-circle-dot' }
  }
}

/** Clamps to 0-100 so a bad value can never render a broken bar. */
export function clampProgress(percent: number): number {
  if (!Number.isFinite(percent)) return 0
  return Math.min(100, Math.max(0, Math.round(percent)))
}

/** Screen-reader friendly progress description. */
export function describeProgress(percent: number): string {
  return `${clampProgress(percent)}% complete`
}

/**
 * Whole days from `today` to `targetDate`. Negative when the date has passed,
 * `null` when there is no target date.
 */
export function daysUntil(targetDate: string | null, today: Date): number | null {
  if (!targetDate) return null

  const target = Date.parse(`${targetDate}T00:00:00Z`)
  if (Number.isNaN(target)) return null

  const startOfToday = Date.UTC(today.getUTCFullYear(), today.getUTCMonth(), today.getUTCDate())
  return Math.round((target - startOfToday) / 86_400_000)
}

/**
 * A short, human phrasing of the target date.
 * `isOverdue` comes from the server so every client agrees on it.
 */
export function describeTargetDate(goal: Pick<Goal, 'targetDate' | 'isOverdue'>, today: Date): string | null {
  if (!goal.targetDate) return null

  const days = daysUntil(goal.targetDate, today)
  if (days === null) return null

  if (goal.isOverdue) {
    const overdueBy = Math.abs(days)
    return overdueBy === 1 ? 'Overdue by 1 day' : `Overdue by ${overdueBy} days`
  }

  if (days === 0) return 'Due today'
  if (days === 1) return 'Due tomorrow'
  if (days < 0) return `Target date was ${formatDate(goal.targetDate)}`

  return `Due in ${days} days`
}

const monthNames = [
  'Jan', 'Feb', 'Mar', 'Apr', 'May', 'Jun',
  'Jul', 'Aug', 'Sep', 'Oct', 'Nov', 'Dec',
] as const

/**
 * ISO date -> "12 Sep 2026".
 *
 * Formatted by hand rather than with `Intl.DateTimeFormat`: ICU data differs
 * between Node versions and browsers (`Sep` vs `Sept`), which would make the
 * server-rendered HTML differ from the client's and trigger a hydration
 * mismatch. When real localisation arrives it belongs behind an i18n layer,
 * not behind an implicit platform default.
 */
export function formatDate(isoDate: string): string {
  const parsed = new Date(`${isoDate}T00:00:00Z`)
  if (Number.isNaN(parsed.getTime())) return isoDate

  return `${parsed.getUTCDate()} ${monthNames[parsed.getUTCMonth()]} ${parsed.getUTCFullYear()}`
}

/**
 * "Robin Sample", "Robin Sample and Kim Example",
 * "Robin Sample, Kim Example and 2 others".
 */
export function describeParticipants(participants: readonly string[]): string | null {
  if (participants.length === 0) return null
  if (participants.length === 1) return participants[0]!
  if (participants.length === 2) return `${participants[0]} and ${participants[1]}`

  const others = participants.length - 2
  return `${participants[0]}, ${participants[1]} and ${others} ${others === 1 ? 'other' : 'others'}`
}
