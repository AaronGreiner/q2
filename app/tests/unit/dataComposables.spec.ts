import { computed, nextTick, ref, shallowRef, watch } from 'vue'
import { beforeEach, describe, expect, it, vi } from 'vitest'
import { de } from '~/i18n/messages'
import { useChatThread, useChats } from '~/composables/useChats'
import type { Image } from '~/api/types'
import { useFriends, usePersonProfile, usePersonSearch, useProfile } from '~/composables/useFriends'
import { useGoalArchive, useGoalLifecycle } from '~/composables/useGoalLifecycle'
import { useGoalDraft } from '~/composables/useGoalDraft'
import { useGoalDetail, useGoals } from '~/composables/useGoals'
import { useHome } from '~/composables/useHome'
import { usePendingProofs, useProofDelivery } from '~/composables/useProofs'

const failure = {
  kind: 'network' as const,
  isExpected: true,
  status: null,
  fieldErrors: {},
  traceId: null,
  errorId: null,
  reason: null,
}

function window(overrides: Record<string, unknown> = {}) {
  return {
    id: 'window-1',
    startsOn: '2026-07-31',
    dueOn: '2026-07-31',
    dueAt: '2026-07-31T21:59:59Z',
    requiredProofs: 1,
    confirmedProofs: 0,
    remainingProofs: 1,
    status: 'Open',
    ...overrides,
  }
}

function activity(overrides: Record<string, unknown> = {}) {
  return {
    id: 'activity-1',
    person: { id: 'person-1', displayName: 'Mara' },
    kind: 'Progress',
    subject: 'Run',
    amount: 1,
    occurredAt: '2026-07-31T09:00:00Z',
    kudosCount: 0,
    hasMyKudos: false,
    ...overrides,
  }
}

function goal(overrides: Record<string, unknown> = {}) {
  return {
    id: 'goal-1',
    title: 'Run',
    status: 'Active',
    schedule: { kind: 'Interval', everyDays: 1, weekdays: [], times: null, period: null },
    current: window(),
    streak: 2,
    windowsDone: 4,
    windowsMissed: 1,
    ...overrides,
  }
}

function friendsPayload() {
  return {
    friends: [{ person: { id: 'friend-1', displayName: 'Jonas' }, streak: 2, lastSeenAt: null }],
    requests: [{ person: { id: 'request-1', displayName: 'Emma' }, requestedAt: '2026-07-31T09:00:00Z' }],
    sentRequests: [],
    suggestions: [],
  }
}

function installReactiveGlobals() {
  vi.stubGlobal('ref', ref)
  vi.stubGlobal('computed', computed)
  vi.stubGlobal('watch', watch)
  vi.stubGlobal('shallowRef', shallowRef)
}

/** A small faithful stand-in for Nuxt's shallow async-data contract. */
function installAsyncData() {
  const refreshes: ReturnType<typeof vi.fn>[] = []

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

    refreshes.push(refresh)
    void refresh()
    return { data, status, refresh }
  })

  return refreshes
}

function installFeatureGlobals(api: object) {
  const report = vi.fn(() => failure)
  const show = vi.fn()
  const refreshNuxtData = vi.fn().mockResolvedValue(undefined)

  // Nuxt's hook bus, reduced to what a composable subscribes to — so a test
  // can play "these keys are being read again" the way refreshNuxtData does.
  const hooks = new Map<string, (keys?: string[]) => Promise<void> | void>()
  vi.stubGlobal('useNuxtApp', () => ({
    hook: (name: string, handler: (keys?: string[]) => Promise<void> | void) => {
      hooks.set(name, handler)
      return () => hooks.delete(name)
    },
  }))
  vi.stubGlobal('onScopeDispose', vi.fn())

  vi.stubGlobal('useQ2Api', () => api)
  vi.stubGlobal('useErrorReporter', () => ({ report }))
  vi.stubGlobal('useToastMessage', () => ({ show }))
  vi.stubGlobal('useMessages', () => ref(de))
  vi.stubGlobal('refreshNuxtData', refreshNuxtData)
  vi.stubGlobal('useSession', () => ({ session: ref(null), adopt: vi.fn() }))

  return { report, show, refreshNuxtData, hooks }
}

async function settle() {
  await vi.waitFor(() => expect(true).toBe(true))
  await nextTick()
}

beforeEach(() => {
  vi.restoreAllMocks()
  vi.unstubAllGlobals()
  installReactiveGlobals()
  installAsyncData()
})

