import { createGoalsApi, type ApiFetch, type GoalsApi } from '~/api/goals'

/**
 * The configured goals API client.
 *
 * The base URL comes from runtime config (`NUXT_PUBLIC_API_BASE_URL`), so the
 * same build runs against local, staging and production. No environment URL is
 * ever hard-coded in a component.
 */
export function useGoalsApi(): GoalsApi {
  const { public: config } = useRuntimeConfig()

  const apiFetch = $fetch.create({
    baseURL: config.apiBaseUrl,

    // A failed write must not be retried silently — it could create a second
    // goal. Retries, where they make sense, are the caller's decision.
    retry: 0,
    timeout: 10_000,
    headers: { Accept: 'application/json' },
  }) as ApiFetch

  return createGoalsApi(apiFetch)
}
