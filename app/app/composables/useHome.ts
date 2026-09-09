import type { ApiFailure } from '~/api/errors'
import type { Activity, Goal, Profile } from '~/api/types'

interface HomePayload {
  profile: Profile | null
  due: Goal[]
  goals: Goal[]
  feed: Activity[]
  failure: ApiFailure | null
}

/**
 * Everything the start screen shows, and the two things it can do.
 *
 * The four reads go out together rather than one after another: on a phone
 * connection four round trips in sequence is four chances to draw a header with
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
        const [profile, due, goals, feed] = await Promise.all([
          api.profile.get(),
          api.goals.today(),
          api.goals.list({ status: 'Active' }),
          api.activity.feed(),
        ])
        return { profile, due, goals, feed, failure: null }
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
  const due = computed(() => data.value?.due ?? [])
  const goals = computed(() => data.value?.goals ?? [])
  const feed = computed(() => data.value?.feed ?? [])
  const error = computed(() => data.value?.failure ?? null)
  const isLoading = computed(() => status.value === 'pending')

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

  return { profile, due, goals, feed, error, isLoading, refresh, toggleKudos }
}

function empty(): HomePayload {
  return { profile: null, due: [], goals: [], feed: [], failure: null }
}

/**
 * The whole feed, for the overview behind the bell.
 *
 * A separate read from `useHome` rather than a slice of it: the start screen
 * asks for a dashboard and this asks for one list, and sharing an async-data
 * key would make opening the overview refetch four endpoints.
 */
export function useActivityOverview() {
  const api = useQ2Api()
  const { report } = useErrorReporter()
  const toast = useToastMessage()
  const t = useMessages()

  const { data, status, refresh } = useAsyncData(
    'activity-overview',
    async () => {
      try {
        return { feed: await api.activity.feed(), failure: null }
      }
      catch (caught) {
        return { feed: [] as Activity[], failure: report(caught, { feature: 'feed', action: 'load' }) }
      }
    },
    { default: () => ({ feed: [] as Activity[], failure: null }) },
  )

  async function toggleKudos(id: string) {
    try {
      const updated = await api.activity.toggleKudos(id)

      // The whole payload, not a field of it: `useAsyncData` hands back a
      // shallow ref, so mutating in place changes nothing anybody is watching.
      if (data.value) {
        data.value = {
          ...data.value,
          feed: data.value.feed.map(entry => (entry.id === id ? updated : entry)),
        }
      }

      if (updated.hasMyKudos) toast.show(t.value.toast.kudosSent)
    }
    catch (caught) {
      report(caught, { feature: 'feed', action: 'kudos' })
    }
  }

  return {
    feed: computed(() => data.value?.feed ?? []),
    error: computed(() => data.value?.failure ?? null),
    isLoading: computed(() => status.value === 'pending'),
    refresh,
    toggleKudos,
  }
}
