import type { ApiCaller } from './client'
import type { Activity, Friend, Friends, FriendSuggestion, LeaderboardEntry, Profile } from './types'

/** The feed, kudos and the leaderboard. */
export interface ActivityApi {
  feed: () => Promise<Activity[]>
  toggleKudos: (id: string) => Promise<Activity>
  leaderboard: () => Promise<LeaderboardEntry[]>
}

/** Friends, incoming requests and suggestions. */
export interface FriendsApi {
  get: (options?: { search?: string }) => Promise<Friends>
  accept: (id: string) => Promise<Friend>
  decline: (id: string) => Promise<void>
  request: (id: string) => Promise<FriendSuggestion>
}

/** The signed-in person's own profile. */
export interface ProfileApi {
  get: () => Promise<Profile>
}

export function createActivityApi(call: ApiCaller): ActivityApi {
  return {
    feed: () => call<Activity[]>('/api/feed', { method: 'GET' }),

    toggleKudos: id => call<Activity>(`/api/feed/${encodeURIComponent(id)}/kudos`, { method: 'POST' }),

    leaderboard: () => call<LeaderboardEntry[]>('/api/leaderboard', { method: 'GET' }),
  }
}

export function createFriendsApi(call: ApiCaller): FriendsApi {
  return {
    get: options => call<Friends>('/api/friends', {
      method: 'GET',
      query: { search: options?.search || undefined },
    }),

    accept: id => call<Friend>(`/api/friends/requests/${encodeURIComponent(id)}/accept`, { method: 'POST' }),

    // 204 No Content: there is nothing to hand back, and the caller reloads.
    decline: async (id) => {
      await call<unknown>(`/api/friends/requests/${encodeURIComponent(id)}/decline`, { method: 'POST' })
    },

    request: id => call<FriendSuggestion>(
      `/api/friends/suggestions/${encodeURIComponent(id)}/request`,
      { method: 'POST' },
    ),
  }
}

export function createProfileApi(call: ApiCaller): ProfileApi {
  return {
    get: () => call<Profile>('/api/profile', { method: 'GET' }),
  }
}