describe('useHome', () => {
  function homeApi() {
    return {
      profile: { get: vi.fn().mockResolvedValue({ displayName: 'Mara' }) },
      goals: {
        list: vi.fn().mockResolvedValue([goal()]),
        today: vi.fn().mockResolvedValue([goal({ isMine: true })]),
      },
      activity: {
        feed: vi.fn().mockResolvedValue([activity()]),
        toggleKudos: vi.fn().mockResolvedValue(activity({ kudosCount: 1, hasMyKudos: true })),
      },
    }
  }

  /**
   * `/api/today` carries the goals friends asked you to check as well, for the
   * Dos tab. On the start screen they were rows of yours — and "Als Nächstes",
   * with a camera that could only fail (#78).
   */
  it('keeps the goals you only check off the start screen', async () => {
    const api = homeApi()
    api.goals.today.mockResolvedValue([
      goal({ id: 'own', isMine: true }),
      goal({ id: 'friends', isMine: false }),
    ])
    installFeatureGlobals(api)
    const home = useHome()

    await vi.waitFor(() => expect(home.profile.value).not.toBeNull())
    expect(home.due.value.map(entry => entry.id)).toEqual(['own'])
  })

  it('loads the complete dashboard and applies both actions', async () => {
    const api = homeApi()
    const { show } = installFeatureGlobals(api)
    const home = useHome()

    await vi.waitFor(() => expect(home.profile.value).toMatchObject({ displayName: 'Mara' }))
    expect(home.due.value).toHaveLength(1)
    expect(home.feed.value).toHaveLength(1)
    expect(home.error.value).toBeNull()
    expect(home.isLoading.value).toBe(false)

    await home.toggleKudos('activity-1')
    expect(home.feed.value[0]).toMatchObject({ kudosCount: 1, hasMyKudos: true })

    // The button already says it; a toast would only say it again.
    expect(show).not.toHaveBeenCalled()
  })

  it('turns load and action failures into safe failures', async () => {
    const api = homeApi()
    api.profile.get.mockRejectedValueOnce(new Error('offline'))
    const { report } = installFeatureGlobals(api)
    const home = useHome()

    await vi.waitFor(() => expect(home.error.value).toEqual(failure))
    expect(home.due.value).toEqual([])
    expect(report).toHaveBeenCalledWith(expect.any(Error), { feature: 'home', action: 'load' })

    api.activity.toggleKudos.mockRejectedValueOnce(new Error('kudos'))
    await home.toggleKudos('activity-1')

    expect(report).toHaveBeenCalledWith(expect.any(Error), { feature: 'feed', action: 'kudos' })
  })
})

describe('goal composables', () => {
  function goalsApi() {
    return {
      goals: {
        list: vi.fn().mockResolvedValue([goal()]),
        today: vi.fn().mockResolvedValue([goal()]),
        create: vi.fn().mockResolvedValue(goal({ id: 'goal-new' })),
        get: vi.fn().mockResolvedValue({ goal: goal(), team: [], history: [] }),
      },
    }
  }

  it('loads goals and creates one', async () => {
    const api = goalsApi()
    const { show } = installFeatureGlobals(api)
    const state = useGoals()
    await vi.waitFor(() => expect(state.goals.value).toHaveLength(1))

    expect(state.due.value).toHaveLength(1)

    const created = await state.create({ title: 'New goal' })
    expect(created).toMatchObject({ id: 'goal-new' })
    expect(state.isCreating.value).toBe(false)
    expect(state.createError.value).toBeNull()
    expect(show).not.toHaveBeenCalled()
  })

  it('keeps a create failure visible without changing product data', async () => {
    const api = goalsApi()
    const { report } = installFeatureGlobals(api)
    const state = useGoals()
    await vi.waitFor(() => expect(state.due.value).toHaveLength(1))

    api.goals.create.mockRejectedValueOnce(new Error('create'))
    await expect(state.create({ title: 'Nope' })).resolves.toBeNull()

    expect(state.createError.value).toEqual(failure)
    expect(report).toHaveBeenCalledWith(expect.any(Error), { feature: 'goals', action: 'create' })
  })

  it('distinguishes a missing detail from a real failure', async () => {
    const api = goalsApi()
    installFeatureGlobals(api)
    const id = ref('goal-1')
    const detail = useGoalDetail(id)
    await vi.waitFor(() => expect(detail.detail.value).not.toBeNull())

    vi.stubGlobal('useErrorReporter', () => ({
      report: vi.fn(() => ({ ...failure, kind: 'notFound' as const })),
    }))
    api.goals.get.mockRejectedValueOnce(new Error('missing'))
    const missing = useGoalDetail(ref('missing'))
    await vi.waitFor(() => expect(missing.isMissing.value).toBe(true))
    expect(missing.error.value).toBeNull()
  })
})

describe('the waiting Do', () => {
  const waiting = {
    id: 'draft-1',
    title: 'Lesen',
    icon: 'book-open',
    schedule: { kind: 'Interval', everyDays: 1, weekdays: [], times: null, period: null },
    reminderAt: null,
    createdAt: '2026-10-12T09:00:00Z',
  }

  function draftApi(state: { draft: unknown, hasFriends: boolean } = { draft: null, hasFriends: false }) {
    return {
      goals: {
        draft: vi.fn().mockResolvedValue(state),
        saveDraft: vi.fn().mockResolvedValue(waiting),
        discardDraft: vi.fn().mockResolvedValue(undefined),
      },
    }
  }

  it('reads whether there is one and whether there is anybody, and keeps one waiting', async () => {
    const api = draftApi()
    installFeatureGlobals(api)
    const state = useGoalDraft()
    await vi.waitFor(() => expect(state.hasFriends.value).toBe(false))

    expect(state.draft.value).toBeNull()

    const request = { title: 'Lesen', icon: 'book-open', schedule: { kind: 'Interval' as const, everyDays: 1 }, reminderAt: null }
    await expect(state.save(request)).resolves.toEqual(waiting)
    expect(api.goals.saveDraft).toHaveBeenCalledWith(request)
    expect(state.draft.value).toEqual(waiting)
    expect(state.isSaving.value).toBe(false)

    await expect(state.discard()).resolves.toBe(true)
    expect(state.draft.value).toBeNull()
    expect(state.hasFriends.value).toBe(false)
  })

  it('keeps a refused save visible, and a failed discard in place', async () => {
    const api = draftApi({ draft: waiting, hasFriends: false })
    const { report } = installFeatureGlobals(api)
    const state = useGoalDraft()
    await vi.waitFor(() => expect(state.draft.value).toEqual(waiting))

    api.goals.saveDraft.mockRejectedValueOnce(new Error('refused'))
    await expect(state.save({ title: 'Nope' })).resolves.toBeNull()
    expect(state.saveError.value).toEqual(failure)
    expect(report).toHaveBeenCalledWith(expect.any(Error), { feature: 'goals', action: 'saveDraft' })

    api.goals.discardDraft.mockRejectedValueOnce(new Error('offline'))
    await expect(state.discard()).resolves.toBe(false)
    expect(state.draft.value).toEqual(waiting)
  })

  it('assumes a friend when it cannot tell, so nobody is offered to wait by mistake', async () => {
    const api = draftApi()
    api.goals.draft.mockRejectedValueOnce(new Error('offline'))
    installFeatureGlobals(api)
    const state = useGoalDraft()
    await vi.waitFor(() => expect(state.error.value).toEqual(failure))

    expect(state.hasFriends.value).toBe(true)
    expect(state.draft.value).toBeNull()
  })
})

