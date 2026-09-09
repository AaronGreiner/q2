import { computed, nextTick, ref, watch } from 'vue'
import { beforeEach, describe, expect, it, vi } from 'vitest'
import { de } from '~/i18n/messages'
import { useChatThread, useChats } from '~/composables/useChats'
import { useFriends, usePersonProfile, usePersonSearch, useProfile } from '~/composables/useFriends'
import { useGoalArchive, useGoalLifecycle } from '~/composables/useGoalLifecycle'
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

  vi.stubGlobal('useQ2Api', () => api)
  vi.stubGlobal('useErrorReporter', () => ({ report }))
  vi.stubGlobal('useToastMessage', () => ({ show }))
  vi.stubGlobal('useMessages', () => ref(de))
  vi.stubGlobal('refreshNuxtData', refreshNuxtData)

  return { report, show, refreshNuxtData }
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
        today: vi.fn().mockResolvedValue([goal()]),
      },
      activity: {
        feed: vi.fn().mockResolvedValue([activity()]),
        toggleKudos: vi.fn().mockResolvedValue(activity({ kudosCount: 1, hasMyKudos: true })),
      },
    }
  }

  it('loads the complete dashboard and applies both actions', async () => {
    const api = homeApi()
    const { show } = installFeatureGlobals(api)
    const home = useHome()

    await vi.waitFor(() => expect(home.profile.value).toMatchObject({ displayName: 'Mara' }))
    expect(home.due.value).toHaveLength(1)
    expect(home.goals.value).toHaveLength(1)
    expect(home.feed.value).toHaveLength(1)
    expect(home.error.value).toBeNull()
    expect(home.isLoading.value).toBe(false)

    await home.toggleKudos('activity-1')
    expect(home.feed.value[0]).toMatchObject({ kudosCount: 1, hasMyKudos: true })
    expect(show).toHaveBeenCalledWith(de.toast.kudosSent)
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
    expect(show).toHaveBeenCalledWith(de.toast.goalCreated)
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
    expect(show).toHaveBeenCalledWith(de.toast.goalPaused)
    expect(lifecycle.isBusy.value).toBe(false)
    expect(lifecycle.error.value).toBeNull()

    await lifecycle.endPause('goal-1')
    expect(show).toHaveBeenCalledWith(de.toast.pauseEnded)
  })

  /**
   * The same request raises an objection and takes it back, so only the answer
   * knows which of the two just happened.
   */
  it('reads which way an objection went from the response', async () => {
    const api = lifecycleApi()
    const { show } = installFeatureGlobals(api)
    const lifecycle = useGoalLifecycle()

    await lifecycle.toggleVeto('goal-1')
    expect(show).toHaveBeenCalledWith(de.toast.pauseVetoed)

    api.goals.vetoPause.mockResolvedValueOnce(goal({ pause: { id: 'pause-1', vetoedByMe: false } }))
    await lifecycle.toggleVeto('goal-1')
    expect(show).toHaveBeenCalledWith(de.toast.pauseVetoWithdrawn)
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

  it('stops a goal and says which ending it was', async () => {
    const api = lifecycleApi()
    const { show } = installFeatureGlobals(api)
    const lifecycle = useGoalLifecycle()

    await lifecycle.close('goal-1', false)

    expect(api.goals.close).toHaveBeenCalledWith('goal-1', { completed: false })
    expect(show).toHaveBeenCalledWith(de.toast.goalClosed)
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
    await state.remove('friend-1')

    expect(show).toHaveBeenCalledWith(de.toast.friendAdded('Emma'), { private: true })
    expect(show).toHaveBeenCalledWith(de.toast.requestSent)
    expect(show).toHaveBeenCalledWith(de.toast.requestWithdrawn)
    expect(show).toHaveBeenCalledWith(de.toast.friendRemoved)
    expect(refreshNuxtData).toHaveBeenCalledWith('profile')
  })

  it('reports a failed relationship action', async () => {
    const api = socialApi()
    api.friends.remove.mockRejectedValueOnce(new Error('remove'))
    const { report } = installFeatureGlobals(api)
    const state = useFriends()
    await settle()

    await state.remove('friend-1')
    expect(report).toHaveBeenCalledWith(expect.any(Error), { feature: 'friends', action: 'remove' })
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
    return { id: 'chat-1', title: 'Jonas', messages: [], isGroup: true, ...overrides }
  }

  function chatsApi() {
    return {
      chats: {
        list: vi.fn().mockResolvedValue([chat()]),
        get: vi.fn().mockResolvedValue(chat()),
        send: vi.fn().mockResolvedValue(chat({ messages: [{ id: 'message-1', text: 'Hello' }] })),
        react: vi.fn().mockResolvedValue(chat({ messages: [{ id: 'message-1', reactions: ['👏'] }] })),
        leave: vi.fn().mockResolvedValue(undefined),
      },
    }
  }

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
    const push = vi.fn().mockResolvedValue(undefined)
    vi.stubGlobal('useRouter', () => ({ push }))
    const { show, refreshNuxtData } = installFeatureGlobals(api)
    const state = useChatThread(ref('chat-1'))
    await vi.waitFor(() => expect(state.chat.value).not.toBeNull())

    await expect(state.send('   ')).resolves.toBe(false)
    await expect(state.send(' Hello ')).resolves.toBe(true)
    expect(api.chats.send).toHaveBeenCalledWith('chat-1', 'Hello')
    expect(state.isSending.value).toBe(false)

    await state.cheer()
    expect(api.chats.send).toHaveBeenCalledWith('chat-1', de.chats.cheerText)
    expect(show).toHaveBeenCalledWith(de.toast.cheerSent)

    await state.react('message-1', '👏')
    expect(api.chats.react).toHaveBeenCalledWith('chat-1', 'message-1', '👏')

    await state.leave()
    expect(refreshNuxtData).toHaveBeenCalledWith('chats')
    expect(refreshNuxtData).toHaveBeenCalledWith('profile')
    expect(push).toHaveBeenCalledWith('/chats')
    expect(show).toHaveBeenCalledWith(de.toast.groupLeft)
  })

  it('reports failed thread actions and distinguishes missing threads', async () => {
    const api = chatsApi()
    const push = vi.fn()
    vi.stubGlobal('useRouter', () => ({ push }))
    const { report } = installFeatureGlobals(api)
    const state = useChatThread(ref('chat-1'))
    await vi.waitFor(() => expect(state.chat.value).not.toBeNull())

    api.chats.send.mockRejectedValueOnce(new Error('send'))
    api.chats.react.mockRejectedValueOnce(new Error('react'))
    api.chats.leave.mockRejectedValueOnce(new Error('leave'))
    await expect(state.send('Hello')).resolves.toBe(false)
    await state.react('message-1', '👏')
    await state.leave()
    expect(report).toHaveBeenCalledWith(expect.any(Error), { feature: 'chats', action: 'send' })
    expect(report).toHaveBeenCalledWith(expect.any(Error), { feature: 'chats', action: 'react' })
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
        submit: vi.fn().mockResolvedValue({ id: 'proof-9', status: 'Voting' }),
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
    expect(show).toHaveBeenCalledWith(de.toast.voteCast)
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

  it('says whether a delivered photograph is waiting or already believed', async () => {
    const api = proofsApi()
    const { show } = installFeatureGlobals(api)
    const delivery = useProofDelivery()

    await expect(delivery.deliver('goal-1', { id: 'image-1' })).resolves.toBe(true)
    expect(api.proofs.submit).toHaveBeenCalledWith('goal-1', 'image-1', true)
    expect(show).toHaveBeenCalledWith(de.toast.proofDelivered)

    // A goal nobody shares has nobody to ask, so it comes back believed.
    api.proofs.submit.mockResolvedValueOnce({ id: 'proof-10', status: 'Confirmed' })
    await delivery.deliver('goal-1', { id: 'image-2' })
    expect(show).toHaveBeenCalledWith(de.toast.proofConfirmed)
  })

  it('keeps a refused delivery visible as a failure', async () => {
    const api = proofsApi()
    const { report } = installFeatureGlobals(api)
    const delivery = useProofDelivery()

    api.proofs.submit.mockRejectedValueOnce(new Error('window full'))

    await expect(delivery.deliver('goal-1', { id: 'image-1' })).resolves.toBe(false)
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
          sharedGoals: 2,
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
    expect(state.person.value).toMatchObject({ sharedGoals: 2, balance: { done: 3, missed: 1 } })
    expect(state.isMissing.value).toBe(false)
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
