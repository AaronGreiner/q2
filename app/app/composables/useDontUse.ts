import type { ApiFailure } from '~/api/errors'
import type { Goal } from '~/api/types'

/**
 * Logging a use of a Don't: "Heute 1 Bier".
 *
 * One tap from the row, the goal and its conversation, and the same request
 * from all three, which is why it is a composable of its own. Whether the tap
 * needs confirming first (when it would be a relapse) is the button's business
 * (`DontLogButton`); this only sends it and says what became of it, because
 * the answer is the only thing that knows (docs/adr/0037-donts.md).
 *
 * A use within the limit is confirmed with "Rückgängig" for a few seconds
 * (#110): the "+" sits in a scrolling list, and a scroll that starts on it
 * should not cost a use. A relapse gets no such button — it was asked about
 * before, and it cannot be taken back. `onUndone` is how the screen reads
 * itself again afterwards.
 */
export function useDontUse(onUndone: () => unknown = () => undefined) {
  const api = useQ2Api()
  const { report } = useErrorReporter()
  const toast = useToastMessage()
  const t = useMessages()

  const isLogging = ref(false)
  const failure = ref<ApiFailure | null>(null)

  /** Returns the goal as it is now, or null with the reason in `error`. */
  async function logUse(goalId: string, imageId: string | null = null): Promise<Goal | null> {
    if (isLogging.value) return null

    isLogging.value = true
    failure.value = null

    try {
      const goal = await api.goals.logUse(goalId, { imageId })
      const useId = goal.current?.latestUseId

      if (goal.current?.status === 'Relapse' || !useId) {
        toast.show(goal.current?.status === 'Relapse' ? t.value.toast.relapseLogged : t.value.toast.useLogged)
      }
      else {
        toast.show(t.value.toast.useLogged, { label: t.value.toast.undo, onClick: () => void undo(goalId, useId) })
      }

      return goal
    }
    catch (caught) {
      failure.value = report(caught, { feature: 'goals', action: 'logUse' })
      return null
    }
    finally {
      isLogging.value = false
    }
  }

  /** Takes the use back, or says it is too late; either way the screen is read again. */
  async function undo(goalId: string, useId: string): Promise<boolean> {
    try {
      await api.goals.undoUse(goalId, useId)
      toast.show(t.value.toast.useUndone)
      return true
    }
    catch (caught) {
      // Refused because the moment has passed: an answer, not an incident.
      const refused = report(caught, { feature: 'goals', action: 'undoUse' })
      if (refused.kind === 'validation') toast.show(t.value.toast.undoTooLate)
      return false
    }
    finally {
      await onUndone()
    }
  }

  return {
    isLogging: computed(() => isLogging.value),
    error: computed(() => failure.value),
    logUse,
    undo,
  }
}
