import type { ApiFailure } from '~/api/errors'
import type { Friends, PersonSearchResult, Profile, UpdateProfileRequest } from '~/api/types'
import { isFirstLoad, placeholder } from '~/utils/firstLoad'

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
    { default: (): FriendsPayload => placeholder({ friends: empty(), failure: null }) },
  )

  const friends = computed(() => data.value?.friends.friends ?? [])
  const requests = computed(() => data.value?.friends.requests ?? [])
  const sentRequests = computed(() => data.value?.friends.sentRequests ?? [])
  const suggestions = computed(() => data.value?.friends.suggestions ?? [])

  /** The tab bar counts pending requests, and several of these change it. */
  async function reload() {
    await Promise.all([refresh(), refreshNuxtData('counts')])
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

  const accept = (personId: string) =>
    run('accept', async () => {
      await api.friends.accept(personId)
    })

  const decline = (personId: string) =>
    run('decline', () => api.friends.decline(personId))

  const request = (personId: string) =>
    run('request', async () => {
      await api.friends.request(personId)
    })

  const withdraw = (personId: string) =>
    run('withdraw', () => api.friends.withdraw(personId))

  return {
    friends,
    requests,
    sentRequests,
    suggestions,
    error: computed(() => data.value?.failure ?? null),
    isLoading: computed(() => isFirstLoad(status.value, data.value)),
    refresh: reload,
    accept,
    decline,
    request,
    withdraw,
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
 * The signed-in person's own profile: stats, badges and recent activity — and
 * the two things about it somebody can change.
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
    { default: () => placeholder({ profile: null, failure: null }) },
  )

  const isSaving = ref(false)

  /**
   * Puts the answer in place of what was there.
   *
   * `useAsyncData` hands back a **shallow** ref, so assigning to a property of
   * `data.value` changes the object without telling anything watching it: the
   * request succeeds and the screen does not move.
   */
  function put(profile: Profile) {
    data.value = { profile, failure: null }
  }

  async function update(request: UpdateProfileRequest) {
    if (isSaving.value) return false

    isSaving.value = true

    try {
      put(await api.profile.update(request))
      return true
    }
    catch (caught) {
      report(caught, { feature: 'profile', action: 'update' })
      return false
    }
    finally {
      isSaving.value = false
    }
  }

  return {
    profile: computed(() => data.value?.profile ?? null),
    error: computed(() => data.value?.failure ?? null),
    isLoading: computed(() => isFirstLoad(status.value, data.value)),
    isSaving: computed(() => isSaving.value),
    refresh,

    rename: (displayName: string) => update({ displayName }),

    chooseAvatar: (imageId: string) => update({ avatarImageId: imageId }),

    /**
     * Deletes the picture itself rather than merely unpointing the profile at
     * it. That is the whole "remove" path in q2: an image nothing points at
     * would still be sitting in somebody's storage allowance, and the profile
     * reference is cleared by the same request (ImageService.DeleteAsync).
     */
    async removeAvatar(imageId: string) {
      if (isSaving.value) return

      isSaving.value = true

      try {
        await api.images.remove(imageId)
        put(await api.profile.get())
      }
      catch (caught) {
        report(caught, { feature: 'images', action: 'delete' })
      }
      finally {
        isSaving.value = false
      }
    },
  }
}

/**
 * Somebody else's profile.
 *
 * The balance it carries is scoped by the server to the goals the two people
 * share. Nothing here narrows it, and nothing here may: a screen that filtered
 * would be a screen that could stop filtering.
 */
export function usePersonProfile(id: Ref<string>) {
  const api = useQ2Api()
  const { report } = useErrorReporter()

  const { data, status, refresh } = useAsyncData(
    () => `person:${id.value}`,
    async () => {
      try {
        return { person: await api.profile.person(id.value), failure: null }
      }
      catch (caught) {
        return { person: null, failure: report(caught, { feature: 'people', action: 'load' }) }
      }
    },
    {
      // Without this, tapping from one search result to another leaves the
      // first person's numbers on screen under the second person's name.
      watch: [id],
      default: () => placeholder({ person: null, failure: null }),
    },
  )

  const failure = computed(() => data.value?.failure ?? null)
  const isRemoving = ref(false)

  /**
   * Ends the friendship from their profile — the place it is decided, rather
   * than a button beside "Nachricht schreiben" in the friends list. The list
   * and its counts are read again, and this profile too, because what it may
   * show has just changed.
   */
  async function removeFriend(): Promise<boolean> {
    if (isRemoving.value) return false
    isRemoving.value = true

    try {
      await api.friends.remove(id.value)
      await Promise.all([refresh(), refreshNuxtData(['friends', 'counts'])])
      return true
    }
    catch (caught) {
      report(caught, { feature: 'friends', action: 'remove' })
      return false
    }
    finally {
      isRemoving.value = false
    }
  }

  return {
    person: computed(() => data.value?.person ?? null),

    // A stale link is an ordinary outcome, so it gets its own calm state rather
    // than the generic "something went wrong".
    isMissing: computed(() => failure.value?.kind === 'notFound'),
    error: computed(() => (failure.value && failure.value.kind !== 'notFound' ? failure.value : null)),
    isLoading: computed(() => isFirstLoad(status.value, data.value)),
    refresh,
    removeFriend,
    isRemoving: computed(() => isRemoving.value),
  }
}
