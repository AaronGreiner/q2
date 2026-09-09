import type { ApiCaller } from './client'
import type { CloseGoalRequest, CreateGoalRequest, Goal, GoalDetail, GoalStatus, RequestPauseRequest } from './types'

/**
 * The only place the frontend talks HTTP about goals.
 *
 * Components and composables call these functions; nobody else builds a URL or
 * inspects a response body. That is what makes "the contract changed" a
 * compile error in one file instead of a runtime bug spread across the app.
 */
export interface GoalsApi {
  list: (options?: { status?: GoalStatus }) => Promise<Goal[]>

  /** The goals whose current window covers today — what is on somebody's plate. */
  today: () => Promise<Goal[]>

  /** The goals that have stopped, most recently stopped first. */
  archive: () => Promise<Goal[]>

  get: (id: string) => Promise<GoalDetail>
  create: (request: CreateGoalRequest) => Promise<Goal>

  /** Sets a goal aside for whole days, with a reason its friends read. */
  pause: (id: string, request: RequestPauseRequest) => Promise<Goal>

  /** Ends the running pause early. */
  endPause: (id: string) => Promise<Goal>

  /** Objects to a running pause, or takes the objection back. Anonymous. */
  vetoPause: (id: string) => Promise<Goal>

  /** Stops a goal for good and moves it to the archive. */
  close: (id: string, request: CloseGoalRequest) => Promise<Goal>

  /** Deletes a stopped goal outright, for everybody on it. */
  remove: (id: string) => Promise<void>

  /*
   * Delivering a proof is deliberately not here. It is a photograph and a vote,
   * which is `~/api/proofs.ts` — the route still reads `/api/goals/{id}/proof`
   * because that is how people think about it, but everything that follows from
   * it belongs to the photograph.
   */
}

export function createGoalsApi(call: ApiCaller): GoalsApi {
  return {
    list: options => call<Goal[]>('/api/goals', {
      method: 'GET',
      query: { status: options?.status },
    }),

    today: () => call<Goal[]>('/api/today', { method: 'GET' }),

    archive: () => call<Goal[]>('/api/goals/archive', { method: 'GET' }),

    get: id => call<GoalDetail>(goal(id), { method: 'GET' }),

    create: request => call<Goal>('/api/goals', { method: 'POST', body: request }),

    pause: (id, request) => call<Goal>(`${goal(id)}/pause`, { method: 'POST', body: request }),

    endPause: id => call<Goal>(`${goal(id)}/pause`, { method: 'DELETE' }),

    vetoPause: id => call<Goal>(`${goal(id)}/pause/veto`, { method: 'POST' }),

    close: (id, request) => call<Goal>(`${goal(id)}/close`, { method: 'POST', body: request }),

    // 204 and no body: there is nothing left to describe.
    remove: async (id) => {
      await call<unknown>(goal(id), { method: 'DELETE' })
    },
  }
}

function goal(id: string): string {
  return `/api/goals/${encodeURIComponent(id)}`
}
