import type { ApiFailure } from '~/api/errors'
import type { CreateGoalRequest, Goal, GoalStatus } from '~/api/types'

interface GoalsPayload {
  goals: Goal[]
  failure: ApiFailure | null
}

/**
 * Loading, filtering and creating goals.
 *
 * Pages compose views; this is where the state lives. It exposes the states the
 * UI has to render — loading, empty, error, loaded — as separate flags instead
 * of leaving every component to infer them from `data === undefined`.
 *
 * Note that the failure travels *inside* the async data rather than in a
 * separate ref: everything `useAsyncData` returns is serialised into the SSR
 * payload and revived in the browser, while a ref set during server rendering
 * would simply be back to `null` after hydration — and the error state would
 * vanish on the client.
 */
export function useGoals(statusFilter: Ref<GoalStatus | undefined>) {
  const api = useGoalsApi()
  const { report } = useErrorReporter()

  const { data, status, refresh } = useAsyncData<GoalsPayload>(
    'goals',
    async () => {
      try {
        return { goals: await api.list({ status: statusFilter.value }), failure: null }
      }
      catch (caught) {
        // Handled rather than thrown: a failed list is an in-page error state,
        // not a reason to replace the whole app with an error screen.
        return { goals: [], failure: report(caught, { feature: 'goals', action: 'list' }) }
      }
    },
    {
      default: (): GoalsPayload => ({ goals: [], failure: null }),
      watch: [statusFilter],
    },
  )

  const goals = computed<Goal[]>(() => data.value?.goals ?? [])
  const error = computed<ApiFailure | null>(() => data.value?.failure ?? null)
  const isLoading = computed(() => status.value === 'pending')
  const isEmpty = computed(() => !isLoading.value && error.value === null && goals.value.length === 0)

  const isCreating = ref(false)
  const createError = ref<ApiFailure | null>(null)

  /**
   * Creates a goal and refreshes the list.
   * Returns the created goal, or `null` when the request failed — the reason
   * is in {@link createError}, including field-level messages.
   */
  async function create(request: CreateGoalRequest): Promise<Goal | null> {
    isCreating.value = true
    createError.value = null

    try {
      const created = await api.create(request)
      await refresh()
      return created
    }
    catch (caught) {
      createError.value = report(caught, { feature: 'goals', action: 'create' })
      return null
    }
    finally {
      isCreating.value = false
    }
  }

  return {
    goals,
    isLoading,
    isEmpty,
    error,
    refresh,
    create,
    isCreating,
    createError,
  }
}
