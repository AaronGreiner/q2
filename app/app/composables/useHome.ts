import type { ApiFailure } from '~/api/errors'
import type { Activity, Goal, GoalTask, LeaderboardEntry, Profile } from '~/api/types'

interface HomePayload {
  profile: Profile | null
  tasks: GoalTask[]
  goals: Goal[]
  feed: Activity[]
  leaderboard: LeaderboardEntry[]
  failure: ApiFailure | null
}

/**
 * Everything the start screen shows, and the two things it can do.
 *
 * The five reads go out together rather than one after another: on a phone
 * connection five round trips in sequence is five chances to draw a header with
 * nothing under it. One failure fails the screen — a start screen missing its
 * feed but showing its streak would look finished when it is not.
 *
 * The failure travels *inside* the async data rather than in a separate ref:
 * everything `useAsyncData` returns is serialised into the SSR payload, while a
 * ref set during server rendering would simply be back to `null` after
 * hydration — and the error state would vanish on the client.
 */
export function useHome() {
  const api = useQ2Api()
  const { report } = useErrorReporter()
  const toast = useToastMessage()
  const t = useMessages()

  const { data, status, refresh } = useAsyncData<HomePayload>(
    'home',
    async () => {
      try {
        const [profile, tasks, goals, feed, leaderboard] = await Promise.all([
          api.profile.get(),
          api.tasks.list(),
          api.goals.list({ status: 'Active' }),
          api.activity.feed(),
          api.activity.leaderboard(),
        ])
        return { profile, tasks, goals, feed, leaderboard, failure: null }
      }
      catch (caught) {
        return {
          ...empty(),
          failure: report(caught, { feature: 'home', action: 'load' }),
        }
      }
    },
    { default: empty },
  )

  /**
   * Puts a new payload in place of the current one.
   *
   * `useAsyncData` hands back a **shallow** ref, so assigning to a property of
   * `data.value` changes the object without telling anything watching it — the
   * request succeeds and the screen does not move. Replacing the whole payload
   * is what makes an answer visible.
   */
  function replace(update: (payload: HomePayload) => HomePayload) {
    if (data.value) data.value = update(data.value)
  }

  const profile = computed(() => data.value?.profile ?? null)
  const tasks = computed(() => data.value?.tasks ?? [])
  const goals = computed(() => data.value?.goals ?? [])
  const feed = computed(() => data.value?.feed ?? [])
  const leaderboard = computed(() => data.value?.leaderboard ?? [])
  const error = computed(() => data.value?.failure ?? null)
  const isLoading = computed(() => status.value === 'pending')

  /**
   * Ticks a task off. The answer replaces the row straight away; the rest of
   * the screen — the ring, the streak, the feed — is refreshed afterwards,
   * because ticking one task changes all three.
   */
  async function toggleTask(id: string) {
    const wasDone = tasks.value.find(task => task.id === id)?.isDone ?? false

    try {
      const updated = await api.tasks.toggle(id)
      replace(payload => ({ ...payload, tasks: payload.tasks.map(task => (task.id === id ? updated : task)) }))
      if (!wasDone) toast.show(t.value.toast.taskDone)
      await refresh()
    }
    catch (caught) {
      report(caught, { feature: 'tasks', action: 'toggle' })
    }
  }

  async function toggleKudos(id: string) {
    try {
      const updated = await api.activity.toggleKudos(id)
      replace(payload => ({ ...payload, feed: payload.feed.map(entry => (entry.id === id ? updated : entry)) }))
      if (updated.hasMyKudos) toast.show(t.value.toast.kudosSent)
    }
    catch (caught) {
      report(caught, { feature: 'feed', action: 'kudos' })
    }
  }

  return { profile, tasks, goals, feed, leaderboard, error, isLoading, refresh, toggleTask, toggleKudos }
}

function empty(): HomePayload {
  return { profile: null, tasks: [], goals: [], feed: [], leaderboard: [], failure: null }
}
