import type { Counts } from '~/api/types'

/** What the badges show when there is nothing to count — or nothing known. */
export const noCounts: Counts = {
  unreadChats: 0,
  pendingFriendRequests: 0,
  unseenNotifications: 0,
  proofsAwaitingVote: 0,
}

/**
 * Every number the app puts on a badge: the tab bar's two, the bell's and the
 * vote banner's.
 *
 * One read under one key, shared by the layout and the start screen. It is
 * loaded once and then kept current by the live connection
 * (`useLiveConnection`), which replaces it whole whenever one of the numbers
 * moves on the server — that is how a badge changes without the page doing
 * anything. A screen whose own action moved a number reads it again itself
 * (`refreshNuxtData('counts')`), so the badge is right even while there is no
 * connection.
 *
 * These used to ride along on the profile. They have their own read now
 * because they have their own way of arriving
 * (docs/adr/0024-one-notification-pipeline.md).
 *
 * A failure is not worth an error state: the badges fall back to showing
 * nothing, which is what they show when there is nothing.
 */
export function useCounts() {
  const api = useQ2Api()
  const { report } = useErrorReporter()

  const { data, refresh } = useAsyncData<Counts>(
    'counts',
    async () => {
      try {
        return await api.notifications.counts()
      }
      catch (caught) {
        report(caught, { feature: 'notifications', action: 'counts' })
        return { ...noCounts }
      }
    },
    { default: () => ({ ...noCounts }) },
  )

  return {
    counts: computed(() => data.value ?? noCounts),
    refresh,
  }
}
