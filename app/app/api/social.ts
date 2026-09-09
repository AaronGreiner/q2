import type { ApiCaller } from './client'
import type {
  Activity,
  Friend,
  Friends,
  PersonSearchResult,
  PersonProfile,
  Profile,
  UpdateProfileRequest,
} from './types'

/**
 * The feed and the kudos on it.
 *
 * There is no ranking here, and its absence is the product decision: q2 tells
 * your friends when you miss, so a table sorting everybody by how well they are
 * doing would be a scoreboard somebody comes last on.
 */
export interface ActivityApi {
  feed: () => Promise<Activity[]>
  toggleKudos: (id: string) => Promise<Activity>
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

/** The signed-in person's own profile, and other people's. */
export interface ProfileApi {
  get: () => Promise<Profile>

  /**
   * Somebody else's profile.
   *
   * The balance that comes back covers only the goals the two of them share —
   * scoped by the server, never by the screen. A client that filtered it would
   * be a client that could stop filtering it.
   */
  person: (personId: string) => Promise<PersonProfile>

  /**
   * Changes the name or the picture. Omitted properties keep their value, and
   * the whole profile comes back — the screen that called this is already
   * showing the rest of it.
   */
  update: (request: UpdateProfileRequest) => Promise<Profile>
}

export function createActivityApi(call: ApiCaller): ActivityApi {
  return {
    feed: () => call<Activity[]>('/api/feed', { method: 'GET' }),

    toggleKudos: id => call<Activity>(`/api/feed/${encodeURIComponent(id)}/kudos`, { method: 'POST' }),
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

    person: personId => call<PersonProfile>(`/api/people/${encodeURIComponent(personId)}`, { method: 'GET' }),

    update: request => call<Profile>('/api/profile', { method: 'PUT', body: request }),
  }
}
