import type { ApiFailure } from '~/api/errors'
import type { Goal } from '~/api/types'

interface ArchivePayload {
  goals: Goal[]
  failure: ApiFailure | null
}

/**
 * The exits a goal has: setting it aside, objecting to that, and stopping it.
 *
 * One composable for all four because they are one decision from the screen's
 * point of view — every one of them answers with the whole goal, and every one
 * of them is followed by the same reload. Splitting them would mean four
 * copies of the busy flag and four places to forget the toast.
 */
export function useGoalLifecycle() {
  const api = useQ2Api()
  const { report } = useErrorReporter()
  const toast = useToastMessage()
  const t = useMessages()

  const isBusy = ref(false)
  const failure = ref<ApiFailure | null>(null)

  async function run(action: string, work: () => Promise<Goal>): Promise<Goal | null> {
    if (isBusy.value) return null

    isBusy.value = true
    failure.value = null

    try {
      return await work()
    }
    catch (caught) {
      failure.value = report(caught, { feature: 'goals', action })
      return null
    }
    finally {
      isBusy.value = false
    }
  }

  /**
   * Sets a goal aside. Returns the goal, or null with the reason in
   * {@link error} — the allowance and "one at a time" are the server's answer,
   * including their field-level messages.
   */
  async function pause(id: string, reason: string, days: number) {
    const goal = await run('pause', () => api.goals.pause(id, { reason, days }))

    if (goal) toast.show(t.value.toast.goalPaused)
    return goal
  }

  async function endPause(id: string) {
    const goal = await run('endPause', () => api.goals.endPause(id))

    if (goal) toast.show(t.value.toast.pauseEnded)
    return goal
  }

  /**
   * Raises this person's objection, or takes it back.
   *
   * The toast is chosen from what came back rather than from what was sent: the
   * same request does both, and only the response knows which it was.
   */
  async function toggleVeto(id: string) {
    const goal = await run('vetoPause', () => api.goals.vetoPause(id))

    if (goal) {
      toast.show(goal.pause?.vetoedByMe ? t.value.toast.pauseVetoed : t.value.toast.pauseVetoWithdrawn)
    }

    return goal
  }

  async function close(id: string, completed: boolean) {
    const goal = await run('close', () => api.goals.close(id, { completed }))

    if (goal) toast.show(t.value.toast.goalClosed)
    return goal
  }

  return {
    isBusy: computed(() => isBusy.value),
    error: computed(() => failure.value),
    pause,
    endPause,
    toggleVeto,
    close,
  }
}

/**
 * The archive: goals that have stopped, and the only place they can be deleted.
 *
 * Its own request rather than a filter over the goals list — the archive is
 * sorted by when things ended and holds both kinds of ending, and a screen that
 * filtered client-side would download every running goal to show none of them.
 */
export function useGoalArchive() {
  const api = useQ2Api()
  const { report } = useErrorReporter()
  const toast = useToastMessage()
  const t = useMessages()

  const { data, status, refresh } = useAsyncData<ArchivePayload>(
    'goals-archive',
    async () => {
      try {
        return { goals: await api.goals.archive(), failure: null }
      }
      catch (caught) {
        return { goals: [], failure: report(caught, { feature: 'goals', action: 'archive' }) }
      }
    },
    { default: (): ArchivePayload => ({ goals: [], failure: null }) },
  )

  const isRemoving = ref(false)

  /** Deletes one for good. Returns true when it went. */
  async function remove(id: string): Promise<boolean> {
    if (isRemoving.value) return false

    isRemoving.value = true

    try {
      await api.goals.remove(id)
      await refresh()
      toast.show(t.value.toast.goalDeleted)
      return true
    }
    catch (caught) {
      report(caught, { feature: 'goals', action: 'delete' })
      return false
    }
    finally {
      isRemoving.value = false
    }
  }

  return {
    goals: computed(() => data.value?.goals ?? []),
    error: computed(() => data.value?.failure ?? null),
    isLoading: computed(() => status.value === 'pending'),
    isRemoving: computed(() => isRemoving.value),
    refresh,
    remove,
  }
}
