import type { ApiFailure } from '~/api/errors'
import type { EditGoalRequest, Goal, GoalEditPreview } from '~/api/types'
import { isFirstLoad, placeholder } from '~/utils/firstLoad'

interface ArchivePayload {
  goals: Goal[]
  failure: ApiFailure | null
}

/**
 * What the owner can do to a running goal and what its checkers can do about
 * it: setting it aside, editing it, objecting to either, and stopping it.
 *
 * One composable for all of them because they are one decision from the
 * screen's point of view — every one of them answers with the whole goal, and
 * every one of them is followed by the same reload. Splitting them would mean
 * a copy of the busy flag and of the failure that goes with it for each.
 */
export function useGoalLifecycle() {
  const api = useQ2Api()
  const { report } = useErrorReporter()

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
  const pause = (id: string, reason: string, days: number) =>
    run('pause', () => api.goals.pause(id, { reason, days }))

  const endPause = (id: string) =>
    run('endPause', () => api.goals.endPause(id))

  /**
   * Raises this person's objection, or takes it back. The same request does
   * both, so the goal that comes back is the only thing that knows which.
   */
  const toggleVeto = (id: string) =>
    run('vetoPause', () => api.goals.vetoPause(id))

  const close = (id: string, completed: boolean) =>
    run('close', () => api.goals.close(id, { completed }))

  /** Moves the daily reminder — `HH:MM:SS` — or takes it away with null. */
  const setReminder = (id: string, reminderAt: string | null) =>
    run('reminder', () => api.goals.setReminder(id, { reminderAt }))

  /**
   * Edits the goal. What applies now and what waits is the server's answer,
   * read back from the goal it returns (docs/adr/0044-editing-a-running-goal.md).
   */
  const edit = (id: string, request: EditGoalRequest) =>
    run('edit', () => api.goals.edit(id, request))

  /** Takes back the change waiting on the goal. */
  const withdrawChange = (id: string) =>
    run('withdrawChange', () => api.goals.withdrawChange(id))

  /** Raises this person's objection to the waiting change, or takes it back. */
  const toggleChangeVeto = (id: string) =>
    run('vetoChange', () => api.goals.vetoChange(id))

  const previewing = ref(false)
  const preview = ref<GoalEditPreview | null>(null)

  /**
   * What an edit would do, for the sheet's last step. Its own busy flag and
   * its own answer: reading it is not one of the actions above, and a
   * refusal lands in {@link error} like theirs, with the field it is about.
   */
  async function previewEdit(id: string, request: EditGoalRequest): Promise<GoalEditPreview | null> {
    previewing.value = true
    failure.value = null
    preview.value = null

    try {
      preview.value = await api.goals.previewEdit(id, request)
      return preview.value
    }
    catch (caught) {
      failure.value = report(caught, { feature: 'goals', action: 'previewEdit' })
      return null
    }
    finally {
      previewing.value = false
    }
  }

  return {
    isBusy: computed(() => isBusy.value),
    error: computed(() => failure.value),
    pause,
    endPause,
    toggleVeto,
    close,
    setReminder,
    edit,
    withdrawChange,
    toggleChangeVeto,
    previewEdit,
    preview: computed(() => preview.value),
    isPreviewing: computed(() => previewing.value),
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
    { default: (): ArchivePayload => placeholder({ goals: [], failure: null }) },
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
    isLoading: computed(() => isFirstLoad(status.value, data.value)),
    isRemoving: computed(() => isRemoving.value),
    refresh,
    remove,
  }
}
