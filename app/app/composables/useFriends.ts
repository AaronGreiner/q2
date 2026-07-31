import type { ApiFailure } from '~/api/errors'
import type { Friends, PersonSearchResult } from '~/api/types'

interface FriendsPayload {
  friends: Friends
  failure: ApiFailure | null
}

/**
 * The friends screen: who you know, who is waiting, and who you might know.
 *
 * Suggestions and mutual counts are derived by the server from the friend
 * graph, so every one of these actions changes them — which is why the whole
 * screen is reloaded after a write rather than patched in place. One read is
 * cheap and cannot disagree with itself.
 */
export function useFriends() {
  const api = useQ2Api()
  const { report } = useErrorReporter()
  const toast = useToastMessage()
  const t = useMessages()

  const { data, status, refresh } = useAsyncData<FriendsPayload>(
    'friends',
    async () => {
      try {
        return { friends: await api.friends.get(), failure: null }
      }
      catch (caught) {
        return { friends: empty(), failure: report(caught, { feature: 'friends', action: 'list' }) }
      }
    },
    { default: (): FriendsPayload => ({ friends: empty(), failure: null }) },
  )

  const friends = computed(() => data.value?.friends.friends ?? [])
  const requests = computed(() => data.value?.friends.requests ?? [])
  const sentRequests = computed(() => data.value?.friends.sentRequests ?? [])
  const suggestions = computed(() => data.value?.friends.suggestions ?? [])

  /** The tab bar counts pending requests, and several of these change it. */
  async function reload() {
    await Promise.all([refresh(), refreshNuxtData('profile')])
  }

  async function run(action: string, work: () => Promise<void>) {
    try {
      await work()
      await reload()
    }
    catch (caught) {
      report(caught, { feature: 'friends', action })
    }
  }

  async function accept(personId: string) {
    const name = requests.value.find(request => request.person.id === personId)?.person.displayName

    await run('accept', async () => {
      await api.friends.accept(personId)
      if (name) toast.show(t.value.toast.friendAdded(name), { private: true })
    })
  }

  const decline = (personId: string) =>
    run('decline', () => api.friends.decline(personId))

  const request = (personId: string) =>
    run('request', async () => {
      await api.friends.request(personId)
      toast.show(t.value.toast.requestSent)
    })

  const withdraw = (personId: string) =>
    run('withdraw', async () => {
      await api.friends.withdraw(personId)
      toast.show(t.value.toast.requestWithdrawn)
    })

  const remove = (personId: string) =>
    run('remove', async () => {
      await api.friends.remove(personId)
      toast.show(t.value.toast.friendRemoved)
    })

  return {
    friends,
    requests,
    sentRequests,
    suggestions,
    error: computed(() => data.value?.failure ?? null),
    isLoading: computed(() => status.value === 'pending'),
    refresh: reload,
    accept,
    decline,
    request,
    withdraw,
    remove,
  }
}

/**
 * Finding people, anywhere in q2.
 *
 * Client-side rather than through `useAsyncData`: a search box is not part of
 * a page's initial state, and server-rendering results for a term nobody has
 * typed yet would be work thrown away on every first paint.
 */
export function usePersonSearch(query: Ref<string>) {
  const api = useQ2Api()
  const { report } = useErrorReporter()

  /** Mirrors FriendsService.MinimumSearchLength on the server. */
  const minimumLength = 2

  const results = ref<PersonSearchResult[]>([])
  const isSearching = ref(false)
  const failure = ref<ApiFailure | null>(null)

  const term = computed(() => query.value.trim())
  const isActive = computed(() => term.value.length >= minimumLength)
  let revision = 0

  async function search() {
    const currentRevision = ++revision

    if (!isActive.value) {
      results.value = []
      failure.value = null
      isSearching.value = false
      return
    }

    const requestedTerm = term.value
    isSearching.value = true

    try {
      const found = await api.friends.search(requestedTerm)
      if (currentRevision !== revision) return

      results.value = found
      failure.value = null
    }
    catch (caught) {
      if (currentRevision !== revision) return

      results.value = []
      failure.value = report(caught, { feature: 'friends', action: 'search' })
    }
    finally {
      if (currentRevision === revision) isSearching.value = false
    }
  }

  watch(term, search)

  return { results, isSearching, isActive, minimumLength, error: failure, search }
}

function empty(): Friends {
  return { friends: [], requests: [], sentRequests: [], suggestions: [] }
}

/**
 * The signed-in person's own profile: stats, badges and recent activity.
 */
export function useProfile() {
  const api = useQ2Api()
  const { report } = useErrorReporter()

  const { data, status, refresh } = useAsyncData(
    'profile',
    async () => {
      try {
        return { profile: await api.profile.get(), failure: null }
      }
      catch (caught) {
        return { profile: null, failure: report(caught, { feature: 'profile', action: 'load' }) }
      }
    },
    { default: () => ({ profile: null, failure: null }) },
  )

  return {
    profile: computed(() => data.value?.profile ?? null),
    error: computed(() => data.value?.failure ?? null),
    isLoading: computed(() => status.value === 'pending'),
    refresh,
  }
}
