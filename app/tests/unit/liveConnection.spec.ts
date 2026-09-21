import { describe, expect, it, vi } from 'vitest'
import { createLiveLink, keysFor, liveEvents } from '~/composables/useLiveConnection'
import type { Counts, NotificationLine } from '~/api/types'

/**
 * The live connection's rules, without a server.
 *
 * What travels over it is asserted end to end in LiveHubTests on the server
 * and in the E2E suite. What is asserted here is what the client does with a
 * connection: when it opens one, that it never opens two, and that failing to
 * is an ordinary state.
 */

/** Stands in for a SignalR connection: the same states, and a way to play the server. */
function fakeTransport(options: { failToStart?: boolean } = {}) {
  const handlers = new Map<string, (payload: never) => void>()
  let reconnected: (() => void) | null = null

  const transport = {
    state: 'Disconnected',
    start: vi.fn(async () => {
      if (options.failToStart) throw new Error('offline')
      transport.state = 'Connected'
    }),
    stop: vi.fn(async () => {
      transport.state = 'Disconnected'
    }),
    on: vi.fn((event: string, handler: (payload: never) => void) => {
      handlers.set(event, handler)
    }),
    onreconnected: vi.fn((handler: () => void) => {
      reconnected = handler
    }),

    /** The server sending an event. */
    send(event: string, payload: unknown) {
      handlers.get(event)?.(payload as never)
    },

    /** The connection coming back after a drop. */
    reconnect() {
      reconnected?.()
    },
  }

  return transport
}

function link(transport = fakeTransport()) {
  const options = {
    connect: vi.fn(() => transport),
    onCounts: vi.fn(),
    onChanged: vi.fn(),
    onNotification: vi.fn(),
    onCaughtUp: vi.fn(),
  }

  return { transport, options, live: createLiveLink(options) }
}

const counts: Counts = { unreadChats: 2, pendingFriendRequests: 0, unseenNotifications: 1, proofsAwaitingVote: 0 }

const notification: NotificationLine = {
  id: null,
  kind: 'MessageReceived',
  actor: null,
  subject: null,
  excerpt: 'Hallo',
  amount: null,
  target: 'Conversation',
  targetId: 'chat-1',
  occurredAt: '2026-06-17T09:00:00Z',
  isNew: true,
}

describe('keysFor', () => {
  it('names a thread and a goal by their id, the way their own reads are keyed', () => {
    expect(keysFor({ area: 'Chats', id: 'chat-1' })).toEqual(['chats', 'chat:chat-1'])
    expect(keysFor({ area: 'Goals', id: 'goal-1' })).toContain('goal:goal-1')
  })

  it('refreshes only the list when no single thing changed', () => {
    expect(keysFor({ area: 'Chats', id: null })).toEqual(['chats'])
    expect(keysFor({ area: 'Goals', id: null })).not.toContain('goal:null')
  })

  it('reaches the start screen from a goal or the feed, since it shows both', () => {
    expect(keysFor({ area: 'Goals', id: null })).toContain('home')
    expect(keysFor({ area: 'Feed', id: null })).toEqual(expect.arrayContaining(['home', 'activity-overview']))
  })

  it('reads the chat list again when a goal or its photographs move, and a goal\'s thread by its goal', () => {
    // The list sorts by what last happened in a goal's conversation and marks
    // what waits for a vote; an open thread of that goal follows `goal:<id>`.
    expect(keysFor({ area: 'Goals', id: null })).toContain('chats')
    expect(keysFor({ area: 'Proofs', id: 'goal-1' })).toEqual(['proofs-pending', 'chats', 'goal:goal-1'])
  })

  it.each([
    ['Friends', ['friends']],
    ['Proofs', ['proofs-pending', 'chats']],
    ['Challenge', ['challenge-today']],
    ['Notifications', ['notifications']],
  ] as const)('maps %s to its read', (area, keys) => {
    expect(keysFor({ area, id: null })).toEqual(keys)
  })
})

describe('createLiveLink', () => {
  it('does not connect until it is opened', () => {
    const { options } = link()

    expect(options.connect).not.toHaveBeenCalled()
  })

  it('connects once, and catches up on what moved while it was not', async () => {
    const { transport, options, live } = link()

    await live.open()
    await live.open()

    expect(options.connect).toHaveBeenCalledTimes(1)
    expect(transport.start).toHaveBeenCalledTimes(1)
    expect(options.onCaughtUp).toHaveBeenCalledTimes(1)
  })

  it('hands on what the server sends', async () => {
    const { transport, options, live } = link()
    await live.open()

    transport.send(liveEvents.counts, counts)
    transport.send(liveEvents.changed, { area: 'Chats', id: 'chat-1' })
    transport.send(liveEvents.notification, notification)

    expect(options.onCounts).toHaveBeenCalledWith(counts)
    expect(options.onChanged).toHaveBeenCalledWith({ area: 'Chats', id: 'chat-1' })
    expect(options.onNotification).toHaveBeenCalledWith(notification)
  })

  it('listens for exactly the events the server names', async () => {
    const { transport, live } = link()
    await live.open()

    // A typo here is a banner that never appears, with nothing to say why.
    expect(transport.on.mock.calls.map(([event]) => event).sort())
      .toEqual(['changed', 'counts', 'notification'])
  })

  it('catches up again after a dropped connection comes back', async () => {
    const { transport, options, live } = link()
    await live.open()

    transport.reconnect()

    expect(options.onCaughtUp).toHaveBeenCalledTimes(2)
  })

  it('treats failing to connect as an ordinary state, and tries again when asked', async () => {
    const transport = fakeTransport({ failToStart: true })
    const { options, live } = link(transport)

    await expect(live.open()).resolves.toBeUndefined()
    expect(options.onCaughtUp).not.toHaveBeenCalled()

    await live.open()
    expect(transport.start).toHaveBeenCalledTimes(2)
    expect(options.connect).toHaveBeenCalledTimes(1)
  })

  it('lets go when closed, and closing twice is closing once', async () => {
    const { transport, live } = link()
    await live.open()

    await live.close()
    await live.close()

    expect(transport.stop).toHaveBeenCalledTimes(1)
    expect(transport.state).toBe('Disconnected')
  })

  it('has nothing to close before it was ever opened', async () => {
    const { options, live } = link()

    await live.close()

    expect(options.connect).not.toHaveBeenCalled()
  })
})
