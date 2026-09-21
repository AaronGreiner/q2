import type { Messages } from '~/i18n/messages'
import type { ChatMessage, GoalEvent, Proof } from '~/api/types'
import { formatDay } from './display'

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
    case 'WindowDone':
      return t.chats.event.windowDone(event.streak ?? 0)
    case 'WindowMissed':
      return t.chats.event.windowMissed(event.confirmedProofs ?? 0, event.requiredProofs ?? 1)
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
