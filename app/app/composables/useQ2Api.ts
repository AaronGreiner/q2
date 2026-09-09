import { createCaller, type ApiFetch } from '~/api/client'
import { createAccountsApi } from '~/api/accounts'
import { createChallengesApi } from '~/api/challenges'
import { createChatsApi, createSettingsApi } from '~/api/chats'
import { createGoalsApi } from '~/api/goals'
import { createImagesApi } from '~/api/images'
import { createInviteApi, createModerationApi } from '~/api/moderation'
import { createNotificationsApi } from '~/api/notifications'
import { createProofsApi } from '~/api/proofs'
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

  /*
   * The session is a cookie, and a cookie does not travel by itself.
   *
   * In the browser it needs `credentials: 'include'`, because the API is a
   * different origin from the frontend (localhost:3000 → localhost:5080), and
   * a cross-origin fetch sends no cookies unless asked. The server side of the
   * CORS policy has the matching `AllowCredentials`.
   *
   * During server rendering there is no browser to attach it: Nitro is making
   * the request, so the cookie has to be copied off the request that arrived.
   * Without this the first paint of every page would be the signed-out one and
   * the screen would only fill in after hydration.
   */
  const headers: Record<string, string> = { Accept: 'application/json' }

  if (import.meta.server) {
    const cookie = useRequestHeaders(['cookie']).cookie

    if (cookie) {
      headers.cookie = cookie
    }
  }

  const apiFetch = $fetch.create({
    baseURL: config.apiBaseUrl,
    credentials: 'include',

    // A failed write must not be retried silently — it could send a message
    // twice. Retries, where they make sense, are the caller's decision.
    retry: 0,
    timeout: 10_000,
    headers,
  }) as ApiFetch

  const call = createCaller(apiFetch)

  return {
    accounts: createAccountsApi(call),
    goals: createGoalsApi(call),
    images: createImagesApi(call),
    proofs: createProofsApi(call),
    challenges: createChallengesApi(call),
    moderation: createModerationApi(call),
    invite: createInviteApi(call),
    notifications: createNotificationsApi(call),
    activity: createActivityApi(call),
    friends: createFriendsApi(call),
    chats: createChatsApi(call),
    profile: createProfileApi(call),
    settings: createSettingsApi(call),
  }
}

export type Q2Api = ReturnType<typeof useQ2Api>
