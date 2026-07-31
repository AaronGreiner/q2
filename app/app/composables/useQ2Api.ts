import { createCaller, type ApiFetch } from '~/api/client'
import { createChatsApi, createSettingsApi } from '~/api/chats'
import { createGoalsApi, createTasksApi } from '~/api/goals'
import { createActivityApi, createFriendsApi, createProfileApi } from '~/api/social'

/**
 * The configured q2 API client.
 *
 * The base URL comes from runtime config (`NUXT_PUBLIC_API_BASE_URL`), so the
 * same build runs against local, staging and production. No environment URL is
 * ever hard-coded in a component.
 */
export function useQ2Api() {
  const { public: config } = useRuntimeConfig()

  const apiFetch = $fetch.create({
    baseURL: config.apiBaseUrl,

    // A failed write must not be retried silently — it could send a message
    // twice. Retries, where they make sense, are the caller's decision.
    retry: 0,
    timeout: 10_000,
    headers: { Accept: 'application/json' },
  }) as ApiFetch

  const call = createCaller(apiFetch)

  return {
    goals: createGoalsApi(call),
    tasks: createTasksApi(call),
    activity: createActivityApi(call),
    friends: createFriendsApi(call),
    chats: createChatsApi(call),
    profile: createProfileApi(call),
    settings: createSettingsApi(call),
  }
}

export type Q2Api = ReturnType<typeof useQ2Api>
