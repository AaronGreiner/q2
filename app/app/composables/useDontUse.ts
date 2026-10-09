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
 */
export function useDontUse() {
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
      toast.show(goal.current?.status === 'Relapse' ? t.value.toast.relapseLogged : t.value.toast.useLogged)
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

  return {
    isLogging: computed(() => isLogging.value),
    error: computed(() => failure.value),
    logUse,
  }
}
