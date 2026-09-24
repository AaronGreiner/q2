import { computed, ref, watch } from 'vue'
import { beforeEach, describe, expect, it, vi } from 'vitest'
import { noCounts, useCounts } from '~/composables/useCounts'
import { useNotifications } from '~/composables/useNotifications'
import type { Counts, NotificationLine } from '~/api/types'

const failure = {
  kind: 'network' as const,
  isExpected: true,
  status: null,
  fieldErrors: {},
  traceId: null,
  errorId: null,
  reason: null,
}

const someCounts: Counts = { unreadChats: 2, pendingFriendRequests: 1, unseenNotifications: 3, proofsAwaitingVote: 1 }

function line(overrides: Partial<NotificationLine> = {}): NotificationLine {
  return {
    id: 'line-1',
    kind: 'FriendshipStarted',
    actor: null,
    subject: null,
    excerpt: null,
    amount: null,
    target: 'Person',
    targetId: null,
    occurredAt: '2026-07-31T09:00:00Z',
    isNew: true,
    ...overrides,
  }
}

/** A small faithful stand-in for Nuxt's async-data contract: pending, then the answer. */
function installAsyncData() {
  vi.stubGlobal('useAsyncData', (
    _key: unknown,
    handler: () => Promise<unknown>,
    options: { default?: () => unknown } = {},
  ) => {
    const data = ref(options.default?.())
    const status = ref('pending')
    const refresh = vi.fn(async () => {
      status.value = 'pending'
      data.value = await handler()
      status.value = 'success'
      return data.value
    })

    void refresh()
    return { data, status, refresh }
  })
}

/** The API, the reporter, and the shared counts the layout would already hold. */
function install(notifications: object, counts: Counts | null = { ...someCounts }) {
  const report = vi.fn(() => failure)
  const shared = ref<Counts | null>(counts)

  vi.stubGlobal('useQ2Api', () => ({ notifications }))
  vi.stubGlobal('useErrorReporter', () => ({ report }))
  vi.stubGlobal('useNuxtData', () => ({ data: shared }))

  return { report, shared }
}

beforeEach(() => {
  vi.restoreAllMocks()
  vi.unstubAllGlobals()
  vi.stubGlobal('ref', ref)
  vi.stubGlobal('computed', computed)
  vi.stubGlobal('watch', watch)
  installAsyncData()
})

describe('useCounts', () => {
  it('reads every badge in one go', async () => {
    install({ counts: vi.fn().mockResolvedValue(someCounts) })
    const { counts } = useCounts()

    await vi.waitFor(() => expect(counts.value).toEqual(someCounts))
  })

  it('shows no badges rather than an error when the numbers cannot be read', async () => {
    const { report } = install({ counts: vi.fn().mockRejectedValue(new Error('offline')) })
    const { counts } = useCounts()

    await vi.waitFor(() => expect(report).toHaveBeenCalledWith(expect.any(Error), {
      feature: 'notifications',
      action: 'counts',
    }))
    expect(counts.value).toEqual(noCounts)
  })
})

