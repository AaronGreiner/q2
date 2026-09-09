import type { ApiFailure } from '~/api/errors'
import type { ChallengeArchiveEntry, ChallengeRoom, Image, KudosKind } from '~/api/types'
import { uploadSizes } from '~/utils/images'

interface RoomPayload {
  room: ChallengeRoom | null
  failure: ApiFailure | null
}

/**
 * Today's challenge room.
 *
 * Almost nothing is decided here. Who is in the room, whether the pictures come
 * with it and whether a reaction is allowed are all answered by the server —
 * this only says what happens on screen while the answer is on its way.
 *
 * The one rule that does live here is that every write replaces the whole
 * payload with what came back. Contributing changes the room in a way no client
 * could guess: friends' pictures that were not in the previous response arrive
 * with it, because contributing is what earns them.
 */
export function useChallengeRoom() {
  const api = useQ2Api()
  const { report } = useErrorReporter()
  const toast = useToastMessage()
  const t = useMessages()

  const { data, status, refresh } = useAsyncData<RoomPayload>(
    'challenge-today',
    async () => {
      try {
        return { room: (await api.challenges.today()).room, failure: null }
      }
      catch (caught) {
        return { room: null, failure: report(caught, { feature: 'challenge', action: 'today' }) }
      }
    },
    { default: () => ({ room: null, failure: null }) },
  )

  const isSubmitting = ref(false)

  /**
   * Contributes an already-uploaded photograph.
   *
   * `PhotoCapture` has done the shrinking and the upload by the time this runs,
   * which is why it takes an `Image` rather than a file — the same split as a
   * proof, so a dropped connection never leaves half a picture in the room.
   */
  async function contribute(image: Image) {
    if (isSubmitting.value) return

    isSubmitting.value = true

    try {
      const room = await api.challenges.submit(image.id, true)
      data.value = { room, failure: null }
      toast.show(t.value.toast.challengeJoined)
    }
    catch (caught) {
      report(caught, { feature: 'challenge', action: 'contribute' })

      // Something moved underneath us — most likely midnight passed while the
      // camera was open. Reloading is the honest answer.
      await refresh()
    }
    finally {
      isSubmitting.value = false
    }
  }

  async function withdraw() {
    if (isSubmitting.value) return

    isSubmitting.value = true

    try {
      data.value = { room: (await api.challenges.withdraw()).room, failure: null }
      toast.show(t.value.toast.challengeWithdrawn)
    }
    catch (caught) {
      report(caught, { feature: 'challenge', action: 'withdraw' })
      await refresh()
    }
    finally {
      isSubmitting.value = false
    }
  }

  /**
   * Adds or takes back a reaction, and puts the updated contribution in place.
   *
   * The whole payload is replaced rather than the entry mutated: `useAsyncData`
   * hands back a shallow ref, so changing a nested object changes nothing
   * anybody is watching.
   */
  async function react(entryId: string, kind: KudosKind) {
    try {
      const updated = await api.challenges.react(entryId, kind)
      const current = data.value?.room

      if (!current) return

      data.value = {
        failure: null,
        room: {
          ...current,
          ownEntry: current.ownEntry?.id === entryId ? updated : current.ownEntry,
          entries: current.entries.map(entry => (entry.id === entryId ? updated : entry)),
        },
      }
    }
    catch (caught) {
      report(caught, { feature: 'challenge', action: 'react' })
    }
  }

  return {
    room: computed(() => data.value?.room ?? null),
    error: computed(() => data.value?.failure ?? null),
    isLoading: computed(() => status.value === 'pending'),
    isSubmitting: computed(() => isSubmitting.value),
    refresh,
    contribute,
    withdraw,
    react,

    /** The longest edge a contribution is uploaded at. */
    maxEdge: uploadSizes.proof,
  }
}

/**
 * Your own challenge archive.
 *
 * A separate read from the room rather than a slice of it: the room is about
 * today and this is about every day, and sharing an async-data key would make
 * opening the archive refetch the room.
 */
export function useChallengeArchive() {
  const api = useQ2Api()
  const { report } = useErrorReporter()

  const { data, status, refresh } = useAsyncData(
    'challenge-archive',
    async () => {
      try {
        return { entries: await api.challenges.archive(), failure: null as ApiFailure | null }
      }
      catch (caught) {
        return {
          entries: [] as ChallengeArchiveEntry[],
          failure: report(caught, { feature: 'challenge', action: 'archive' }),
        }
      }
    },
    { default: () => ({ entries: [] as ChallengeArchiveEntry[], failure: null as ApiFailure | null }) },
  )

  return {
    entries: computed(() => data.value?.entries ?? []),
    error: computed(() => data.value?.failure ?? null),
    isLoading: computed(() => status.value === 'pending'),
    refresh,
  }
}
