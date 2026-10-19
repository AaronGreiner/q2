import type { Messages } from '~/i18n/messages'
import type { GoalChange } from '~/api/types'
import { formatClock, formatDay, formatWeekdayDay, goalIconName, localDay, scheduleLabel } from './display'

/** One line of an edit: what changed, from what to what. */
export interface GoalChangeItem {
  key: string
  icon: string
  text: string

  /**
   * Whether it applied the moment it was made — the title, the icon, a
   * checker added, an earlier day — or waits for the next window.
   */
  now: boolean
}

/**
 * An edit of a running goal as lines a person reads, in the order the sheet
 * asks for them (docs/adr/0044-editing-a-running-goal.md).
 *
 * The server sends which fields moved and from what to what; the sentences are
 * composed here, for the same reason every other one is — the app speaks two
 * languages. A Don't's schedule is its period, so a changed one is worded as
 * the period it is counted over rather than as a rhythm.
 */
export function goalChangeItems(change: GoalChange, t: Messages): GoalChangeItem[] {
  const words = t.edit.item
  const items: GoalChangeItem[] = []

  if (change.titleTo) {
    items.push({ key: 'title', icon: 'i-lucide-type', text: words.title(change.titleFrom ?? '', change.titleTo), now: true })
  }

  if (change.iconTo) {
    items.push({ key: 'icon', icon: goalIconName(change.iconTo), text: words.icon, now: true })
  }

  if (change.descriptionChanged) {
    items.push({
      key: 'description',
      icon: 'i-lucide-text',
      text: change.descriptionTo ? words.description : words.descriptionRemoved,
      now: false,
    })
  }

  if (change.scheduleFrom && change.scheduleTo) {
    const isPeriod = change.scheduleTo.kind === 'Limit'

    items.push({
      key: 'schedule',
      icon: 'i-lucide-repeat',
      text: isPeriod
        ? words.period(
            t.schedule.period[change.scheduleFrom.period ?? 'Day'],
            t.schedule.period[change.scheduleTo.period ?? 'Day'])
        : words.schedule(scheduleLabel(change.scheduleFrom, t), scheduleLabel(change.scheduleTo, t)),
      now: false,
    })
  }

  if (isSet(change.limitFrom) && isSet(change.limitTo)) {
    items.push({
      key: 'limit',
      icon: 'i-lucide-ban',
      text: words.limit(words.limitValue(change.limitFrom), words.limitValue(change.limitTo)),
      now: false,
    })
  }

  if (change.proofModeFrom && change.proofModeTo) {
    items.push({
      key: 'proofMode',
      icon: 'i-lucide-shield-check',
      text: words.proofMode(t.dont.proofMode[change.proofModeFrom], t.dont.proofMode[change.proofModeTo]),
      now: false,
    })
  }

  if (change.targetDateFrom && change.targetDateTo) {
    items.push({
      key: 'targetDate',
      icon: 'i-lucide-calendar',
      text: words.targetDate(formatDay(change.targetDateFrom), formatDay(change.targetDateTo)),

      // An earlier day asks more, and moves the deadline at once.
      now: change.targetDateTo < change.targetDateFrom,
    })
  }

  for (const person of change.checkersAdded) {
    items.push({ key: `added:${person.id}`, icon: 'i-lucide-user-plus', text: words.checkerAdded(person.displayName), now: true })
  }

  for (const person of change.checkersRemoved) {
    items.push({ key: `removed:${person.id}`, icon: 'i-lucide-user-minus', text: words.checkerRemoved(person.displayName), now: false })
  }

  return items
}

/** "Gilt ab Mi, 17.6." for the half that waits, or the next window when the day is not known. */
export function goalChangeTiming(change: GoalChange, t: Messages): string {
  return change.appliesOn ? t.edit.from(formatWeekdayDay(change.appliesOn, t)) : t.edit.fromNext
}

/**
 * The moment an easier change stops being open to objection — "Di, 16.6.,
 * 11:00 Uhr" — in the reader's zone. Null for one that cannot be objected to.
 */
export function objectionDeadline(change: GoalChange, t: Messages, offsetMinutes = 0): string | null {
  if (!change.objectable || !change.objectionEndsAt) return null

  const day = localDay(Date.parse(change.objectionEndsAt), offsetMinutes)
  const clock = formatClock(change.objectionEndsAt, offsetMinutes) ?? ''

  return t.edit.moment(formatWeekdayDay(day, t), clock)
}

function isSet(value: number | null | undefined): value is number {
  return value !== null && value !== undefined
}