describe('the exits a goal has', () => {
  function lifecycleApi() {
    return {
      goals: {
        pause: vi.fn().mockResolvedValue(goal({ pause: { id: 'pause-1', vetoedByMe: false } })),
        endPause: vi.fn().mockResolvedValue(goal({ pause: null })),
        vetoPause: vi.fn().mockResolvedValue(goal({ pause: { id: 'pause-1', vetoedByMe: true } })),
        close: vi.fn().mockResolvedValue(goal({ status: 'Completed' })),
        archive: vi.fn().mockResolvedValue([goal({ status: 'Completed' })]),
        remove: vi.fn().mockResolvedValue(undefined),
      },
    }
  }

  it('sets a goal aside and lets it go again', async () => {
    const api = lifecycleApi()
    const { show } = installFeatureGlobals(api)
    const lifecycle = useGoalLifecycle()

    await lifecycle.pause('goal-1', 'Grippe, seit Freitag im Bett.', 3)

    expect(api.goals.pause).toHaveBeenCalledWith(
      'goal-1',
      { reason: 'Grippe, seit Freitag im Bett.', days: 3 },
    )
    expect(lifecycle.isBusy.value).toBe(false)
    expect(lifecycle.error.value).toBeNull()

    await expect(lifecycle.endPause('goal-1')).resolves.toMatchObject({ pause: null })

    // The goal's own screen shows the pause coming and going.
    expect(show).not.toHaveBeenCalled()
  })

  /**
   * The same request raises an objection and takes it back, so only the answer
   * knows which of the two just happened.
   */
  it('hands back the goal the objection left behind', async () => {
    const api = lifecycleApi()
    installFeatureGlobals(api)
    const lifecycle = useGoalLifecycle()

    await expect(lifecycle.toggleVeto('goal-1')).resolves.toMatchObject({ pause: { vetoedByMe: true } })

    api.goals.vetoPause.mockResolvedValueOnce(goal({ pause: { id: 'pause-1', vetoedByMe: false } }))
    await expect(lifecycle.toggleVeto('goal-1')).resolves.toMatchObject({ pause: { vetoedByMe: false } })
  })

  it('keeps a refused pause visible as a failure with its field messages', async () => {
    const api = lifecycleApi()
    const { report } = installFeatureGlobals(api)
    const lifecycle = useGoalLifecycle()

    api.goals.pause.mockRejectedValueOnce(new Error('no allowance left'))
    await expect(lifecycle.pause('goal-1', 'Grippe, seit Freitag im Bett.', 3)).resolves.toBeNull()

    expect(lifecycle.error.value).toEqual(failure)
    expect(report).toHaveBeenCalledWith(expect.any(Error), { feature: 'goals', action: 'pause' })
  })

  it('stops a goal with the ending that was chosen', async () => {
    const api = lifecycleApi()
    const { show } = installFeatureGlobals(api)
    const lifecycle = useGoalLifecycle()

    await lifecycle.close('goal-1', false)

    expect(api.goals.close).toHaveBeenCalledWith('goal-1', { completed: false })
    expect(show).not.toHaveBeenCalled()
  })

  it('loads the archive and deletes from it', async () => {
    const api = lifecycleApi()
    const { show } = installFeatureGlobals(api)
    const archive = useGoalArchive()

    await vi.waitFor(() => expect(archive.goals.value).toHaveLength(1))
    expect(archive.error.value).toBeNull()

    await expect(archive.remove('goal-1')).resolves.toBe(true)
    expect(api.goals.remove).toHaveBeenCalledWith('goal-1')
    expect(show).toHaveBeenCalledWith(de.toast.goalDeleted)
  })

  it('reports a refused deletion without emptying the list', async () => {
    const api = lifecycleApi()
    const { report } = installFeatureGlobals(api)
    const archive = useGoalArchive()

    await vi.waitFor(() => expect(archive.goals.value).toHaveLength(1))

    api.goals.remove.mockRejectedValueOnce(new Error('still running'))
    await expect(archive.remove('goal-1')).resolves.toBe(false)

    expect(archive.goals.value).toHaveLength(1)
    expect(report).toHaveBeenCalledWith(expect.any(Error), { feature: 'goals', action: 'delete' })
  })
})

