import type { Messages } from '~/i18n/messages'
import type { Activity, Goal, GoalStatus, GoalTask } from '~/api/types'

/**
 * Presentation logic.
 *
 * Pure functions, no Vue, no fetch, no `Date.now()` without an argument — so
 * they are trivially unit-testable and produce the same output in a component
 * test, in SSR and in the browser. Anything that produces words takes the
 * message catalogue as an argument rather than reaching for a composable.
 */

export interface StatusPresentation {
  label: string
  /** Nuxt UI colour alias. */
  color: 'primary' | 'success' | 'neutral'
  icon: string
}

export function goalStatusPresentation(status: GoalStatus, t: Messages): StatusPresentation {
  switch (status) {
    case 'Completed':
      return { label: t.status.Completed, color: 'success', icon: 'i-lucide-circle-check' }
    case 'Archived':
      return { label: t.status.Archived, color: 'neutral', icon: 'i-lucide-archive' }
    case 'Active':
    default:
      return { label: t.status.Active, color: 'primary', icon: 'i-lucide-circle-dot' }
  }
}

/** Clamps to 0-100 so a bad value can never render a broken bar. */
export function clampProgress(percent: number): number {
  if (!Number.isFinite(percent)) return 0
  return Math.min(100, Math.max(0, Math.round(percent)))
}

/** The icon name a goal's `icon` maps to. */
export function goalIconName(icon: string): string {
  return `i-lucide-${icon}`
}

/**
 * "07:00" from an API time or an ISO instant.
 *
 * Formatted by hand rather than with `Intl.DateTimeFormat`: ICU data differs
 * between Node versions and browsers, which would make the server-rendered
 * HTML differ from the client's and trigger a hydration mismatch.
 *
 * `offsetMinutes` shifts an instant into the reader's zone. It is zero during
 * server rendering and corrected after hydration — see `useTimeZoneOffset`.
 * A `TimeOnly` such as a reminder is already wall-clock and is shown as
 * written, whatever the offset.
 */
export function formatClock(value: string | null, offsetMinutes = 0): string | null {
  if (!value) return null

  // A `TimeOnly` arrives as "07:00:00"; an instant as a full ISO string.
  if (!value.includes('T')) {
    const [hours, minutes] = value.split(':')
    return hours && minutes ? `${pad(Number(hours))}:${pad(Number(minutes))}` : null
  }

  const local = shift(value, offsetMinutes)
  if (!local) return null

  return `${pad(local.getUTCHours())}:${pad(local.getUTCMinutes())}`
}

/**
 * The instant, moved into the reader's zone so the UTC getters read as local
 * time. Returns null for anything unparseable.
 */
function shift(iso: string, offsetMinutes: number): Date | null {
  const instant = new Date(iso)
  if (Number.isNaN(instant.getTime())) return null

  return new Date(instant.getTime() + offsetMinutes * 60_000)
}

function pad(value: number): string {
  return String(value).padStart(2, '0')
}

/**
 * "gerade eben", "vor 12 Min", "vor 3 Std", "gestern", "vor 4 Tagen".
 *
 * Stops at days on purpose: past a week the number stops meaning anything and
 * the feed is not an archive.
 */
export function formatRelativeTime(iso: string, now: number, t: Messages): string {
  const then = Date.parse(iso)
  if (Number.isNaN(then)) return ''

  const minutes = Math.max(0, Math.round((now - then) / 60_000))
  if (minutes < 1) return t.time.justNow
  if (minutes < 60) return t.time.minutesAgo(minutes)

  const hours = Math.floor(minutes / 60)
  if (hours < 24) return t.time.hoursAgo(hours)

  const days = Math.floor(hours / 24)
  return days === 1 ? t.time.yesterday : t.time.daysAgo(days)
}

/**
 * The timestamp a chat list shows: the clock today, "gestern" yesterday, the
 * weekday within the week, and a date beyond that.
 */
export function formatChatTime(iso: string | null, now: number, t: Messages, offsetMinutes = 0): string {
  if (!iso) return ''

  const then = shift(iso, offsetMinutes)
  if (!then) return ''

  const today = new Date(now + offsetMinutes * 60_000)
  const days = Math.floor((startOfDay(today) - startOfDay(then)) / 86_400_000)

  if (days <= 0) return formatClock(iso, offsetMinutes) ?? ''
  if (days === 1) return t.time.yesterday
  if (days < 7) return t.time.weekdays[then.getUTCDay()] ?? ''

  return `${then.getUTCDate()}.${then.getUTCMonth() + 1}.`
}

/**
 * Midnight of the day a moment falls on, in whichever zone the moment has
 * already been shifted into. See {@link formatClock}.
 */
function startOfDay(date: Date): number {
  return Date.UTC(date.getUTCFullYear(), date.getUTCMonth(), date.getUTCDate())
}

/** "1,2 / 2 L" — the amount under a measurable task. */
export function formatMeasure(task: Pick<GoalTask, 'measuredValue' | 'targetValue' | 'measureUnit'>, decimal: string): string | null {
  if (task.targetValue === null) return null

  const measured = formatAmount(task.measuredValue ?? 0, decimal)
  const target = formatAmount(task.targetValue, decimal)
  const unit = task.measureUnit ? ` ${task.measureUnit}` : ''

  return `${measured} / ${target}${unit}`
}

/**
 * A number with at most one decimal place, using the separator the current
 * language writes it with. Hand-rolled for the same reason as the clock above.
 */
export function formatAmount(value: number, decimal: string): string {
  const rounded = Math.round(value * 10) / 10
  return Number.isInteger(rounded) ? String(rounded) : String(rounded).replace('.', decimal)
}

/**
 * The sentence under somebody's name in the feed.
 *
 * Composed here from the structured event rather than stored as text, which is
 * what lets the same activity read as German or English — see
 * ActivityResponse on the server.
 */
export function activitySentence(activity: Activity, t: Messages): string {
  switch (activity.kind) {
    case 'TaskCompleted':
      return t.activity.taskCompleted(activity.subject ?? '')
    case 'StreakReached':
      return t.activity.streakReached(activity.amount ?? 0)
    case 'GoalProgress':
      return t.activity.goalProgress(activity.subject ?? '', activity.amount ?? 0)
    case 'GoalCreated':
      return t.activity.goalCreated(activity.subject ?? '')
    default:
      return activity.subject ?? ''
  }
}

/** "14 von 21 Schritten", or the plain percentage when there is nothing to count. */
export function goalSubtitle(goal: Pick<Goal, 'completedSteps' | 'totalSteps' | 'progressPercent'>, t: Messages): string {
  return goal.totalSteps > 1
    ? t.goals.steps(goal.completedSteps, goal.totalSteps)
    : t.goals.progressLabel(goal.progressPercent)
}
