import type { ApiCaller } from './client'
import type { PushKey } from './types'

/**
 * Registering this browser for notifications.
 *
 * Three calls and none of them sends anything. Notifications are produced by
 * the two background jobs on the server — a window falling due, a challenge
 * being published — and a client that could ask for one would be a client that
 * could send somebody else one.
 */
export interface NotificationsApi {
  /**
   * Whether this deployment sends notifications, and the key to subscribe
   * with. A deployment without VAPID keys is a supported state: the settings
   * screen says so rather than offering a switch that would do nothing.
   */
  key: () => Promise<PushKey>

  /** Registers this browser, or brings its keys up to date. */
  subscribe: (endpoint: string, publicKey: string, authSecret: string) => Promise<void>

  /** Forgets it. Succeeds whether or not there was anything to forget. */
  unsubscribe: (endpoint: string) => Promise<void>
}

export function createNotificationsApi(call: ApiCaller): NotificationsApi {
  return {
    key: () => call<PushKey>('/api/notifications/key', { method: 'GET' }),

    subscribe: async (endpoint, publicKey, authSecret) => {
      await call<unknown>('/api/notifications/subscribe', {
        method: 'POST',
        body: { endpoint, publicKey, authSecret },
      })
    },

    unsubscribe: async (endpoint) => {
      await call<unknown>('/api/notifications/unsubscribe', { method: 'POST', body: { endpoint } })
    },
  }
}
