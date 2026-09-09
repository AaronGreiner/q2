import { beforeEach, describe, expect, it, vi } from 'vitest'
import { createAccountsApi } from '~/api/accounts'
import { createChatsApi, createSettingsApi } from '~/api/chats'
import { createCaller, type ApiCaller, type ApiFetch } from '~/api/client'
import { createDiagnosticsApi } from '~/api/diagnostics'
import { ApiError } from '~/api/errors'
import { createGoalsApi } from '~/api/goals'
import { createImagesApi, imageUrl, uploadContentType } from '~/api/images'
import { createProofsApi } from '~/api/proofs'
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

  it('maps every goal action, including safe path encoding', async () => {
    const goals = createGoalsApi(caller)
    const goalRequest = {
      title: 'Run',
      description: null,
      icon: 'flame',
      schedule: { kind: 'Times' as const, times: 3, period: 'Week' as const },
      targetDate: null,
      reminderAt: null,
      participantIds: [],
    }

    await goals.list()
    await goals.list({ status: 'Archived' })
    await goals.today()
    await goals.archive()
    await goals.get('goal/one')
    await goals.create(goalRequest)
    await goals.pause('goal/one', { reason: 'Grippe, seit Freitag im Bett.', days: 3 })
    await goals.endPause('goal/one')
    await goals.vetoPause('goal/one')
    await goals.close('goal/one', { completed: true })
    await goals.remove('goal/one')

    expect(call.mock.calls).toEqual([
      ['/api/goals', { method: 'GET', query: { status: undefined } }],
      ['/api/goals', { method: 'GET', query: { status: 'Archived' } }],
      ['/api/today', { method: 'GET' }],
      ['/api/goals/archive', { method: 'GET' }],
      ['/api/goals/goal%2Fone', { method: 'GET' }],
      ['/api/goals', { method: 'POST', body: goalRequest }],
      ['/api/goals/goal%2Fone/pause', {
        method: 'POST',
        body: { reason: 'Grippe, seit Freitag im Bett.', days: 3 },
      }],
      ['/api/goals/goal%2Fone/pause', { method: 'DELETE' }],
      ['/api/goals/goal%2Fone/pause/veto', { method: 'POST' }],
      ['/api/goals/goal%2Fone/close', { method: 'POST', body: { completed: true } }],
      ['/api/goals/goal%2Fone', { method: 'DELETE' }],
    ])
  })

  it('maps every proof action, including the route that still reads as a goal', async () => {
    const api = createProofsApi(caller)

    await api.submit('goal/two', 'image-1', true)
    await api.get('proof/one')
    await api.pending()
    await api.vote('proof/one', 'Doubt')
    await api.react('proof/one', 'Fire')

    expect(call.mock.calls).toEqual([
      ['/api/goals/goal%2Ftwo/proof', { method: 'POST', body: { imageId: 'image-1', capturedInApp: true } }],
      ['/api/proofs/proof%2Fone', { method: 'GET' }],
      ['/api/proofs/pending', { method: 'GET' }],
      ['/api/proofs/proof%2Fone/vote', { method: 'POST', body: { value: 'Doubt' } }],
      ['/api/proofs/proof%2Fone/reactions', { method: 'POST', body: { kind: 'Fire' } }],
    ])
  })

  it('maps every chat and settings action', async () => {
    const chats = createChatsApi(caller)
    const settings = createSettingsApi(caller)
    const group = { title: 'Crew', icon: 'sprout', participantIds: ['person-1'] }
    const update = { language: 'English' as const }

    await chats.list()
    await chats.list({ search: 'Mara' })
    await chats.get('chat/one')
    await chats.send('chat/one', 'Hello')
    await chats.react('chat/one', 'message/one', 'Applause')
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
      ['/api/chats/chat%2Fone/messages/message%2Fone/reactions', { method: 'POST', body: { kind: 'Applause' } }],
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
    await friends.get()
    await friends.search('Mara')
    await friends.request('person/one')
    await friends.withdraw('person/one')
    await friends.accept('person/one')
    await friends.decline('person/one')
    await friends.remove('person/one')
    await profile.get()
    await profile.update({ displayName: 'Mara Sommer' })

    expect(call.mock.calls).toEqual([
      ['/api/feed', { method: 'GET' }],
      ['/api/feed/activity%2Fone/kudos', { method: 'POST' }],
      ['/api/friends', { method: 'GET' }],
      ['/api/friends/search', { method: 'GET', query: { query: 'Mara' } }],
      ['/api/friends/person%2Fone/request', { method: 'POST' }],
      ['/api/friends/person%2Fone/request', { method: 'DELETE' }],
      ['/api/friends/person%2Fone/accept', { method: 'POST' }],
      ['/api/friends/person%2Fone/decline', { method: 'POST' }],
      ['/api/friends/person%2Fone', { method: 'DELETE' }],
      ['/api/profile', { method: 'GET' }],
      ['/api/profile', { method: 'PUT', body: { displayName: 'Mara Sommer' } }],
    ])
  })

  it('sends an upload as the raw body under its own media type', async () => {
    const api = createImagesApi(caller)
    const file = new Blob([new Uint8Array([1, 2, 3])], { type: uploadContentType })

    await api.upload(file, 'Avatar')
    await api.quota()
    await api.remove('image/one')

    expect(call.mock.calls).toEqual([
      ['/api/images', {
        method: 'POST',
        query: { purpose: 'Avatar' },
        body: file,
        headers: { 'Content-Type': uploadContentType },
      }],
      ['/api/images/quota', { method: 'GET' }],
      ['/api/images/image%2Fone', { method: 'DELETE' }],
    ])
  })

  it('falls back to a media type when the browser gave the blob none', async () => {
    // A canvas that refused the requested format hands back a blob with an
    // empty type, and an upload with no media type is a 415 rather than a
    // picture.
    const api = createImagesApi(caller)

    await api.upload(new Blob([new Uint8Array([1])]), 'Proof')

    expect(call.mock.calls[0]?.[1]).toMatchObject({ headers: { 'Content-Type': uploadContentType } })
  })
})

describe('an image address', () => {
  it('is absolute, because an img resolves against the page and not the client', () => {
    expect(imageUrl('http://localhost:5080', 'abc')).toBe('http://localhost:5080/api/images/abc')
  })

  it('does not double the separator when the base already ends in one', () => {
    expect(imageUrl('http://localhost:5080/', 'abc')).toBe('http://localhost:5080/api/images/abc')
  })

  it('escapes the id rather than pasting it into a path', () => {
    expect(imageUrl('http://x', 'a/b')).toBe('http://x/api/images/a%2Fb')
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
