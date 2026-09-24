import type { Messages } from '~/i18n/messages'
import type { ChatMessage, GoalEvent, Proof } from '~/api/types'
import { addDays, formatDay, formatWeekdayDay, localDay } from './display'

/**
 * One row of a thread: something somebody wrote, a photograph with its vote,
 * or a line about what happened to the goal.
 */
export type ThreadItem
  = | { kind: 'message', key: string, at: string, message: ChatMessage }
    | { kind: 'proof', key: string, at: string, event: GoalEvent, proof: Proof }
    | { kind: 'event', key: string, at: string, event: GoalEvent }

/**
 * The messages and the goal's events as one thread, oldest first.
 *
 * The server sends them as two lists, each already in order, because they are
 * two different things: what people wrote, and what the goal says happened
 * (docs/adr/0027-goal-conversations.md). Putting them side by side by time is
 * layout, not a rule — which is why it happens here. At the same instant the
 * event goes first: a photograph arrives before anybody can answer it.
 */
export function threadItems(messages: readonly ChatMessage[], events: readonly GoalEvent[]): ThreadItem[] {
  const items: ThreadItem[] = []
  let m = 0
  let e = 0

  while (m < messages.length || e < events.length) {
    const message = messages[m]
    const event = events[e]

    if (event && (!message || Date.parse(event.at) <= Date.parse(message.sentAt))) {
      items.push(event.proof
        ? { kind: 'proof', key: `event:${event.key}`, at: event.at, event, proof: event.proof }
        : { kind: 'event', key: `event:${event.key}`, at: event.at, event })
      e++
    }
    else if (message) {
      items.push({ kind: 'message', key: `message:${message.id}`, at: message.sentAt, message })
      m++
    }
  }

  return items
}

/**
 * What the thread actually draws: the items, a separator wherever the day
 * changes, and a run of missed windows folded into one line.
 */
export type ThreadRow
  = | ThreadItem
    | { kind: 'day', key: string, label: string }
    | { kind: 'missedRun', key: string, from: string, to: string, count: number }

/**
 * The items as rows, with day separators and missed windows folded together.
 *
 * Two or more missed windows in a row, with nothing said in between, become
 * one line — "7.–23.9. · 15 Fenster verpasst". Fifteen identical lines tell
 * nobody anything more than the one does, and they bury what people wrote.
 * The days come from the windows (`day`), not from when the misses were
 * settled, because a run is usually settled in one go the next time anybody
 * looks.
 *
 * A separator goes before what people said and sent — messages and
 * photographs — when the day has changed. A line about a window carries its
 * own day instead ("Geschafft · Streak 3 · 3.9."): a daily goal has one on
 * every day, and a separator above each would double the thread.
 *
 * `offsetMinutes` is the reader's zone, zero until hydration — see
 * `useTimeZoneOffset`.
 */
export function threadRows(items: readonly ThreadItem[], now: number, t: Messages, offsetMinutes = 0): ThreadRow[] {
  const folded: ThreadRow[] = []

  for (let index = 0; index < items.length; index++) {
    const item = items[index]!
    let end = index

    if (isMissed(item)) {
      while (isMissed(items[end + 1])) end++
    }

    if (end > index) {
      const run = items.slice(index, end + 1)
      const days = run.map(entry => windowDay(entry, offsetMinutes)).sort()

      folded.push({
        kind: 'missedRun',
        key: `missed:${item.key}`,
        from: formatDay(days[0]!),
        to: formatDay(days.at(-1)!),
        count: run.length,
      })
      index = end
      continue
    }

    folded.push(item)
  }

  const today = localDay(now, offsetMinutes)
  const rows: ThreadRow[] = []
  let current: string | null = null

  for (const row of folded) {
    if (row.kind === 'message' || row.kind === 'proof') {
      const day = localDay(Date.parse(row.at), offsetMinutes)

      if (day !== current) {
        rows.push({ kind: 'day', key: `day:${day}`, label: dayLabel(day, today, t) })
        current = day
      }
    }

    rows.push(row)
  }

  return rows
}

function isMissed(item: ThreadItem | undefined): boolean {
  return item?.kind === 'event' && item.event.kind === 'WindowMissed'
}

function windowDay(item: ThreadItem, offsetMinutes: number): string {
  if (item.kind !== 'message' && item.event.day) return item.event.day
  return localDay(Date.parse(item.at), offsetMinutes)
}

function dayLabel(day: string, today: string, t: Messages): string {
  if (day === today) return t.chats.dayToday
  if (day === addDays(today, -1)) return t.chats.dayYesterday
  return formatWeekdayDay(day, t)
}

/**
 * The sentence for a line in the thread.
 *
 * `actorName` is null for the reader's own doing, which the catalogue words as
 * "Du". A photograph is drawn as itself and has no sentence of its own.
 */
export function goalEventText(event: GoalEvent, t: Messages): string {
  const name = event.isMine ? null : event.actorName

  switch (event.kind) {
    case 'Created':
      return t.chats.event.created(name)
    // A window's line says which day it was: the thread puts no separator
    // above it (see threadRows), and a miss is settled whenever somebody next
    // looks, so where it sits does not say so either.
    case 'WindowDone': {
      const text = t.chats.event.windowDone(event.streak ?? 0)
      return event.day ? t.chats.onDay(text, formatDay(event.day)) : text
    }
    case 'WindowMissed': {
      const text = t.chats.event.windowMissed(event.confirmedProofs ?? 0, event.requiredProofs ?? 1)
      return event.day ? t.chats.onDay(text, formatDay(event.day)) : text
    }
    case 'PauseStarted':
      return t.chats.event.pauseStarted(name, event.until ? formatDay(event.until) : '')
    case 'PauseEnded':
      return t.chats.event.pauseEnded
    case 'PauseOverturned':
      return t.chats.event.pauseOverturned
    case 'Completed':
      return t.chats.event.completed
    case 'Stopped':
      return t.chats.event.stopped(name)
    case 'ProofDelivered':
    default:
      return t.chats.lastEvent.ProofDelivered
  }
}

/**
 * The icon beside a line. Grey like every state — only a kept window's flame
 * carries the streak gradient, because that is what the flame means.
 */
export function goalEventIcon(kind: GoalEvent['kind']): string {
  switch (kind) {
    case 'Created':
      return 'i-lucide-sparkles'
    case 'WindowDone':
      return 'i-lucide-flame'
    case 'WindowMissed':
      return 'i-lucide-circle-x'
    case 'PauseStarted':
      return 'i-lucide-pause'
    case 'PauseEnded':
    case 'PauseOverturned':
      return 'i-lucide-play'
    case 'Completed':
      return 'i-lucide-trophy'
    case 'Stopped':
      return 'i-lucide-archive'
    case 'ProofDelivered':
    default:
      return 'i-lucide-camera'
  }
}
