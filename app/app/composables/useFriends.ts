import type { ApiFailure } from '~/api/errors'
import type { Friends } from '~/api/types'

interface FriendsPayload {
  friends: Friends
  failure: ApiFailure | null
}

/**
 * Friends, requests and suggestions — one read and three answers to it.
 *
 * Search is passed to the server, which applies it to friends and suggestions
 * but never to pending requests: hiding somebody's request behind a search box
 * is how it stays unanswered forever.
 */
export function useFriends(search: Ref<string>) {
  const api = useQ2Api()
  const { report } = useErrorReporter()
  const toast = useToastMessage()
  const t = useMessages()

  const { data, status, refresh } = useAsyncData<FriendsPayload>(
    'friends',
    async () => {
      try {
        return { friends: await api.friends.get({ search: search.value }), failure: null }
      }
      catch (caught) {
        return { friends: empty(), failure: report(caught, { feature: 'friends', action: 'list' }) }
      }
    },
    { watch: [search], default: (): FriendsPayload => ({ friends: empty(), failure: null }) },
  )

  const friends = computed(() => data.value?.friends.friends ?? [])
  const requests = computed(() => data.value?.friends.requests ?? [])
  const suggestions = computed(() => data.value?.friends.suggestions ?? [])

  async function accept(id: string) {
    const name = requests.value.find(request => request.id === id)?.person.displayName

    try {
      await api.friends.accept(id)
      await refresh()
      if (name) toast.show(t.value.toast.friendAdded(name))
    }
    catch (caught) {
      report(caught, { feature: 'friends', action: 'accept' })
    }
  }

  async function decline(id: string) {
    try {
      await api.friends.decline(id)
      await refresh()
    }
    catch (caught) {
      report(caught, { feature: 'friends', action: 'decline' })
    }
  }

  async function request(id: string) {
    try {
      const updated = await api.friends.request(id)

      // A whole new payload, not a nested property: `useAsyncData` returns a
      // shallow ref, and an in-place update would leave the button reading
      // "Hinzufügen" after the request had already been sent.
      if (data.value) {
        data.value = {
          ...data.value,
          friends: {
            ...data.value.friends,
            suggestions: data.value.friends.suggestions
              .map(suggestion => (suggestion.id === id ? updated : suggestion)),
          },
        }
      }

      toast.show(t.value.toast.requestSent)
    }
    catch (caught) {
      report(caught, { feature: 'friends', action: 'request' })
    }
  }

  return {
    friends,
    requests,
    suggestions,
    error: computed(() => data.value?.failure ?? null),
    isLoading: computed(() => status.value === 'pending'),
    refresh,
    accept,
    decline,
    request,
  }
}

function empty(): Friends {
  return { friends: [], requests: [], suggestions: [] }
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
