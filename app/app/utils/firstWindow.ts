import type { GoalScheduleRequest, Weekday } from '~/api/types'
import { addDays } from './display'

/**
 * The last day of a new goal's first window, for the create sheet's summary.
 *
 * A mirror of `GoalSchedule.FirstWindow` on the server, kept deliberately
 * small: it only has to say which day the first deadline falls on, before the
 * goal exists. The goal itself is always answered by the server.
 *
 * - every n days starts today;
 * - chosen weekdays start on the next chosen day, today included;
 * - a quota runs to the end of its week (Sunday) or month;
 * - once is due today, because the sheet has no date for it.
 */
export function firstWindowEnd(schedule: GoalScheduleRequest, today: string): string {
  switch (schedule.kind) {
    case 'Weekdays':
      return nextMatchingDay(today, schedule.weekdays ?? []) ?? today

    case 'Times':
      return schedule.period === 'Month' ? lastOfMonth(today) : sundayOf(today)

    case 'Interval':
    case 'Once':
    default:
      return today
  }
}

const order: readonly Weekday[] = ['Monday', 'Tuesday', 'Wednesday', 'Thursday', 'Friday', 'Saturday', 'Sunday']

/** Monday-first position of a day, 0 to 6. */
function weekdayOf(day: string): number {
  const [year, month, date] = day.split('-').map(Number)
  return (new Date(Date.UTC(year ?? 1970, (month ?? 1) - 1, date ?? 1)).getUTCDay() + 6) % 7
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

function lastOfMonth(day: string): string {
  const [year, month] = day.split('-').map(Number)
  return new Date(Date.UTC(year ?? 1970, month ?? 1, 0)).toISOString().slice(0, 10)
}
