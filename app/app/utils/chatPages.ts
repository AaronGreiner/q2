import type { ChatDetail, ChatMessage, ChatPage, GoalEvent, Proof } from '~/api/types'

/**
 * What of a thread is loaded beyond its newest page (#44).
 *
 * The server sends a thread a page at a time: opening it reads the newest page,
 * and scrolling up reads the ones before it through the cursor each page hands
 * out. The newest page is read again whenever something changes — a reply over
 * the live connection, a message sent from here — and everything else that has
 * been loaded is kept here, in front of it.
 *
 * Every line in here is older than every line on the newest page, so the two
 * are put together by appending, and the thread draws them as one
 * (`threadItems`). Nothing is ordered on this side.
 */
export interface ThreadHistory {
  /** The thread this belongs to; another thread starts from nothing. */
  chatId: string
  messages: ChatMessage[]
  events: GoalEvent[]

  /** The cursor for the page before the oldest line loaded, or null at the beginning of the thread. */
  older: string | null
}

/** Nothing loaded beyond the newest page yet. */
export function freshHistory(chat: ChatDetail): ThreadHistory {
  return { chatId: chat.id, messages: [], events: [], older: chat.olderCursor ?? null }
}

/**
 * The history once the newest page has been read again.
 *
 * When new lines arrive, the newest page moves on and its oldest lines drop off
 * the front of it. They are kept: they are exactly what lies between the
 * history and the new page, and losing them would leave a hole in the middle of
 * the thread. The cursor stays where it was, because nothing before the history
 * has been read.
 *
 * When the two pages have no line in common, more arrived at once than a page
 * holds, and what lies between them is unknown. The history starts again from
 * the new page rather than drawing a thread with lines missing from it.
 */
export function carryOver(history: ThreadHistory | null, previous: ChatDetail | null, next: ChatDetail): ThreadHistory {
  if (!history || history.chatId !== next.id || !previous || previous.id !== next.id) {
    return freshHistory(next)
  }

  const messageIds = new Set(next.messages.map(message => message.id))
  const eventKeys = new Set(next.events.map(event => event.key))

  const overlaps = previous.messages.some(message => messageIds.has(message.id))
    || previous.events.some(event => eventKeys.has(event.key))

  if (!overlaps) return freshHistory(next)

  const messages = previous.messages.filter(message => !messageIds.has(message.id))
  const events = previous.events.filter(event => !eventKeys.has(event.key))

  if (messages.length === 0 && events.length === 0) return history

  return {
    ...history,
    messages: [...history.messages, ...messages],
    events: [...history.events, ...events],
  }
}

/** A page read further back, put in front of what is loaded. */
export function prependPage(history: ThreadHistory, page: ChatPage): ThreadHistory {
  const messageIds = new Set(history.messages.map(message => message.id))
  const eventKeys = new Set(history.events.map(event => event.key))

  return {
    ...history,
    messages: [...page.messages.filter(message => !messageIds.has(message.id)), ...history.messages],
    events: [...page.events.filter(event => !eventKeys.has(event.key)), ...history.events],
    older: page.olderCursor ?? null,
  }
}

/**
 * A message as the server now has it — after a reaction, say — wherever it is
 * loaded. A list without it comes back as it was, so nothing redraws.
 */
export function withMessage<T extends { messages: ChatMessage[] }>(holder: T, message: ChatMessage): T {
  if (!holder.messages.some(entry => entry.id === message.id)) return holder

  return { ...holder, messages: holder.messages.map(entry => (entry.id === message.id ? message : entry)) }
}

/** A photograph as the server now has it — after a vote — wherever it is loaded. */
export function withProof<T extends { events: GoalEvent[] }>(holder: T, proof: Proof): T {
  if (!holder.events.some(event => event.proof?.id === proof.id)) return holder

  return { ...holder, events: holder.events.map(event => (event.proof?.id === proof.id ? { ...event, proof } : event)) }
}
