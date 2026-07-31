import { computed, nextTick, ref, watch } from 'vue'
import { beforeEach, describe, expect, it, vi } from 'vitest'
import { de } from '~/i18n/messages'
import { useChatThread, useChats } from '~/composables/useChats'
import { useFriends, usePersonSearch, useProfile } from '~/composables/useFriends'
import { useGoalDetail, useGoals } from '~/composables/useGoals'
import { useHome } from '~/composables/useHome'

const failure = {
  kind: 'network' as const,
  isExpected: true,
  status: null,
  fieldErrors: {},
  traceId: null,
  errorId: null,
  reason: null,
}

function task(overrides: Record<string, unknown> = {}) {
  return {
    id: 'task-1',
    goalId: 'goal-1',
    title: 'Run',
    rhythm: 'Daily',
    reminderAt: null,
    isDone: false,
    measuredValue: null,
    targetValue: null,
    measureUnit: null,
    measurePercent: null,
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
    progressPercent: 20,
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
      tasks: {
        list: vi.fn().mockResolvedValue([task()]),
        toggle: vi.fn().mockResolvedValue(task({ isDone: true })),
      },
      goals: { list: vi.fn().mockResolvedValue([goal()]) },
      activity: {
        feed: vi.fn().mockResolvedValue([activity()]),
        leaderboard: vi.fn().mockResolvedValue([{ person: { id: 'person-1' }, score: 4 }]),
        toggleKudos: vi.fn().mockResolvedValue(activity({ kudosCount: 1, hasMyKudos: true })),
      },
    }
  }

  it('loads the complete dashboard and applies both actions', async () => {
    const api = homeApi()
    const { show } = installFeatureGlobals(api)
    const home = useHome()

    await vi.waitFor(() => expect(home.profile.value).toMatchObject({ displayName: 'Mara' }))
    expect(home.tasks.value).toHaveLength(1)
    expect(home.goals.value).toHaveLength(1)
    expect(home.feed.value).toHaveLength(1)
    expect(home.leaderboard.value).toHaveLength(1)
    expect(home.error.value).toBeNull()
    expect(home.isLoading.value).toBe(false)

    await home.toggleTask('task-1')
    expect(api.tasks.toggle).toHaveBeenCalledWith('task-1')
    expect(show).toHaveBeenCalledWith(de.toast.taskDone)

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
    expect(home.tasks.value).toEqual([])
    expect(report).toHaveBeenCalledWith(expect.any(Error), { feature: 'home', action: 'load' })

    api.tasks.toggle.mockRejectedValueOnce(new Error('toggle'))
    api.activity.toggleKudos.mockRejectedValueOnce(new Error('kudos'))
    await home.toggleTask('task-1')
    await home.toggleKudos('activity-1')

    expect(report).toHaveBeenCalledWith(expect.any(Error), { feature: 'tasks', action: 'toggle' })
    expect(report).toHaveBeenCalledWith(expect.any(Error), { feature: 'feed', action: 'kudos' })
  })
})

describe('goal composables', () => {
  function goalsApi() {
    return {
      goals: {
        list: vi.fn().mockResolvedValue([goal()]),
        create: vi.fn().mockResolvedValue(goal({ id: 'goal-new' })),
        get: vi.fn().mockResolvedValue({ ...goal(), tasks: [task()] }),
        contribute: vi.fn().mockResolvedValue(goal({ progressPercent: 100, status: 'Completed' })),
      },
      tasks: {
        list: vi.fn().mockResolvedValue([task()]),
        toggle: vi.fn().mockResolvedValue(task({ isDone: true })),
      },
    }
  }

  it('loads goals, updates a task and creates a goal', async () => {
    const api = goalsApi()
    const { show } = installFeatureGlobals(api)
    const state = useGoals()
    await vi.waitFor(() => expect(state.goals.value).toHaveLength(1))

    await state.toggleTask('task-1')
    expect(state.tasks.value[0]).toMatchObject({ isDone: true })
    expect(show).toHaveBeenCalledWith(de.toast.taskDone)

    const created = await state.create({ title: 'New goal' })
    expect(created).toMatchObject({ id: 'goal-new' })
    expect(state.isCreating.value).toBe(false)
    expect(state.createError.value).toBeNull()
    expect(show).toHaveBeenCalledWith(de.toast.goalCreated)
  })

  it('keeps create and toggle failures visible without changing product data', async () => {
    const api = goalsApi()
    const { report } = installFeatureGlobals(api)
    const state = useGoals()
    await vi.waitFor(() => expect(state.tasks.value).toHaveLength(1))

    api.goals.create.mockRejectedValueOnce(new Error('create'))
    api.tasks.toggle.mockRejectedValueOnce(new Error('toggle'))
    await expect(state.create({ title: 'Nope' })).resolves.toBeNull()
    await state.toggleTask('task-1')

    expect(state.createError.value).toEqual(failure)
    expect(report).toHaveBeenCalledWith(expect.any(Error), { feature: 'goals', action: 'create' })
    expect(report).toHaveBeenCalledWith(expect.any(Error), { feature: 'tasks', action: 'toggle' })
  })

  it('distinguishes a missing detail and refreshes after writes', async () => {
    const api = goalsApi()
    const { report, show } = installFeatureGlobals(api)
    const id = ref('goal-1')
    const detail = useGoalDetail(id)
    await vi.waitFor(() => expect(detail.detail.value).not.toBeNull())

    await detail.contribute()
    expect(show).toHaveBeenCalledWith(de.toast.goalReached)
    expect(detail.isContributing.value).toBe(false)

    await detail.toggleTask('task-1')
    expect(show).toHaveBeenCalledWith(de.toast.taskDone)

    api.goals.contribute.mockRejectedValueOnce(new Error('contribute'))
    api.tasks.toggle.mockRejectedValueOnce(new Error('toggle'))
    await detail.contribute()
    await detail.toggleTask('task-1')
    expect(report).toHaveBeenCalledWith(expect.any(Error), { feature: 'goals', action: 'contribute' })

    vi.stubGlobal('useErrorReporter', () => ({
      report: vi.fn(() => ({ ...failure, kind: 'notFound' as const })),
    }))
    api.goals.get.mockRejectedValueOnce(new Error('missing'))
    const missing = useGoalDetail(ref('missing'))
    await vi.waitFor(() => expect(missing.isMissing.value).toBe(true))
    expect(missing.error.value).toBeNull()
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
