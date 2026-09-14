import type { ApiCaller } from './client'
import type { Counts, NotificationLine, PushKey } from './types'

/**
 * The bell, the badges, and registering this browser for notifications.
 *
 * Nothing here sends anything. A notification is a consequence of something
 * somebody did — a message, a vote, a request — and the server produces it as
 * part of doing that. A client that could ask for one would be a client that
 * could send somebody else one.
 */
export interface NotificationsApi {
  /**
   * The bell's lines, newest first. Reading them is what marks them seen, the
   * way opening a conversation marks it read.
   */
  list: () => Promise<NotificationLine[]>

  /**
   * Every number drawn on a badge, in one read: unread conversations, pending
   * requests, the bell, proofs waiting for a vote. The live connection pushes
   * the same shape, so a badge never has two sources.
   */
  counts: () => Promise<Counts>

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
    list: () => call<NotificationLine[]>('/api/notifications', { method: 'GET' }),

    counts: () => call<Counts>('/api/counts', { method: 'GET' }),

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
