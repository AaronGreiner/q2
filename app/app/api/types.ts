import type { components } from './generated/schema'

/**
 * The API contract, named for use in the app.
 *
 * Everything the frontend knows about the backend's shapes comes from the
 * generated schema. Re-exporting here means components import from one stable
 * place, and a contract change surfaces as a type error rather than as a
 * runtime surprise.
 */
export type Goal = components['schemas']['GoalResponse']
export type GoalStatus = components['schemas']['GoalStatus']
export type CreateGoalRequest = components['schemas']['CreateGoalRequest']
export type ProblemDetails = components['schemas']['ProblemDetails']
export type ValidationProblemDetails = components['schemas']['HttpValidationProblemDetails']

/** Every status the API can return, in the order the UI offers them. */
export const goalStatuses: readonly GoalStatus[] = ['Active', 'Completed', 'Archived'] as const

export function isGoalStatus(value: unknown): value is GoalStatus {
  return typeof value === 'string' && (goalStatuses as readonly string[]).includes(value)
}