describe('friend and profile composables', () => {
  function socialApi() {
    return {
      friends: {
        get: vi.fn().mockResolvedValue(friendsPayload()),
        accept: vi.fn().mockResolvedValue({}),
        decline: vi.fn().mockResolvedValue(undefined),
        request: vi.fn().mockResolvedValue({}),
        withdraw: vi.fn().mockResolvedValue(undefined),
        remove: vi.fn().mockResolvedValue(undefined),
        search: vi.fn().mockResolvedValue([{ person: { id: 'found-1', displayName: 'Lena' } }]),
      },
      profile: { get: vi.fn().mockResolvedValue({ displayName: 'Mara' }) },
    }
  }

  it('loads every friend state and runs every relationship action', async () => {
    const api = socialApi()
    const { show, refreshNuxtData } = installFeatureGlobals(api)
    const state = useFriends()
    await vi.waitFor(() => expect(state.friends.value).toHaveLength(1))

    expect(state.requests.value).toHaveLength(1)
    expect(state.sentRequests.value).toEqual([])
    expect(state.suggestions.value).toEqual([])
    expect(state.error.value).toBeNull()

    await state.accept('request-1')
    await state.decline('request-1')
    await state.request('friend-2')
    await state.withdraw('friend-2')

    // Every one of them moves a row on the screen, which says it well enough.
    expect(show).not.toHaveBeenCalled()
    expect(refreshNuxtData).toHaveBeenCalledWith('counts')
  })

  it('reports a failed relationship action', async () => {
    const api = socialApi()
    api.friends.withdraw.mockRejectedValueOnce(new Error('withdraw'))
    const { report } = installFeatureGlobals(api)
    const state = useFriends()
    await settle()

    await state.withdraw('friend-1')
    expect(report).toHaveBeenCalledWith(expect.any(Error), { feature: 'friends', action: 'withdraw' })
  })

  it('searches only meaningful terms and clears a failed search', async () => {
    const api = socialApi()
    const { report } = installFeatureGlobals(api)
    const query = ref(' x ')
    const search = usePersonSearch(query)

    await search.search()
    expect(search.isActive.value).toBe(false)
    expect(api.friends.search).not.toHaveBeenCalled()

    query.value = ' Lena '
    await nextTick()
    await vi.waitFor(() => expect(search.results.value).toHaveLength(1))
    expect(api.friends.search).toHaveBeenCalledWith('Lena')

    api.friends.search.mockRejectedValueOnce(new Error('search'))
    await search.search()
    expect(search.results.value).toEqual([])
    expect(search.error.value).toEqual(failure)
    expect(search.isSearching.value).toBe(false)
    expect(report).toHaveBeenCalledWith(expect.any(Error), { feature: 'friends', action: 'search' })
  })

  it('keeps the newest person-search answer when an older request finishes last', async () => {
    const api = socialApi()
    const first = Promise.withResolvers<Array<{ person: { id: string, displayName: string } }>>()
    const second = Promise.withResolvers<Array<{ person: { id: string, displayName: string } }>>()
    api.friends.search
      .mockImplementationOnce(() => first.promise)
      .mockImplementationOnce(() => second.promise)

    // Drive the two requests explicitly so the test controls their completion
    // order rather than Vue's watcher scheduler.
    vi.stubGlobal('watch', vi.fn())
    installFeatureGlobals(api)
    const query = ref('Le')
    const search = usePersonSearch(query)

    const older = search.search()
    query.value = 'Lena'
    const newer = search.search()

    second.resolve([{ person: { id: 'newer', displayName: 'Lena' } }])
    await newer
    first.resolve([{ person: { id: 'older', displayName: 'Leo' } }])
    await older

    expect(search.results.value.map(result => result.person.displayName)).toEqual(['Lena'])
    expect(search.isSearching.value).toBe(false)
  })

  it('loads a profile and exposes a safe profile failure', async () => {
    const api = socialApi()
    installFeatureGlobals(api)
    const profile = useProfile()
    await vi.waitFor(() => expect(profile.profile.value).toMatchObject({ displayName: 'Mara' }))
    expect(profile.isLoading.value).toBe(false)

    api.profile.get.mockRejectedValueOnce(new Error('profile'))
    const failed = useProfile()
    await vi.waitFor(() => expect(failed.error.value).toEqual(failure))
    expect(failed.profile.value).toBeNull()
  })
})

