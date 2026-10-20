import type { GoalScheduleRequest, Weekday } from '~/api/types'
import { addDays } from './display'

/**
 * A new goal's first window, for the create sheet's summary: from which day,
 * until which day, and how much of its period it covers.
 *
 * A mirror of `GoalSchedule.FirstWindow` on the server, kept deliberately
 * small: it only has to say what the first window will ask before the goal
 * exists. The goal itself is always answered by the server.
 *
 * - every n days starts on the day it starts;
 * - chosen weekdays start on the next chosen day, that day included;
 * - a quota runs to the end of its week (Sunday) or month, from the day the
 *   goal starts — and asks for its share of the period (#96, `prorate`);
 * - once is due on the day it was given (`targetDate`), today without one —
 *   the server's `FirstWindow(today, dueOn)` does the same.
 *
 * `from` is the first day the goal counts for: today, or tomorrow when its
 * owner chose to start then.
 */
export interface FirstWindow {
  start: string
  end: string
  /** Days the window covers. */
  days: number
  /** Days of the full period it was cut from; the same as `days` when it was not cut. */
  fullDays: number
}

export function firstWindow(schedule: GoalScheduleRequest, from: string, targetDate: string | null = null): FirstWindow {
  switch (schedule.kind) {
    case 'Weekdays': {
      const day = nextMatchingDay(from, schedule.weekdays ?? []) ?? from
      return single(day)
    }

    case 'Times':
      return period(from, schedule.period === 'Month' ? 'Month' : 'Week')

    // A Don't's period: the day, the week, or the month.
    case 'Limit':
      return schedule.period === 'Day' ? single(from) : period(from, schedule.period === 'Month' ? 'Month' : 'Week')

    case 'Once':
      return { start: from, end: targetDate ?? from, days: 1, fullDays: 1 }

    case 'Interval':
    default:
      return single(from)
  }
}

/** The last day of the first window — what the summary has always said. */
export function firstWindowEnd(schedule: GoalScheduleRequest, from: string, targetDate: string | null = null): string {
  return firstWindow(schedule, from, targetDate).end
}

/**
 * A quota or a limit scaled to the days a cut window covers: the share,
 * rounded up, never below `minimum`. `GoalSchedule.Prorate` on the server —
 * at least one photograph for a Do, possibly none for a Don't.
 */
export function prorate(amount: number, window: FirstWindow, minimum: number): number {
  if (window.fullDays <= 0 || window.days >= window.fullDays) return Math.max(minimum, amount)
  return Math.max(minimum, Math.ceil(amount * window.days / window.fullDays))
}

const order: readonly Weekday[] = ['Monday', 'Tuesday', 'Wednesday', 'Thursday', 'Friday', 'Saturday', 'Sunday']

function single(day: string): FirstWindow {
  return { start: day, end: day, days: 1, fullDays: 1 }
}

function period(from: string, kind: 'Week' | 'Month'): FirstWindow {
  const first = kind === 'Month' ? firstOfMonth(from) : addDays(from, -weekdayOf(from))
  const end = kind === 'Month' ? lastOfMonth(from) : sundayOf(from)

  return { start: from, end, days: daysBetween(from, end) + 1, fullDays: daysBetween(first, end) + 1 }
}

/** Monday-first position of a day, 0 to 6. */
function weekdayOf(day: string): number {
  return (utc(day).getUTCDay() + 6) % 7
}

function utc(day: string): Date {
  const [year, month, date] = day.split('-').map(Number)
  return new Date(Date.UTC(year ?? 1970, (month ?? 1) - 1, date ?? 1))
}

function daysBetween(from: string, to: string): number {
  return Math.round((utc(to).getTime() - utc(from).getTime()) / 86_400_000)
}

function nextMatchingDay(from: string, days: readonly Weekday[]): string | null {
  for (let step = 0; step < 7; step++) {
    const candidate = addDays(from, step)
    if (days.includes(order[weekdayOf(candidate)]!)) return candidate
  }

  return null
}

function sundayOf(day: string): string {
  return addDays(day, 6 - weekdayOf(day))
}

function firstOfMonth(day: string): string {
  return `${day.slice(0, 7)}-01`
}

function lastOfMonth(day: string): string {
  const [year, month] = day.split('-').map(Number)
  return new Date(Date.UTC(year ?? 1970, month ?? 1, 0)).toISOString().slice(0, 10)
}
