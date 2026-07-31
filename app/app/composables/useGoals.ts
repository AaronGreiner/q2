import type { ApiFailure } from '~/api/errors'
import type { CreateGoalRequest, Goal, GoalTask } from '~/api/types'

interface GoalsPayload {
  goals: Goal[]
  tasks: GoalTask[]
  failure: ApiFailure | null
}

/**
 * The goals screen: today's tasks under one tab, the goals themselves under
 * the other.
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
        const [goals, tasks] = await Promise.all([api.goals.list(), api.tasks.list()])
        return { goals, tasks, failure: null }
      }
      catch (caught) {
        return { goals: [], tasks: [], failure: report(caught, { feature: 'goals', action: 'list' }) }
      }
    },
    { default: (): GoalsPayload => ({ goals: [], tasks: [], failure: null }) },
  )

  const goals = computed(() => data.value?.goals ?? [])
  const tasks = computed(() => data.value?.tasks ?? [])
  const error = computed(() => data.value?.failure ?? null)
  const isLoading = computed(() => status.value === 'pending')

  const isCreating = ref(false)
  const createError = ref<ApiFailure | null>(null)

  async function toggleTask(id: string) {
    const wasDone = tasks.value.find(task => task.id === id)?.isDone ?? false

    try {
      const updated = await api.tasks.toggle(id)

      // A whole new payload, not a property of the old one: `useAsyncData`
      // returns a shallow ref, so an in-place update never reaches the screen.
      if (data.value) {
        data.value = {
          ...data.value,
          tasks: data.value.tasks.map(task => (task.id === id ? updated : task)),
        }
      }

      if (!wasDone) toast.show(t.value.toast.taskDone)
    }
    catch (caught) {
      report(caught, { feature: 'tasks', action: 'toggle' })
    }
  }

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

  return { goals, tasks, error, isLoading, refresh, toggleTask, create, isCreating, createError }
}

/**
 * One goal, its team and its tasks.
 *
 * Loaded on the server so the page is meaningful without JavaScript and a
 * shared link has the right title.
 */
export function useGoalDetail(id: Ref<string>) {
  const api = useQ2Api()
  const { report } = useErrorReporter()
  const toast = useToastMessage()
  const t = useMessages()

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
  const isContributing = ref(false)

  async function contribute() {
    isContributing.value = true
    try {
      const updated = await api.goals.contribute(id.value)
      toast.show(updated.status === 'Completed' ? t.value.toast.goalReached : t.value.toast.progressSaved)
      await refresh()
    }
    catch (caught) {
      report(caught, { feature: 'goals', action: 'contribute' })
    }
    finally {
      isContributing.value = false
    }
  }

  async function toggleTask(taskId: string) {
    const wasDone = detail.value?.tasks.find(task => task.id === taskId)?.isDone ?? false

    try {
      await api.tasks.toggle(taskId)
      if (!wasDone) toast.show(t.value.toast.taskDone)
      await refresh()
    }
    catch (caught) {
      report(caught, { feature: 'tasks', action: 'toggle' })
    }
  }

  return { detail, error, isMissing, isLoading, refresh, contribute, isContributing, toggleTask }
}
