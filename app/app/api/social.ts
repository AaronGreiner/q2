import type { ApiCaller } from './client'
import type {
  Activity,
  Friend,
  Friends,
  LeaderboardEntry,
  PersonSearchResult,
  Profile,
} from './types'

/** The feed, kudos and the leaderboard. */
export interface ActivityApi {
  feed: () => Promise<Activity[]>
  toggleKudos: (id: string) => Promise<Activity>
  leaderboard: () => Promise<LeaderboardEntry[]>
}

/**
 * Friends, requests in both directions, suggestions and person search.
 *
 * Everything is addressed by *person* id. The client always has the person —
 * from a search result, a request row or the friends list — and never has to
 * hold on to the id of a friendship row that the next tap may delete.
 */
export interface FriendsApi {
  get: () => Promise<Friends>
  search: (query: string) => Promise<PersonSearchResult[]>
  request: (personId: string) => Promise<PersonSearchResult>
  withdraw: (personId: string) => Promise<void>
  accept: (personId: string) => Promise<Friend>
  decline: (personId: string) => Promise<void>
  remove: (personId: string) => Promise<void>
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
  const person = (personId: string) => `/api/friends/${encodeURIComponent(personId)}`

  return {
    get: () => call<Friends>('/api/friends', { method: 'GET' }),

    search: query => call<PersonSearchResult[]>('/api/friends/search', {
      method: 'GET',
      query: { query },
    }),

    request: personId => call<PersonSearchResult>(`${person(personId)}/request`, { method: 'POST' }),

    // 204 No Content on all four of these: there is nothing to hand back, and
    // the caller reloads the screen it changed.
    withdraw: async (personId) => {
      await call<unknown>(`${person(personId)}/request`, { method: 'DELETE' })
    },

    accept: personId => call<Friend>(`${person(personId)}/accept`, { method: 'POST' }),

    decline: async (personId) => {
      await call<unknown>(`${person(personId)}/decline`, { method: 'POST' })
    },

    remove: async (personId) => {
      await call<unknown>(person(personId), { method: 'DELETE' })
    },
  }
}

export function createProfileApi(call: ApiCaller): ProfileApi {
  return {
    get: () => call<Profile>('/api/profile', { method: 'GET' }),
  }
}
