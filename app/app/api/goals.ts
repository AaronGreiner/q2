import { normalizeApiError } from './errors'
import type { CreateGoalRequest, Goal, GoalStatus } from './types'

/**
 * The only place the frontend talks HTTP to the goal API.
 *
 * Components and composables call these functions; nobody else builds a URL or
 * inspects a response body. That is what makes "the contract changed" a
 * compile error in one file instead of a runtime bug spread across the app.
 *
 * Every function throws an {@link import('./errors').ApiError} — normalisation
 * happens here so callers never see a raw fetch error.
 */
export interface GoalsApi {
  list: (options?: { status?: GoalStatus }) => Promise<Goal[]>
  get: (id: string) => Promise<Goal>
  create: (request: CreateGoalRequest) => Promise<Goal>
}

/** Minimal shape of the fetcher, so tests can pass a stub. */
export type ApiFetch = <T>(url: string, options?: {
  method?: 'GET' | 'POST'
  query?: Record<string, string | undefined>
  body?: unknown
}) => Promise<T>

export function createGoalsApi(apiFetch: ApiFetch): GoalsApi {
  async function call<T>(...args: Parameters<ApiFetch>): Promise<T> {
    try {
      return await apiFetch<T>(...args)
    }
    catch (error) {
      throw normalizeApiError(error)
    }
  }

  return {
    list: options => call<Goal[]>('/api/goals', {
      method: 'GET',
      query: { status: options?.status },
    }),

    get: id => call<Goal>(`/api/goals/${encodeURIComponent(id)}`, { method: 'GET' }),

    create: request => call<Goal>('/api/goals', {
      method: 'POST',
      body: request,
    }),
  }
}
