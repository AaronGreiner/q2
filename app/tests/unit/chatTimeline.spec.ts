import { describe, expect, it } from 'vitest'
import { de, en } from '~/i18n/messages'
import { goalEventIcon, goalEventText, threadItems, threadRows } from '~/utils/chatTimeline'
import type { ChatMessage, GoalEvent, Proof } from '~/api/types'

function message(id: string, sentAt: string): ChatMessage {
  return { id, senderId: 'person-1', senderName: null, text: id, isMine: false, sentAt, reactions: [] }
}

function event(key: string, at: string, overrides: Partial<GoalEvent> = {}): GoalEvent {
  return {
    key,
    kind: 'WindowDone',
    at,
    actorName: null,
    isMine: false,
    streak: 1,
    confirmedProofs: 1,
    requiredProofs: 1,
    until: null,
    proof: null,
    day: null,
    ...overrides,
  }
}

describe('threadItems', () => {
  it('puts messages and what happened to the goal side by side by time', () => {
    const items = threadItems(
      [message('m1', '2026-07-31T09:00:00+00:00'), message('m2', '2026-07-31T11:00:00+00:00')],
      [event('e1', '2026-07-31T08:00:00+00:00'), event('e2', '2026-07-31T10:00:00+00:00')],
    )

    expect(items.map(item => item.key)).toEqual(['event:e1', 'message:m1', 'event:e2', 'message:m2'])
  })

  it('puts the event first at the same instant — a photograph arrives before anybody answers it', () => {
    const items = threadItems(
      [message('m1', '2026-07-31T09:00:00+00:00')],
      [event('e1', '2026-07-31T09:00:00+00:00')],
    )

    expect(items.map(item => item.kind)).toEqual(['event', 'message'])
  })

  it('compares instants, not strings, whatever offset they were written in', () => {
    const items = threadItems(
      [message('m1', '2026-07-31T10:30:00+02:00')],
      [event('e1', '2026-07-31T09:00:00+00:00')],
    )

    expect(items.map(item => item.kind)).toEqual(['message', 'event'])
  })

  it('draws a delivered photograph as a proof rather than as a line', () => {
    const proof = { id: 'proof-1' } as Proof
    const [item] = threadItems([], [event('e1', '2026-07-31T09:00:00+00:00', { kind: 'ProofDelivered', proof })])

    expect(item).toMatchObject({ kind: 'proof', proof })
  })

  it('keys every row uniquely even when a message and an event share an id', () => {
    const items = threadItems(
      [message('same', '2026-07-31T09:00:00+00:00')],
      [event('same', '2026-07-31T09:00:00+00:00')],
    )

    expect(new Set(items.map(item => item.key)).size).toBe(2)
  })
})

describe('goalEventText', () => {
  it('words every kind in both languages', () => {
    const kinds = ['Created', 'WindowDone', 'WindowMissed', 'PauseStarted', 'PauseEnded', 'PauseOverturned', 'Completed', 'Stopped'] as const

    for (const kind of kinds) {
      const entry = event('e', '2026-07-31T09:00:00+00:00', { kind, until: '2026-08-03', actorName: 'Lena' })

      expect(goalEventText(entry, de)).not.toBe('')
      expect(goalEventText(entry, en)).not.toBe(goalEventText(entry, de))
      expect(goalEventIcon(kind)).toMatch(/^i-lucide-/)
    }
  })

  it('says a single missed window without a tally, and a quota with one', () => {
    expect(goalEventText(event('e', '2026-07-31T09:00:00+00:00', { kind: 'WindowMissed', confirmedProofs: 0, requiredProofs: 1 }), de))
      .toBe('Verpasst')
    expect(goalEventText(event('e', '2026-07-31T09:00:00+00:00', { kind: 'WindowMissed', confirmedProofs: 2, requiredProofs: 3 }), de))
      .toBe('Verpasst · 2 von 3 geliefert')
  })

  it('never names the reader, even if a name came along', () => {
    const mine = event('e', '2026-07-31T09:00:00+00:00', { kind: 'Stopped', isMine: true, actorName: 'Mara' })

    expect(goalEventText(mine, de)).toBe('Du hast das Ziel beendet.')
  })
})

describe('threadRows', () => {
  const now = Date.parse('2026-09-24T12:00:00Z')

  function missed(key: string, at: string, day: string) {
    return event(key, at, { kind: 'WindowMissed', streak: null, confirmedProofs: 0, day })
  }

  it('folds a run of missed windows into one line with the days they were due', () => {
    const items = threadItems([], [
      missed('a', '2026-09-24T08:00:00Z', '2026-09-21'),
      missed('b', '2026-09-24T08:00:00Z', '2026-09-22'),
      missed('c', '2026-09-24T08:00:00Z', '2026-09-23'),
    ])

    const rows = threadRows(items, now, de)

    expect(rows.map(row => row.kind)).toEqual(['missedRun'])
    expect(rows[0]).toMatchObject({ from: '21.9.', to: '23.9.', count: 3 })
    expect(de.chats.missedRun(3, de.chats.dayRange('21.9.', '23.9.'))).toBe('21.9.–23.9. · 3 Fenster verpasst')
  })

  it('leaves a single miss alone, and a message breaks a run', () => {
    const items = threadItems(
      [message('m1', '2026-09-24T09:00:00Z')],
      [
        missed('a', '2026-09-24T08:00:00Z', '2026-09-21'),
        missed('b', '2026-09-24T10:00:00Z', '2026-09-22'),
        missed('c', '2026-09-24T10:00:00Z', '2026-09-23'),
      ],
    )

    expect(threadRows(items, now, de).map(row => row.kind)).toEqual(['event', 'day', 'message', 'missedRun'])
  })

  it('puts a separator wherever the day changes, and names today and yesterday', () => {
    const items = threadItems(
      [
        message('m1', '2026-09-20T09:00:00Z'),
        message('m2', '2026-09-23T09:00:00Z'),
        message('m3', '2026-09-24T09:00:00Z'),
        message('m4', '2026-09-24T10:00:00Z'),
      ],
      [],
    )

    const labels = threadRows(items, now, de)
      .filter(row => row.kind === 'day')
      .map(row => (row.kind === 'day' ? row.label : ''))

    expect(labels).toEqual(['So, 20.9.', 'Gestern', 'Heute'])
  })

  it('says which day a window was, since no separator stands above it', () => {
    expect(goalEventText(missed('a', '2026-09-24T08:00:00Z', '2026-09-21'), de)).toBe('Verpasst · 21.9.')
    expect(goalEventText(event('k', '2026-09-24T08:00:00Z', { day: '2026-09-23', streak: 3 }), de)).toBe('Geschafft · Streak 3 · 23.9.')
  })

  it('puts no separator between lines about the goal', () => {
    const items = threadItems([], [
      event('a', '2026-09-22T21:00:00Z', { day: '2026-09-22' }),
      event('b', '2026-09-23T21:00:00Z', { day: '2026-09-23' }),
    ])

    expect(threadRows(items, now, de).map(row => row.kind)).toEqual(['event', 'event'])
  })
})
