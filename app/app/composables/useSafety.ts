import type { ApiFailure } from '~/api/errors'
import type { Person, ReportReason, ReportTargetKind } from '~/api/types'

/**
 * Reporting and blocking, from wherever they are offered.
 *
 * One composable for both because they are one gesture on screen — the sheet
 * that offers "melden" also offers "blockieren" — and because both end the same
 * way: the thing you did not want to see is gone, and you are told so once.
 *
 * Neither decides anything. Who may report what, and what a block reaches, are
 * the server's answers; this only says what happens on screen while it is on
 * its way.
 */
export function useSafety() {
  const api = useQ2Api()
  const { report: reportError } = useErrorReporter()
  const toast = useToastMessage()
  const t = useMessages()

  const isBusy = ref(false)

  /** Files a report. Returns true when it was received. */
  async function report(
    targetKind: ReportTargetKind,
    targetId: string,
    reason: ReportReason,
    note: string,
  ): Promise<boolean> {
    if (isBusy.value) return false

    isBusy.value = true

    try {
      await api.moderation.report(targetKind, targetId, reason, note)

      // Thanked once, and told nothing else. There is no status to follow,
      // because an outcome would be news about somebody else's account.
      toast.show(t.value.toast.reported)
      return true
    }
    catch (caught) {
      reportError(caught, { feature: 'moderation', action: 'report' })
      return false
    }
    finally {
      isBusy.value = false
    }
  }

  /** Blocks somebody. Returns true when it took. */
  async function block(personId: string): Promise<boolean> {
    if (isBusy.value) return false

    isBusy.value = true

    try {
      await api.moderation.block(personId)
      toast.show(t.value.toast.blocked)
      return true
    }
    catch (caught) {
      reportError(caught, { feature: 'moderation', action: 'block' })
      return false
    }
    finally {
      isBusy.value = false
    }
  }

  return { isBusy: computed(() => isBusy.value), report, block }
}

/**
 * The list of people this person has blocked.
 *
 * Its own read rather than part of the settings payload: it is one screen
 * behind a row most people never open, and loading it with the settings would
 * be a request on every visit for a list that is almost always empty.
 */
export function useBlockedPeople() {
  const api = useQ2Api()
  const { report } = useErrorReporter()
  const toast = useToastMessage()
  const t = useMessages()

  const { data, status, refresh } = useAsyncData(
    'blocked-people',
    async () => {
      try {
        return { people: await api.moderation.blocked(), failure: null as ApiFailure | null }
      }
      catch (caught) {
        return {
          people: [] as Person[],
          failure: report(caught, { feature: 'moderation', action: 'blocked' }),
        }
      }
    },
    { default: () => ({ people: [] as Person[], failure: null as ApiFailure | null }) },
  )

  const isUnblocking = ref(false)

  async function unblock(personId: string) {
    if (isUnblocking.value) return

    isUnblocking.value = true

    try {
      // The server answers with the list, so the screen is drawn from what it
      // says rather than from what this guessed.
      data.value = { people: await api.moderation.unblock(personId), failure: null }
      toast.show(t.value.toast.unblocked)
    }
    catch (caught) {
      report(caught, { feature: 'moderation', action: 'unblock' })
      await refresh()
    }
    finally {
      isUnblocking.value = false
    }
  }

  return {
    people: computed(() => data.value?.people ?? []),
    error: computed(() => data.value?.failure ?? null),
    isLoading: computed(() => status.value === 'pending'),
    isUnblocking: computed(() => isUnblocking.value),
    refresh,
    unblock,
  }
}