describe('chat composables', () => {
  function chat(overrides: Record<string, unknown> = {}) {
    return { id: 'chat-1', title: 'Jonas', messages: [], events: [], olderCursor: null, isGroup: true, ...overrides }
  }

  function line(id: string, overrides: Record<string, unknown> = {}) {
    return { id, text: id, reactions: [], ...overrides }
  }

  function chatsApi() {
    return {
      chats: {
        list: vi.fn().mockResolvedValue([chat()]),
        get: vi.fn().mockResolvedValue(chat()),
        send: vi.fn().mockResolvedValue(chat({ messages: [{ id: 'message-1', text: 'Hello' }] })),
        react: vi.fn().mockResolvedValue(line('message-1', { reactions: ['👏'] })),
        older: vi.fn().mockResolvedValue({ messages: [], events: [], olderCursor: null }),
        mute: vi.fn().mockResolvedValue(chat({ isMuted: true })),
        leave: vi.fn().mockResolvedValue(undefined),
      },
      proofs: {
        vote: vi.fn().mockResolvedValue({ id: 'proof-1' }),
      },
      images: {
        remove: vi.fn().mockResolvedValue(undefined),
      },
    }
  }

  it('sends an uploaded photograph, and deletes it again when the message fails', async () => {
    const api = chatsApi()
    vi.stubGlobal('useGoBack', () => vi.fn())
    const { report } = installFeatureGlobals(api)
    const state = useChatThread(ref('chat-1'))
    await vi.waitFor(() => expect(state.chat.value).not.toBeNull())
    const image = { id: 'image-1', purpose: 'ChatPhoto', width: 800, height: 600 } as Image

    await expect(state.sendPhoto(image)).resolves.toBe(true)
    expect(api.chats.send).toHaveBeenCalledWith('chat-1', { imageId: 'image-1' })
    expect(api.images.remove).not.toHaveBeenCalled()
    expect(state.isSending.value).toBe(false)

    // Nobody was sent it, so nothing should be left on the server either.
    api.chats.send.mockRejectedValueOnce(new Error('send'))
    await expect(state.sendPhoto(image)).resolves.toBe(false)
    expect(report).toHaveBeenCalledWith(expect.any(Error), { feature: 'chats', action: 'send-photo' })
    expect(api.images.remove).toHaveBeenCalledWith('image-1')

    // A tidy-up that fails as well is not a second problem to show anybody.
    api.chats.send.mockRejectedValueOnce(new Error('send'))
    api.images.remove.mockRejectedValueOnce(new Error('remove'))
    await expect(state.sendPhoto(image)).resolves.toBe(false)
  })

  it('votes from a goal\'s thread and reads it again, with the vote screen and the badges', async () => {
    const api = chatsApi()
    api.chats.get.mockResolvedValue(chat({ pinnedGoal: { id: 'goal-1' }, events: [] }))
    vi.stubGlobal('useGoBack', () => vi.fn())
    const { refreshNuxtData, report } = installFeatureGlobals(api)
    const state = useChatThread(ref('chat-1'))
    await vi.waitFor(() => expect(state.chat.value).not.toBeNull())
    const reads = api.chats.get.mock.calls.length

    await state.vote('proof-1', 'Confirm')

    expect(api.proofs.vote).toHaveBeenCalledWith('proof-1', 'Confirm')
    expect(api.chats.get.mock.calls.length).toBe(reads + 1)
    expect(refreshNuxtData).toHaveBeenCalledWith(['counts', 'proofs-pending'])
    expect(state.isVoting.value).toBe(false)

    // A vote that no longer fits is reported, and the thread is read again
    // rather than left offering buttons that cannot work.
    api.proofs.vote.mockRejectedValueOnce(new Error('closed'))
    await state.vote('proof-1', 'Doubt')

    expect(report).toHaveBeenCalledWith(expect.any(Error), { feature: 'chats', action: 'vote' })
    expect(api.chats.get.mock.calls.length).toBe(reads + 2)
  })

  it('reads a goal\'s thread again when its goal changes, and not for another goal', async () => {
    const api = chatsApi()
    api.chats.get.mockResolvedValue(chat({ pinnedGoal: { id: 'goal-1' }, events: [] }))
    vi.stubGlobal('useGoBack', () => vi.fn())
    const { hooks } = installFeatureGlobals(api)
    const state = useChatThread(ref('chat-1'))
    await vi.waitFor(() => expect(state.chat.value).not.toBeNull())
    const reads = api.chats.get.mock.calls.length
    const onRefresh = hooks.get('app:data:refresh')!

    await onRefresh(['goals', 'goal:goal-2'])
    expect(api.chats.get.mock.calls.length).toBe(reads)

    await onRefresh(['proofs-pending', 'chats', 'goal:goal-1'])
    expect(api.chats.get.mock.calls.length).toBe(reads + 1)
  })

  it('reads further back a page at a time and keeps it while the newest page moves on', async () => {
    const api = chatsApi()
    api.chats.get.mockResolvedValue(chat({ messages: [line('m3'), line('m4')], olderCursor: 'before-m3' }))
    api.chats.older
      .mockResolvedValueOnce({ messages: [line('m2')], events: [], olderCursor: 'before-m2' })
      .mockResolvedValueOnce({ messages: [line('m1')], events: [], olderCursor: null })
    vi.stubGlobal('useGoBack', () => vi.fn())
    installFeatureGlobals(api)
    const state = useChatThread(ref('chat-1'))
    await vi.waitFor(() => expect(state.chat.value).not.toBeNull())

    expect(state.hasOlder.value).toBe(true)
    await expect(state.loadOlder()).resolves.toBe(true)
    expect(api.chats.older).toHaveBeenLastCalledWith('chat-1', 'before-m3')
    expect(state.messages.value.map(message => message.id)).toEqual(['m2', 'm3', 'm4'])

    // A reply moves the newest page on by one; the line it pushed out stays.
    api.chats.send.mockResolvedValueOnce(chat({ messages: [line('m4'), line('m5')], olderCursor: 'before-m4' }))
    await state.send('m5')
    expect(state.messages.value.map(message => message.id)).toEqual(['m2', 'm3', 'm4', 'm5'])

    // And the next page is still the one before the oldest line loaded.
    await expect(state.loadOlder()).resolves.toBe(true)
    expect(api.chats.older).toHaveBeenLastCalledWith('chat-1', 'before-m2')
    expect(state.messages.value.map(message => message.id)).toEqual(['m1', 'm2', 'm3', 'm4', 'm5'])
    expect(state.hasOlder.value).toBe(false)
    await expect(state.loadOlder()).resolves.toBe(false)
  })

  it('patches a reaction and a vote into a line pages back', async () => {
    const api = chatsApi()
    const proof = { id: 'proof-1', votes: { myVote: null } }
    api.chats.get.mockResolvedValue(chat({ messages: [line('m2')], pinnedGoal: { id: 'goal-1' }, olderCursor: 'before-m2' }))
    api.chats.older.mockResolvedValueOnce({
      messages: [line('m1')],
      events: [{ key: 'ProofDelivered-1', kind: 'ProofDelivered', proof }],
      olderCursor: null,
    })
    api.chats.react.mockResolvedValueOnce(line('m1', { reactions: [{ kind: 'Fire', count: 1, isMine: true }] }))
    api.proofs.vote.mockResolvedValueOnce({ ...proof, votes: { myVote: 'Confirm' } })
    vi.stubGlobal('useGoBack', () => vi.fn())
    installFeatureGlobals(api)
    const state = useChatThread(ref('chat-1'))
    await vi.waitFor(() => expect(state.chat.value).not.toBeNull())
    await state.loadOlder()

    await state.react('m1', 'Fire')
    expect(state.messages.value[0]).toMatchObject({ id: 'm1', reactions: [{ kind: 'Fire', isMine: true }] })

    await state.vote('proof-1', 'Confirm')
    expect(state.events.value[0]?.proof).toMatchObject({ votes: { myVote: 'Confirm' } })
  })

  it('reports a page that did not arrive and leaves what is loaded', async () => {
    const api = chatsApi()
    api.chats.get.mockResolvedValue(chat({ messages: [line('m2')], olderCursor: 'before-m2' }))
    api.chats.older.mockRejectedValueOnce(new Error('offline'))
    vi.stubGlobal('useGoBack', () => vi.fn())
    const { report } = installFeatureGlobals(api)
    const state = useChatThread(ref('chat-1'))
    await vi.waitFor(() => expect(state.chat.value).not.toBeNull())

    await expect(state.loadOlder()).resolves.toBe(false)
    expect(report).toHaveBeenCalledWith(expect.any(Error), { feature: 'chats', action: 'older' })
    expect(state.olderFailed.value).toBe(true)
    expect(state.isLoadingOlder.value).toBe(false)
    expect(state.messages.value.map(message => message.id)).toEqual(['m2'])

    // Asked again, it goes through.
    await expect(state.loadOlder()).resolves.toBe(true)
    expect(state.olderFailed.value).toBe(false)
  })

  it('loads and filters the conversation list', async () => {
    const api = chatsApi()
    installFeatureGlobals(api)
    const search = ref('Jonas')
    const state = useChats(search)

    await vi.waitFor(() => expect(state.chats.value).toHaveLength(1))
    expect(api.chats.list).toHaveBeenCalledWith({ search: 'Jonas' })
    expect(state.error.value).toBeNull()
    expect(state.isLoading.value).toBe(false)
  })

  it('sends, cheers, reacts and leaves from one authoritative thread', async () => {
    const api = chatsApi()
    const goBack = vi.fn()
    vi.stubGlobal('useGoBack', () => goBack)
    const { show, refreshNuxtData } = installFeatureGlobals(api)
    const state = useChatThread(ref('chat-1'))
    await vi.waitFor(() => expect(state.chat.value).not.toBeNull())

    await expect(state.send('   ')).resolves.toBe(false)
    await expect(state.send(' Hello ')).resolves.toBe(true)
    expect(api.chats.send).toHaveBeenCalledWith('chat-1', { text: 'Hello' })
    expect(state.isSending.value).toBe(false)

    await state.cheer()
    expect(api.chats.send).toHaveBeenCalledWith('chat-1', { text: de.chats.cheerText })

    await state.react('message-1', '👏')
    expect(api.chats.react).toHaveBeenCalledWith('chat-1', 'message-1', '👏')

    await state.setMuted(true)
    expect(api.chats.mute).toHaveBeenCalledWith('chat-1', true)
    expect(state.chat.value).toMatchObject({ isMuted: true })

    await state.leave()
    expect(refreshNuxtData).toHaveBeenCalledWith('chats')
    expect(refreshNuxtData).toHaveBeenCalledWith('counts')
    // Back to where the thread was opened from, with the list as the fallback.
    expect(goBack).toHaveBeenCalledWith('/chats')

    // The one of these that leaves nothing on screen to show it worked.
    expect(show).toHaveBeenCalledTimes(1)
    expect(show).toHaveBeenCalledWith(de.toast.groupLeft)
  })

  it('reports failed thread actions and distinguishes missing threads', async () => {
    const api = chatsApi()
    vi.stubGlobal('useGoBack', () => vi.fn())
    const { report } = installFeatureGlobals(api)
    const state = useChatThread(ref('chat-1'))
    await vi.waitFor(() => expect(state.chat.value).not.toBeNull())

    api.chats.send.mockRejectedValueOnce(new Error('send'))
    api.chats.react.mockRejectedValueOnce(new Error('react'))
    api.chats.leave.mockRejectedValueOnce(new Error('leave'))
    api.chats.mute.mockRejectedValueOnce(new Error('mute'))
    await expect(state.send('Hello')).resolves.toBe(false)
    await state.react('message-1', '👏')
    await state.setMuted(true)
    await state.leave()
    expect(report).toHaveBeenCalledWith(expect.any(Error), { feature: 'chats', action: 'send' })
    expect(report).toHaveBeenCalledWith(expect.any(Error), { feature: 'chats', action: 'react' })
    expect(report).toHaveBeenCalledWith(expect.any(Error), { feature: 'chats', action: 'mute' })
    expect(report).toHaveBeenCalledWith(expect.any(Error), { feature: 'chats', action: 'leave' })

    vi.stubGlobal('useErrorReporter', () => ({
      report: vi.fn(() => ({ ...failure, kind: 'notFound' as const })),
    }))
    api.chats.get.mockRejectedValueOnce(new Error('missing'))
    const missing = useChatThread(ref('missing'))
    await vi.waitFor(() => expect(missing.isMissing.value).toBe(true))
    expect(missing.error.value).toBeNull()
  })
})

