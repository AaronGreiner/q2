import type { Messages } from '~/i18n/messages'
import type { Activity, GoalSchedule, GoalStatus, GoalWindow, KudosKind, Risk, Weekday } from '~/api/types'

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

/**
 * The icon name a stored icon key maps to.
 *
 * The server stores the bare Lucide name — "sunrise", not "i-lucide-sunrise" —
 * because the prefix is this client's way of naming an icon and not part of the
 * contract. A native build would spell it differently and the database should
 * not have to be migrated for that.
 */
export function goalIconName(icon: string): string {
  return `i-lucide-${icon}`
}

/** The same, for a group conversation's avatar. */
export function groupIconName(icon: string): string {
  return `i-lucide-${icon}`
}

/**
 * The icon each kind of kudos is drawn with.
 *
 * Three shapes rather than three colours: the accent belongs to actions, and a
 * reaction somebody already gave is a state. What marks your own is the filled
 * surface, not a second hue.
 */
export function kudosIconName(kind: KudosKind): string {
  switch (kind) {
    case 'Fire':
      return 'i-lucide-flame'
    case 'Strong':
      return 'i-lucide-biceps-flexed'
    case 'Applause':
    default:
      return 'i-lucide-hand-heart'
  }
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
    case 'WindowAtRisk':
      return t.activity.windowAtRisk(activity.subject ?? '')
    default:
      return activity.subject ?? ''
  }
}

/**
 * Whether a feed entry is somebody's bad news.
 *
 * The one row in the feed that is not a celebration, and the one that must not
 * offer a way to pile on: a warning gets no kudos button. All three kudos are
 * approving, so the button could never be an insult on its own — but "stark
 * gemacht" under "droht zu verpassen" is a sentence nobody should be able to
 * send, and the cheapest way to make sure is not to draw the control.
 */
export function isWarning(activity: Activity): boolean {
  return activity.kind === 'WindowAtRisk'
}

/**
 * The words for a window that is close to being missed, from the reader's
 * point of view.
 *
 * Flat on purpose: what is missing and how long there is left. No count of past
 * failures and no "again" — the numbers are unpleasant enough, and at this
 * point nothing has actually gone wrong.
 */
export function riskSentence(risk: Risk, t: Messages): string {
  if (risk.reason === 'Tight') {
    return t.risk.tight(risk.missingProofs, risk.remainingDays)
  }

  return risk.requiredProofs === 1 ? t.risk.lastDayOne : t.risk.lastDayMany(risk.missingProofs)
}

/**
 * How often a goal is due, in one short line: "Jeden Tag", "Mo, Do",
 * "3× pro Woche".
 *
 * The server sends the schedule as data and this composes the words, for the
 * same reason the feed does: a sentence assembled on the server can only ever
 * be one language.
 */
export function scheduleLabel(schedule: GoalSchedule, t: Messages): string {
  switch (schedule.kind) {
    case 'Once':
      return t.schedule.once

    case 'Interval':
      return t.schedule.everyNDays(schedule.everyDays ?? 1)

    case 'Weekdays':
      return formatWeekdays(schedule.weekdays, t)

    case 'Times':
      return t.schedule.timesPer(schedule.times ?? 1, t.schedule.period[schedule.period ?? 'Week'])

    default:
      return ''
  }
}

/** The sentence under the picker, which says what the choice actually means. */
export function scheduleExplanation(schedule: GoalSchedule, t: Messages): string {
  switch (schedule.kind) {
    case 'Once':
      return t.schedule.explainOnce
    case 'Interval':
      return schedule.everyDays === 1 ? t.schedule.explainDaily : t.schedule.explainInterval
    case 'Weekdays':
      return t.schedule.explainWeekdays
    case 'Times':
      return t.schedule.explainTimes(schedule.times ?? 1, t.schedule.period[schedule.period ?? 'Week'])
    default:
      return ''
  }
}

/**
 * Chosen weekdays, shortened where they run together.
 *
 * "Mo–Fr" rather than "Mo, Di, Mi, Do, Fr", because five names is a line of
 * text where a range is a glance. Three in a row is the point at which the
 * range is shorter than the list.
 */
function formatWeekdays(days: readonly Weekday[], t: Messages): string {
  if (days.length === 0) return ''
  if (days.length === 7) return t.schedule.everyDay

  const indexes = days.map(day => weekdayOrder.indexOf(day)).sort((a, b) => a - b)
  const short = indexes.map(index => t.schedule.weekdayShort[index] ?? '')

  const isRun = indexes.length >= 3
    && indexes.every((value, index) => index === 0 || value === indexes[index - 1]! + 1)

  return isRun ? `${short[0]}–${short[short.length - 1]}` : short.join(', ')
}

/** Monday first, which is the order every weekday list in this app is in. */
const weekdayOrder: readonly Weekday[] = [
  'Monday',
  'Tuesday',
  'Wednesday',
  'Thursday',
  'Friday',
  'Saturday',
  'Sunday',
]

/**
 * What is left of the open window: "Noch 2 von 3" — or "Heute fällig" when one
 * proof is the whole of it, because "noch 1 von 1" says nothing.
 */
export function windowLabel(window: GoalWindow, t: Messages): string {
  if (window.requiredProofs > 1) {
    return t.window.remaining(window.remainingProofs, window.requiredProofs)
  }

  return window.startsOn === window.dueOn ? t.window.dueToday : t.window.dueBy(formatDay(window.dueOn))
}

/**
 * "15.6." — a day written short, the way the chat list writes a date.
 *
 * Hand-formatted for the same reason as the clock above: ICU data differs
 * between Node versions and browsers, and a server-rendered date that does not
 * match the client's is a hydration mismatch.
 */
export function formatDay(day: string): string {
  const [year, month, date] = day.split('-')
  if (!year || !month || !date) return day

  return `${Number(date)}.${Number(month)}.`
}

/**
 * The date part of an instant, as a day somebody can read: `15.6.2026`.
 *
 * Read straight off the ISO string rather than through `Date` or `Intl`. Both
 * would render one day on the server and possibly another in the browser, which
 * is a hydration mismatch — and `Intl`'s output differs between Node versions
 * anyway. The year is here and not in
 * {@link formatDay} because the archive is the one screen that shows things
 * from more than one of them.
 */
export function formatInstantDate(iso: string): string {
  const [year, month, day] = iso.slice(0, 10).split('-')
  if (!year || !month || !day) return iso

  return `${Number(day)}.${Number(month)}.${year}`
}

/** How full the open window is, as a percentage for a ring or a bar. */
export function windowPercent(window: GoalWindow): number {
  return window.requiredProofs === 0
    ? 0
    : clampProgress((window.confirmedProofs / window.requiredProofs) * 100)
}

/** The word for what became of a window, and the icon that goes with it. */
export function windowOutcome(window: GoalWindow, t: Messages): StatusPresentation {
  switch (window.status) {
    case 'Done':
      return { label: t.window.done, color: 'success', icon: 'i-lucide-check' }
    case 'Missed':
      return { label: t.window.missed, color: 'neutral', icon: 'i-lucide-x' }
    // A pause is not a result, so it gets the quietest treatment there is:
    // grey, and a word that says nothing happened rather than that it failed.
    case 'Paused':
      return { label: t.window.paused, color: 'neutral', icon: 'i-lucide-pause' }
    case 'Open':
    default:
      return { label: t.window.open, color: 'primary', icon: 'i-lucide-circle-dot' }
  }
}
