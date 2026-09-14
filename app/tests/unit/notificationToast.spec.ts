import { h, nextTick, ref, watch, type VNode } from 'vue'
import { beforeEach, describe, expect, it, vi } from 'vitest'
import { bannerDurationMs, useNotificationToast } from '~/composables/useNotificationToast'
import { de } from '~/i18n/messages'
import type { NotificationLine } from '~/api/types'

/**
 * A notification arriving while somebody is looking: what the banner says,
 * where it leads, and where it stays away.
 *
 * Nuxt UI's toaster is played by a list the test can read, kept the way the
 * real one keeps its toasts. Everything else — the words, the link, the screen
 * test — is the app's own.
 */

interface Entry {
  id: string
  open?: boolean
  title?: unknown
  [key: string]: unknown
}

function install(path = '/') {
  const toasts = ref<Entry[]>([])
  const add = vi.fn((toast: Entry) => {
    toasts.value = [...toasts.value.filter(entry => entry.id !== toast.id), { open: true, ...toast }]
  })
  const remove = vi.fn((id: string) => {
    toasts.value = toasts.value.map(entry => (entry.id === id ? { ...entry, open: false } : entry))
  })
  const currentRoute = ref({ path })
  const navigateTo = vi.fn()

  vi.stubGlobal('useToast', () => ({ toasts, add, remove }))
  vi.stubGlobal('useMessages', () => ref(de))
  vi.stubGlobal('useRouter', () => ({ currentRoute }))
  vi.stubGlobal('navigateTo', navigateTo)

  return { add, remove, currentRoute, navigateTo }
}

const jonas = {
  id: 'person-2',
  displayName: 'Jonas',
  handle: '@jonas',
  initials: 'J',
  avatarColor: '#4f46e5',
  avatarImageId: null,
  isOnline: true,
}

function message(overrides: Partial<NotificationLine> = {}): NotificationLine {
  return {
    id: null,
    kind: 'MessageReceived',
    actor: jonas,
    subject: null,
    excerpt: 'Kommst du heute mit laufen?',
    amount: null,
    target: 'Conversation',
    targetId: 'chat-1',
    occurredAt: '2026-06-17T09:00:00Z',
    isNew: true,
    ...overrides,
  }
}

/** The banner Nuxt UI was handed last. */
function lastBanner(add: ReturnType<typeof install>['add']): Entry {
  const call = add.mock.calls.at(-1)
  expect(call, 'a banner was shown').toBeDefined()
  return call![0]
}

/** The link in the banner's title, rendered the way Nuxt UI renders it. */
function link(entry: Entry): VNode {
  return (entry.title as () => VNode)()
}

/** A tap on the banner, as the browser would deliver it to the link. */
function tap(entry: Entry) {
  const event = { preventDefault: vi.fn() }
  const onClick = link(entry).props?.onClick as (event: unknown) => void

  onClick(event)
  return event
}

beforeEach(() => {
  vi.restoreAllMocks()
  vi.unstubAllGlobals()
  vi.stubGlobal('h', h)
  vi.stubGlobal('watch', watch)
})

describe('useNotificationToast', () => {
  it('says who wrote and what, and leads to the chat', () => {
    const { add } = install()

    useNotificationToast().announce(message())

    const banner = lastBanner(add)

    expect(link(banner).type).toBe('a')
    expect(link(banner).props).toMatchObject({ 'href': '/chats/chat-1', 'data-testid': 'notification-banner' })
    expect(link(banner).children).toBe('Jonas')
    expect(banner).toMatchObject({
      description: 'Kommst du heute mit laufen?',
      icon: 'i-lucide-message-circle',
      color: 'neutral',
      duration: bannerDurationMs,

      // One target, the way a banner on a phone is: tapping opens it.
      close: false,
    })
  })

  /**
   * Somebody's words are blocked in a replay, the way a chat bubble is: their
   * length alone is personal. A name and a goal title are masked.
   */
  it('keeps what it quotes out of a session replay', () => {
    const { add } = install()
    const banner = useNotificationToast()

    banner.announce(message())
    expect(lastBanner(add).ui).toMatchObject({
      title: 'sentry-mask',
      description: expect.stringContaining('sentry-block'),
    })

    banner.announce(message({ kind: 'ReactionReceived', target: 'Activity', targetId: 'activity-1', excerpt: null }))
    expect(lastBanner(add).ui).toMatchObject({
      title: 'sentry-mask',
      description: expect.stringContaining('sentry-mask'),
    })
  })

  it('says a kudos line the way the bell says it, and leads where the bell would', () => {
    const { add } = install()

    useNotificationToast().announce(message({
      kind: 'ReactionReceived',
      subject: 'Laufen',
      excerpt: null,
      target: 'Activity',
      targetId: 'activity-1',
    }))

    const banner = lastBanner(add)

    expect(link(banner).children).toBe(de.notify.titles.reaction)
    expect(link(banner).props?.href).toBe('/profile')
    expect(banner.description).toBe(`Jonas ${de.notify.gaveKudos(1, 'Laufen')}`)
  })

  it('shows nothing for the chat somebody is reading, or for the list of chats', () => {
    for (const path of ['/chats/chat-1', '/chats']) {
      const { add } = install(path)

      useNotificationToast().announce(message())

      expect(add).not.toHaveBeenCalled()
    }
  })

  it('opens what it is about within the app when tapped, and goes', () => {
    const { add, remove, navigateTo } = install()

    useNotificationToast().announce(message())
    const event = tap(lastBanner(add))

    expect(event.preventDefault).toHaveBeenCalled()
    expect(remove).toHaveBeenCalledWith('notification:MessageReceived:chat-1')
    expect(navigateTo).toHaveBeenCalledWith('/chats/chat-1')
  })

  /**
   * A swipe that dismisses a banner ends in a click on the same element. The
   * banner is already closing by then, and what was just waved away must not
   * open.
   */
  it('does not open what was just swiped away', () => {
    const { add, remove, navigateTo } = install()

    useNotificationToast().announce(message())
    const banner = lastBanner(add)

    remove(banner.id)
    const event = tap(banner)

    expect(event.preventDefault).toHaveBeenCalled()
    expect(navigateTo).not.toHaveBeenCalled()
  })

  it('replaces the banner for one chat rather than stacking a second', () => {
    const { add } = install()
    const banner = useNotificationToast()

    banner.announce(message())
    banner.announce(message({ excerpt: 'Oder morgen?' }))
    banner.announce(message({ targetId: 'chat-2' }))

    expect(add.mock.calls.map(([entry]) => entry.id)).toEqual([
      'notification:MessageReceived:chat-1',
      'notification:MessageReceived:chat-1',
      'notification:MessageReceived:chat-2',
    ])
  })

  it('goes as soon as its chat is opened some other way, and leaves the others', async () => {
    const { remove, currentRoute } = install()
    const banner = useNotificationToast()

    banner.announce(message())
    banner.announce(message({ targetId: 'chat-2' }))

    currentRoute.value = { path: '/chats/chat-1' }
    await nextTick()

    expect(remove).toHaveBeenCalledTimes(1)
    expect(remove).toHaveBeenCalledWith('notification:MessageReceived:chat-1')
  })
})
