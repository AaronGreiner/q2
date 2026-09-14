import type { ApiFailure } from '~/api/errors'
import type { Counts, NotificationLine } from '~/api/types'
import { isFirstLoad, placeholder } from '~/utils/firstLoad'

interface BellPayload {
  lines: NotificationLine[]
  failure: ApiFailure | null
}

/**
 * The bell: what happened to you, newest first.
 *
 * Reading it is seeing it. The server moves the seen-marker in the same
 * request (InboxService), so the number on the bell drops to zero here too,
 * straight away, rather than waiting for the live connection to say so.
 *
 * Which lines are new is the server's answer, measured against the marker as
 * it stood *before* this read — so what brought somebody here is still drawn
 * as new on the screen that just cleared it.
 */
export function useNotifications() {
  const api = useQ2Api()
  const { report } = useErrorReporter()
  const counts = useNuxtData<Counts>('counts')

  const { data, status, refresh } = useAsyncData<BellPayload>(
    'notifications',
    async () => {
      try {
        return { lines: await api.notifications.list(), failure: null }
      }
      catch (caught) {
        return { lines: [], failure: report(caught, { feature: 'notifications', action: 'list' }) }
      }
    },
    { default: (): BellPayload => placeholder({ lines: [], failure: null }) },
  )

  // Once the server has answered, and only if it did: the empty default a
  // read starts from is not an answer, and clearing the badge for a read that
  // then fails would hide what is still waiting.
  watch([data, status], ([payload, state]) => {
    if (state === 'success' && payload && !payload.failure && counts.data.value) {
      // The whole object rather than one field of it: a shallow ref changed
      // in place moves nothing on screen.
      counts.data.value = { ...counts.data.value, unseenNotifications: 0 }
    }
  }, { immediate: true })

  const lines = computed(() => data.value?.lines ?? [])

  /*
   * What was new when this screen showed it stays new while the screen is open.
   *
   * The server measures "new" against the marker as it stood before each read,
   * and this screen's own first read moved it — so every read after that, the
   * live connection catching up or another line arriving, answers "seen" for
   * all of it and would move it under "Früher" in front of the person still
   * reading. Leaving the screen forgets this; the next visit starts over.
   */
  const shownAsNew = new Set<string>()

  watch(lines, (current) => {
    for (const line of current) {
      if (line.isNew && line.id) shownAsNew.add(line.id)
    }
  }, { immediate: true })

  const isNew = (line: NotificationLine) => line.isNew || (line.id !== null && shownAsNew.has(line.id))

  return {
    fresh: computed(() => lines.value.filter(isNew)),
    earlier: computed(() => lines.value.filter(line => !isNew(line))),
    isEmpty: computed(() => lines.value.length === 0),
    error: computed(() => data.value?.failure ?? null),
    isLoading: computed(() => isFirstLoad(status.value, data.value)),
    refresh,
  }
}
