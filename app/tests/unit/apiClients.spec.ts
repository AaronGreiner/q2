import { beforeEach, describe, expect, it, vi } from 'vitest'
import { createAccountsApi } from '~/api/accounts'
import { createChatsApi, createSettingsApi } from '~/api/chats'
import { createCaller, type ApiCaller, type ApiFetch } from '~/api/client'
import { createDiagnosticsApi } from '~/api/diagnostics'
import { ApiError } from '~/api/errors'
import { createGoalsApi, createTasksApi } from '~/api/goals'
import { createActivityApi, createFriendsApi, createProfileApi } from '~/api/social'

/**
 * These tests pin down the frontend's complete HTTP surface. A typo here does
 * not fail TypeScript: it still produces a perfectly valid string and only
 * shows up when a real person reaches that action.
 */
describe('the API caller', () => {
  it('returns the fetch result unchanged', async () => {
    const apiFetch = vi.fn<ApiFetch>().mockResolvedValue({ id: 'answer' })

    await expect(createCaller(apiFetch)<{ id: string }>('/api/example')).resolves.toEqual({ id: 'answer' })
  })

  it('normalises every rejected request once', async () => {
    const apiFetch = vi.fn<ApiFetch>().mockRejectedValue({ status: 404 })

    await expect(createCaller(apiFetch)('/api/example')).rejects.toMatchObject({
      kind: 'notFound',
      status: 404,
    })
  })
})

describe('the API modules', () => {
  let call: ReturnType<typeof vi.fn>
  let caller: ApiCaller

  beforeEach(() => {
    call = vi.fn().mockResolvedValue({})
    caller = call as ApiCaller
  })

  it('maps every account action to its contract route', async () => {
    const api = createAccountsApi(caller)
    const registration = { displayName: 'Mara', email: 'mara@example.test', password: 'long-enough-password' }
    const login = { email: registration.email, password: registration.password }

    await api.register(registration)
    await api.login(login)
    await api.session()
    await api.logout()

    expect(call.mock.calls).toEqual([
      ['/api/auth/register', { method: 'POST', body: registration }],
      ['/api/auth/login', { method: 'POST', body: login }],
      ['/api/auth/session', { method: 'GET' }],
      ['/api/auth/logout', { method: 'POST' }],
    ])
  })

  it('maps every goal and task action, including safe path encoding', async () => {
    const goals = createGoalsApi(caller)
    const tasks = createTasksApi(caller)
    const goalRequest = {
      title: 'Run',
      description: null,
      icon: 'flame',
      totalSteps: 10,
      rhythm: 'Daily' as const,
      targetDate: null,
      reminderAt: null,
      participantIds: [],
    }
    const taskRequest = {
      title: 'Shoes',
      goalId: null,
      rhythm: 'Daily' as const,
      weekdays: [],
      reminderAt: null,
      targetValue: null,
      measureUnit: null,
    }

    await goals.list()
    await goals.list({ status: 'Archived' })
    await goals.get('goal/one')
    await goals.create(goalRequest)
    await goals.contribute('goal/two')
    await tasks.list()
    await tasks.list({ all: true })
    await tasks.create(taskRequest)
    await tasks.toggle('task/one')

    expect(call.mock.calls).toEqual([
      ['/api/goals', { method: 'GET', query: { status: undefined } }],
      ['/api/goals', { method: 'GET', query: { status: 'Archived' } }],
      ['/api/goals/goal%2Fone', { method: 'GET' }],
      ['/api/goals', { method: 'POST', body: goalRequest }],
      ['/api/goals/goal%2Ftwo/contribute', { method: 'POST' }],
      ['/api/tasks', { method: 'GET', query: { all: undefined } }],
      ['/api/tasks', { method: 'GET', query: { all: 'true' } }],
      ['/api/tasks', { method: 'POST', body: taskRequest }],
      ['/api/tasks/task%2Fone/toggle', { method: 'POST' }],
    ])
  })

  it('maps every chat and settings action', async () => {
    const chats = createChatsApi(caller)
    const settings = createSettingsApi(caller)
    const group = { title: 'Crew', emoji: '🌱', participantIds: ['person-1'] }
    const update = { language: 'English' as const }

    await chats.list()
    await chats.list({ search: 'Mara' })
    await chats.get('chat/one')
    await chats.send('chat/one', 'Hello')
    await chats.react('chat/one', 'message/one', '👏')
    await chats.startDirect('person/one')
    await chats.createGroup(group)
    await chats.leave('chat/one')
    await settings.get()
    await settings.update(update)

    expect(call.mock.calls).toEqual([
      ['/api/chats', { method: 'GET', query: { search: undefined } }],
      ['/api/chats', { method: 'GET', query: { search: 'Mara' } }],
      ['/api/chats/chat%2Fone', { method: 'GET' }],
      ['/api/chats/chat%2Fone/messages', { method: 'POST', body: { text: 'Hello' } }],
      ['/api/chats/chat%2Fone/messages/message%2Fone/reactions', { method: 'POST', body: { emoji: '👏' } }],
      ['/api/chats/direct', { method: 'POST', body: { personId: 'person/one' } }],
      ['/api/chats/groups', { method: 'POST', body: group }],
      ['/api/chats/chat%2Fone/leave', { method: 'POST' }],
      ['/api/settings', { method: 'GET' }],
      ['/api/settings', { method: 'PUT', body: update }],
    ])
  })

  it('maps activity, friends, search and profile actions', async () => {
    const activity = createActivityApi(caller)
    const friends = createFriendsApi(caller)
    const profile = createProfileApi(caller)

    await activity.feed()
    await activity.toggleKudos('activity/one')
    await activity.leaderboard()
    await friends.get()
    await friends.search('Mara')
    await friends.request('person/one')
    await friends.withdraw('person/one')
    await friends.accept('person/one')
    await friends.decline('person/one')
    await friends.remove('person/one')
    await profile.get()

    expect(call.mock.calls).toEqual([
      ['/api/feed', { method: 'GET' }],
      ['/api/feed/activity%2Fone/kudos', { method: 'POST' }],
      ['/api/leaderboard', { method: 'GET' }],
      ['/api/friends', { method: 'GET' }],
      ['/api/friends/search', { method: 'GET', query: { query: 'Mara' } }],
      ['/api/friends/person%2Fone/request', { method: 'POST' }],
      ['/api/friends/person%2Fone/request', { method: 'DELETE' }],
      ['/api/friends/person%2Fone/accept', { method: 'POST' }],
      ['/api/friends/person%2Fone/decline', { method: 'POST' }],
      ['/api/friends/person%2Fone', { method: 'DELETE' }],
      ['/api/profile', { method: 'GET' }],
    ])
  })
})

describe('the diagnostics API', () => {
  it('calls both environment-only endpoints', async () => {
    const apiFetch = vi.fn().mockResolvedValue({ enabled: true })
    const api = createDiagnosticsApi(apiFetch)

    await api.sentryStatus()
    await api.triggerServerError()

    expect(apiFetch.mock.calls).toEqual([
      ['/api/diagnostics/sentry', { method: 'GET' }],
      ['/api/diagnostics/boom', { method: 'GET' }],
    ])
  })

  it('normalises failures from either diagnostics endpoint', async () => {
    const apiFetch = vi.fn().mockRejectedValue({ status: 500 })
    const api = createDiagnosticsApi(apiFetch)

    await expect(api.sentryStatus()).rejects.toBeInstanceOf(ApiError)
    await expect(api.triggerServerError()).rejects.toMatchObject({ kind: 'server' })
  })
})