describe('useNotifications', () => {
  it('puts what is new above what was seen before', async () => {
    install({ list: vi.fn().mockResolvedValue([line(), line({ id: 'line-2', isNew: false })]) })
    const bell = useNotifications()

    await vi.waitFor(() => expect(bell.isLoading.value).toBe(false))
    expect(bell.fresh.value.map(entry => entry.id)).toEqual(['line-1'])
    expect(bell.earlier.value.map(entry => entry.id)).toEqual(['line-2'])
    expect(bell.isEmpty.value).toBe(false)
  })

  it('clears the bell\'s own number once it has been read, and no other', async () => {
    const { shared } = install({ list: vi.fn().mockResolvedValue([line()]) })
    const bell = useNotifications()

    await vi.waitFor(() => expect(bell.isLoading.value).toBe(false))
    expect(shared.value).toEqual({ ...someCounts, unseenNotifications: 0 })
  })

  it('leaves the number alone while the read is still on its way', () => {
    const { shared } = install({ list: vi.fn(() => new Promise(() => {})) })
    useNotifications()

    expect(shared.value?.unseenNotifications).toBe(3)
  })

  it('leaves the number alone when the read fails, since nothing was seen', async () => {
    const { shared, report } = install({ list: vi.fn().mockRejectedValue(new Error('offline')) })
    const bell = useNotifications()

    await vi.waitFor(() => expect(bell.error.value).toEqual(failure))
    expect(report).toHaveBeenCalledWith(expect.any(Error), { feature: 'notifications', action: 'list' })
    expect(shared.value?.unseenNotifications).toBe(3)
  })

  it('keeps its lines on screen while it reads again, as a live refresh does', async () => {
    install({ list: vi.fn().mockResolvedValue([line()]) })
    const bell = useNotifications()
    await vi.waitFor(() => expect(bell.isLoading.value).toBe(false))

    const again = bell.refresh()

    expect(bell.isLoading.value).toBe(false)
    expect(bell.fresh.value).toHaveLength(1)
    await again
  })

  it('keeps what it showed as new while it is open, though a later read says seen', async () => {
    install({
      list: vi.fn()
        .mockResolvedValueOnce([line()])
        .mockResolvedValueOnce([line({ isNew: false }), line({ id: 'line-2', isNew: false })]),
    })
    const bell = useNotifications()
    await vi.waitFor(() => expect(bell.fresh.value).toHaveLength(1))

    // The live connection catching up reads again, after this screen's own
    // read has already moved the marker past everything.
    await bell.refresh()

    expect(bell.fresh.value.map(entry => entry.id)).toEqual(['line-1'])
    expect(bell.earlier.value.map(entry => entry.id)).toEqual(['line-2'])
  })

  it('knows when there is nothing at all', async () => {
    install({ list: vi.fn().mockResolvedValue([]) })
    const bell = useNotifications()

    await vi.waitFor(() => expect(bell.isLoading.value).toBe(false))
    expect(bell.isEmpty.value).toBe(true)
  })

  it('takes a dismissed line off the screen before the server has answered', async () => {
    const dismiss = vi.fn(() => new Promise<void>(() => {}))
    install({ list: vi.fn().mockResolvedValue([line(), line({ id: 'line-2', isNew: false })]), dismiss })
    const bell = useNotifications()
    await vi.waitFor(() => expect(bell.isLoading.value).toBe(false))

    void bell.dismiss(line())

    expect(dismiss).toHaveBeenCalledWith('line-1')
    expect(bell.fresh.value).toHaveLength(0)
    expect(bell.earlier.value.map(entry => entry.id)).toEqual(['line-2'])
  })

  it('reads the bell again when a dismissal is refused, so the line comes back', async () => {
    const list = vi.fn().mockResolvedValue([line()])
    const { report } = install({ list, dismiss: vi.fn().mockRejectedValue(new Error('offline')) })
    const bell = useNotifications()
    await vi.waitFor(() => expect(bell.isLoading.value).toBe(false))

    await bell.dismiss(line())

    expect(report).toHaveBeenCalledWith(expect.any(Error), { feature: 'notifications', action: 'dismiss' })
    expect(list).toHaveBeenCalledTimes(2)
    expect(bell.fresh.value).toHaveLength(1)
  })

  it('clears up to the newest line shown rather than up to now', async () => {
    const clear = vi.fn().mockResolvedValue(undefined)
    install({
      list: vi.fn().mockResolvedValue([
        line({ occurredAt: '2026-07-31T10:00:00Z' }),
        line({ id: 'line-2', isNew: false, occurredAt: '2026-07-30T09:00:00Z' }),
      ]),
      clear,
    })
    const bell = useNotifications()
    await vi.waitFor(() => expect(bell.isLoading.value).toBe(false))

    await bell.clearAll()

    expect(clear).toHaveBeenCalledWith('2026-07-31T10:00:00Z')
    expect(bell.isEmpty.value).toBe(true)
  })

  it('asks nothing of the server when there is nothing to clear', async () => {
    const clear = vi.fn()
    install({ list: vi.fn().mockResolvedValue([]), clear })
    const bell = useNotifications()
    await vi.waitFor(() => expect(bell.isLoading.value).toBe(false))

    await bell.clearAll()

    expect(clear).not.toHaveBeenCalled()
  })
})
