import type { ApiCaller } from './client'
import type { CreateGoalRequest, CreateTaskRequest, Goal, GoalDetail, GoalStatus, GoalTask } from './types'

/**
 * The only place the frontend talks HTTP about goals and tasks.
 *
 * Components and composables call these functions; nobody else builds a URL or
 * inspects a response body. That is what makes "the contract changed" a
 * compile error in one file instead of a runtime bug spread across the app.
 */
export interface GoalsApi {
  list: (options?: { status?: GoalStatus }) => Promise<Goal[]>
  get: (id: string) => Promise<GoalDetail>
  create: (request: CreateGoalRequest) => Promise<Goal>
  contribute: (id: string) => Promise<Goal>
}

export interface TasksApi {
  list: (options?: { all?: boolean }) => Promise<GoalTask[]>
  create: (request: CreateTaskRequest) => Promise<GoalTask>
  toggle: (id: string) => Promise<GoalTask>
}

export function createGoalsApi(call: ApiCaller): GoalsApi {
  return {
    list: options => call<Goal[]>('/api/goals', {
      method: 'GET',
      query: { status: options?.status },
    }),

    get: id => call<GoalDetail>(`/api/goals/${encodeURIComponent(id)}`, { method: 'GET' }),

    create: request => call<Goal>('/api/goals', { method: 'POST', body: request }),

    contribute: id => call<Goal>(`/api/goals/${encodeURIComponent(id)}/contribute`, { method: 'POST' }),
  }
}

export function createTasksApi(call: ApiCaller): TasksApi {
  return {
    list: options => call<GoalTask[]>('/api/tasks', {
      method: 'GET',
      query: { all: options?.all ? 'true' : undefined },
    }),

    create: request => call<GoalTask>('/api/tasks', { method: 'POST', body: request }),

    toggle: id => call<GoalTask>(`/api/tasks/${encodeURIComponent(id)}/toggle`, { method: 'POST' }),
  }
}