/**
 * The two composables stage 4 added.
 *
 * The interesting behaviour is not "it calls the endpoint" — it is what happens
 * to the queue afterwards. A card that stayed after a vote would be voted on
 * twice; a card that vanished on a failure would be an abstention nobody chose.
 */
describe('proof composables', () => {
  function card(id: string) {
    return { proof: { id }, goalTitle: 'Laufen', goalIcon: 'medal' }
  }

  function proofsApi() {
    return {
      proofs: {
        pending: vi.fn().mockResolvedValue([card('proof-1'), card('proof-2')]),
        vote: vi.fn().mockResolvedValue({ id: 'proof-1' }),
        submit: vi.fn().mockResolvedValue({ proof: { id: 'proof-9', status: 'Voting' }, conversationId: 'chat-9' }),
      },
    }
  }

  it('drops a card as soon as it has been voted on', async () => {
    const api = proofsApi()
    const { show } = installFeatureGlobals(api)
    const state = usePendingProofs()

    await vi.waitFor(() => expect(state.proofs.value).toHaveLength(2))

    await state.vote('proof-1', 'Doubt')

    expect(api.proofs.vote).toHaveBeenCalledWith('proof-1', 'Doubt')
    expect(state.proofs.value.map(entry => entry.proof.id)).toEqual(['proof-2'])
    expect(show).not.toHaveBeenCalled()
  })

  it('reloads rather than dropping a card when the vote failed', async () => {
    const api = proofsApi()
    const { report } = installFeatureGlobals(api)
    const state = usePendingProofs()

    await vi.waitFor(() => expect(state.proofs.value).toHaveLength(2))

    api.proofs.vote.mockRejectedValueOnce(new Error('closed'))
    await state.vote('proof-1', 'Confirm')

    expect(report).toHaveBeenCalledWith(expect.any(Error), { feature: 'proofs', action: 'vote' })

    // Something is out of step — the vote closed, or somebody got there first.
    // Silently dropping the card would be an abstention nobody chose.
    expect(state.proofs.value).toHaveLength(2)
  })

  it('hands a delivered photograph to the goal and follows it to its conversation', async () => {
    const api = proofsApi()
    const { show } = installFeatureGlobals(api)
    const navigateTo = vi.fn().mockResolvedValue(undefined)
    vi.stubGlobal('useRoute', () => ({ path: '/goals' }))
    vi.stubGlobal('navigateTo', navigateTo)
    const delivery = useProofDelivery()

    await expect(delivery.deliver('goal-1', { id: 'image-1' }, 'camera')).resolves.toEqual({ status: 'moved' })
    expect(api.proofs.submit).toHaveBeenCalledWith('goal-1', 'image-1', 'Live')
    expect(navigateTo).toHaveBeenCalledWith('/chats/chat-9')

    // A goal nobody shares and with no conversation has nobody to ask, so it
    // comes back believed — and a toast is the one place left to say so.
    api.proofs.submit.mockResolvedValueOnce({ proof: { id: 'proof-10', status: 'Confirmed' }, conversationId: null })
    await expect(delivery.deliver('goal-1', { id: 'image-2' }, 'library')).resolves.toEqual({ status: 'stayed' })
    expect(api.proofs.submit).toHaveBeenLastCalledWith('goal-1', 'image-2', 'Screenshot')
    expect(show).toHaveBeenCalledWith(de.toast.proofCounted)
  })

  it('keeps a refused delivery visible as a failure', async () => {
    const api = proofsApi()
    const { report } = installFeatureGlobals(api)
    vi.stubGlobal('useRoute', () => ({ path: '/goals' }))
    vi.stubGlobal('navigateTo', vi.fn())
    const delivery = useProofDelivery()

    api.proofs.submit.mockRejectedValueOnce(new Error('window full'))

    const result = await delivery.deliver('goal-1', { id: 'image-1' }, 'camera')
    expect(result.status).toBe('refused')
    expect(report).toHaveBeenCalledWith(expect.any(Error), { feature: 'proofs', action: 'deliver' })
  })
})

