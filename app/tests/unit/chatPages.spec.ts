import { describe, expect, it } from 'vitest'
import type { ChatDetail, ChatMessage, GoalEvent, Proof } from '~/api/types'
import { carryOver, freshHistory, prependPage, withMessage, withProof } from '~/utils/chatPages'

function message(id: string, overrides: Partial<ChatMessage> = {}): ChatMessage {
  return {
    id,
    senderId: 'person-1',
    senderName: null,
    text: id,
    image: null,
    isMine: false,
    sentAt: '2026-10-18T09:00:00Z',
    reactions: [],
    ...overrides,
  }
}

function event(key: string, proof: Proof | null = null): GoalEvent {
  return { key, kind: proof ? 'ProofDelivered' : 'WindowDone', at: '2026-10-18T09:00:00Z', isMine: false, proof } as GoalEvent
}

function page(messages: string[], olderCursor: string | null, overrides: Partial<ChatDetail> = {}): ChatDetail {
  return { id: 'chat-1', messages: messages.map(id => message(id)), events: [], olderCursor, ...overrides } as ChatDetail
}

const ids = (messages: readonly ChatMessage[]) => messages.map(entry => entry.id)

describe('a thread loaded a page at a time', () => {
  it('starts with nothing beyond the newest page and its cursor', () => {
    expect(freshHistory(page(['m3', 'm4'], 'before-m3'))).toEqual({
      chatId: 'chat-1',
      messages: [],
      events: [],
      older: 'before-m3',
    })
  })

  it('keeps what drops off the newest page as new lines arrive, and keeps its cursor', () => {
    const first = page(['m3', 'm4'], 'before-m3')
    const history = { ...freshHistory(first), messages: [message('m2')], older: 'before-m2' }

    const next = carryOver(history, first, page(['m4', 'm5'], 'before-m4'))

    expect(ids(next.messages)).toEqual(['m2', 'm3'])
    expect(next.older).toBe('before-m2')
  })

  it('stays as it is when the newest page is read again unchanged', () => {
    const first = page(['m1', 'm2'], null)
    const history = freshHistory(first)

    expect(carryOver(history, first, page(['m1', 'm2'], null))).toBe(history)
  })

  it('starts again when more arrived at once than a page holds', () => {
    // Nothing in common: what lies between the two pages was never read, and
    // drawing the thread without it would hide it for good.
    const first = page(['m3', 'm4'], 'before-m3')
    const history = { ...freshHistory(first), messages: [message('m2')], older: 'before-m2' }

    expect(carryOver(history, first, page(['m9', 'm10'], 'before-m9'))).toEqual(freshHistory(page(['m9', 'm10'], 'before-m9')))
  })

  it('starts again for another thread, and for a first page', () => {
    const first = page(['m1'], null)
    const other = page(['x1'], 'before-x1', { id: 'chat-2' })

    expect(carryOver(freshHistory(first), first, other)).toEqual(freshHistory(other))
    expect(carryOver(null, null, first)).toEqual(freshHistory(first))
  })

  it('carries the goal\'s lines along with the messages', () => {
    const first = page(['m2'], null, { events: [event('Created-1'), event('WindowDone-2')] })
    const next = carryOver(freshHistory(first), first, page(['m2', 'm3'], 'before-m2', { events: [event('WindowDone-2')] }))

    expect(next.events.map(entry => entry.key)).toEqual(['Created-1'])
  })

  it('puts a page read further back in front, without a line twice', () => {
    const history = { ...freshHistory(page(['m3'], 'before-m3')), messages: [message('m2')], older: 'before-m2' }

    const next = prependPage(history, { messages: [message('m1'), message('m2')], events: [event('Created-1')], olderCursor: null })

    expect(ids(next.messages)).toEqual(['m1', 'm2'])
    expect(next.events.map(entry => entry.key)).toEqual(['Created-1'])
    expect(next.older).toBeNull()
  })

  it('puts a reacted message and a decided photograph where they are loaded, and only there', () => {
    const holder = { messages: [message('m1'), message('m2')], events: [event('ProofDelivered-1', { id: 'proof-1' } as Proof)] }
    const reacted = message('m2', { reactions: [{ kind: 'Fire', count: 1, isMine: true }] })

    expect(withMessage(holder, reacted).messages[1]).toBe(reacted)
    expect(withMessage(holder, message('elsewhere'))).toBe(holder)

    const decided = { id: 'proof-1', status: 'Confirmed' } as Proof
    expect(withProof(holder, decided).events[0]?.proof).toBe(decided)
    expect(withProof(holder, { id: 'proof-9' } as Proof)).toBe(holder)
  })
})
