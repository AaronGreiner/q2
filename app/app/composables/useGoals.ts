import type { ApiFailure } from '~/api/errors'
import type { CreateGoalRequest, Goal } from '~/api/types'

interface GoalsPayload {
  goals: Goal[]
  due: Goal[]
  failure: ApiFailure | null
}

/**
 * The goals screen: what is due today under one tab, the goals themselves
 * under the other.
 *
 * Both tabs are loaded at once. They are two views of the same commitment, the
 * data is small, and a tab that spins for half a second every time it is
 * touched makes the pair feel like two screens instead of one.
 */
export function useGoals() {
  const api = useQ2Api()
  const { report } = useErrorReporter()
  const toast = useToastMessage()
  const t = useMessages()

  const { data, status, refresh } = useAsyncData<GoalsPayload>(
    'goals',
    async () => {
      try {
        /*
         * Running goals only. What has stopped lives in the archive
         * (`useGoalArchive`), and a list that mixed the two would put things
         * with no deadline among the things somebody still owes.
         */
        const [goals, due] = await Promise.all([api.goals.list({ status: 'Active' }), api.goals.today()])
        return { goals, due, failure: null }
      }
      catch (caught) {
        return { goals: [], due: [], failure: report(caught, { feature: 'goals', action: 'list' }) }
      }
    },
    { default: (): GoalsPayload => ({ goals: [], due: [], failure: null }) },
  )

  const goals = computed(() => data.value?.goals ?? [])
  const due = computed(() => data.value?.due ?? [])
  const error = computed(() => data.value?.failure ?? null)
  const isLoading = computed(() => status.value === 'pending')

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
      const created = await api.goals.create(request)
      await refresh()
      toast.show(t.value.toast.goalCreated)
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

  return { goals, due, error, isLoading, refresh, create, isCreating, createError }
}

/**
 * One goal, its team and its closed windows.
 *
 * Loaded on the server so the page is meaningful without JavaScript and a
 * shared link has the right title.
 */
export function useGoalDetail(id: Ref<string>) {
  const api = useQ2Api()
  const { report } = useErrorReporter()

  const { data, status, refresh } = useAsyncData(
    () => `goal:${id.value}`,
    async () => {
      try {
        return { goal: await api.goals.get(id.value), failure: null }
      }
      catch (caught) {
        return { goal: null, failure: report(caught, { feature: 'goals', action: 'detail' }) }
      }
    },
    { watch: [id], default: () => ({ goal: null, failure: null }) },
  )

  const detail = computed(() => data.value?.goal ?? null)
  const failure = computed(() => data.value?.failure ?? null)

  // A missing goal is an ordinary outcome of following a stale link, so it gets
  // its own calm state rather than the generic "something went wrong".
  const isMissing = computed(() => failure.value?.kind === 'notFound')
  const error = computed(() => (failure.value && failure.value.kind !== 'notFound' ? failure.value : null))
  const isLoading = computed(() => status.value === 'pending')

  return { detail, error, isMissing, isLoading, refresh }
}