describe('somebody else\'s profile', () => {
  it('asks for the person and keeps a stale link calm', async () => {
    const api = {
      profile: {
        person: vi.fn().mockResolvedValue({
          person: { id: 'person-2', displayName: 'Lena' },
          balance: { done: 3, missed: 1 },
          sharedGoals: [{ id: 'goal-1', title: 'Laufen', icon: 'flame', status: 'Active' }],
        }),
      },
    }

    installFeatureGlobals(api)
    const id = ref('person-2')
    const state = usePersonProfile(id)

    await vi.waitFor(() => expect(state.person.value).not.toBeNull())

    expect(api.profile.person).toHaveBeenCalledWith('person-2')

    // Nothing here narrows the balance. The scoping is the server's, and a
    // screen that could filter is a screen that could stop filtering.
    expect(state.person.value).toMatchObject({ sharedGoals: [{ id: 'goal-1' }], balance: { done: 3, missed: 1 } })
    expect(state.isMissing.value).toBe(false)
  })

  it('ends a friendship from the profile and reads the lists again', async () => {
    const api = {
      profile: { person: vi.fn().mockResolvedValue({ person: { id: 'person-2' }, state: 'Friends' }) },
      friends: { remove: vi.fn().mockResolvedValue(undefined) },
    }

    const { refreshNuxtData } = installFeatureGlobals(api)
    const state = usePersonProfile(ref('person-2'))
    await vi.waitFor(() => expect(state.person.value).not.toBeNull())

    await expect(state.removeFriend()).resolves.toBe(true)
    expect(api.friends.remove).toHaveBeenCalledWith('person-2')
    expect(refreshNuxtData).toHaveBeenCalledWith(['friends', 'counts'])
    expect(api.profile.person).toHaveBeenCalledTimes(2)
  })

  it('reports a friendship that could not be ended', async () => {
    const api = {
      profile: { person: vi.fn().mockResolvedValue({ person: { id: 'person-2' }, state: 'Friends' }) },
      friends: { remove: vi.fn().mockRejectedValue(new Error('remove')) },
    }

    const { report } = installFeatureGlobals(api)
    const state = usePersonProfile(ref('person-2'))
    await vi.waitFor(() => expect(state.person.value).not.toBeNull())

    await expect(state.removeFriend()).resolves.toBe(false)
    expect(report).toHaveBeenCalledWith(expect.any(Error), { feature: 'friends', action: 'remove' })
    expect(state.isChanging.value).toBe(false)
  })

  /**
   * Every answer about a person can be given from their profile (#86), and
   * each one reads the profile again, because its `state` is what the screen
   * switches on — and the friends list and the tab's count with it.
   */
  it.each([
    ['request', 'request'],
    ['withdraw', 'withdraw'],
    ['accept', 'accept'],
    ['decline', 'decline'],
  ] as const)('can %s from the profile and reads it again', async (action, call) => {
    const api = {
      profile: { person: vi.fn().mockResolvedValue({ person: { id: 'person-2' }, state: 'None' }) },
      friends: { [call]: vi.fn().mockResolvedValue(undefined) },
    }

    const { refreshNuxtData } = installFeatureGlobals(api)
    const state = usePersonProfile(ref('person-2'))
    await vi.waitFor(() => expect(state.person.value).not.toBeNull())

    await expect(state[action]()).resolves.toBe(true)
    expect(api.friends[call]).toHaveBeenCalledWith('person-2')
    expect(refreshNuxtData).toHaveBeenCalledWith(['friends', 'counts'])
    expect(api.profile.person).toHaveBeenCalledTimes(2)
  })

  it('answers one change at a time', async () => {
    let finish: () => void = () => {}
    const api = {
      profile: { person: vi.fn().mockResolvedValue({ person: { id: 'person-2' }, state: 'RequestReceived' }) },
      friends: {
        accept: vi.fn(() => new Promise<void>((resolve) => {
          finish = resolve
        })),
        decline: vi.fn().mockResolvedValue(undefined),
      },
    }

    installFeatureGlobals(api)
    const state = usePersonProfile(ref('person-2'))
    await vi.waitFor(() => expect(state.person.value).not.toBeNull())

    const accepting = state.accept()
    expect(state.isChanging.value).toBe(true)

    // A second tap while the first is on its way is not a second answer.
    await expect(state.decline()).resolves.toBe(false)
    expect(api.friends.decline).not.toHaveBeenCalled()

    finish()
    await expect(accepting).resolves.toBe(true)
    expect(state.isChanging.value).toBe(false)
  })

  it('treats a person who is gone as a calm state rather than an error', async () => {
    const api = { profile: { person: vi.fn().mockRejectedValue(new Error('gone')) } }

    installFeatureGlobals(api)

    // After the globals, not before: `installFeatureGlobals` installs its own
    // reporter and would overwrite this one.
    vi.stubGlobal('useErrorReporter', () => ({
      report: vi.fn(() => ({ ...failure, kind: 'notFound' as const })),
    }))

    const state = usePersonProfile(ref('nobody'))

    await vi.waitFor(() => expect(state.isMissing.value).toBe(true))
    expect(state.error.value).toBeNull()
  })
})
