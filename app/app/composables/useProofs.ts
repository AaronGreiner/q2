import type { ApiFailure } from '~/api/errors'
import type { FeedProof, Image, ProofVoteValue } from '~/api/types'
import { downscaleForUpload, uploadSizes } from '~/utils/images'

interface PendingPayload {
  proofs: FeedProof[]
  failure: ApiFailure | null
}

/**
 * The photographs waiting for this person's vote.
 *
 * One at a time rather than a scrolling list: a verdict deserves the whole
 * screen, and a list invites tapping "confirm" down the column without looking
 * — which is exactly the inattention the vote exists to prevent. The card
 * leaving after a vote is the feedback.
 */
export function usePendingProofs() {
  const api = useQ2Api()
  const { report } = useErrorReporter()
  const toast = useToastMessage()
  const t = useMessages()

  const { data, status, refresh } = useAsyncData<PendingPayload>(
    'proofs-pending',
    async () => {
      try {
        return { proofs: await api.proofs.pending(), failure: null }
      }
      catch (caught) {
        return { proofs: [], failure: report(caught, { feature: 'proofs', action: 'pending' }) }
      }
    },
    { default: () => ({ proofs: [], failure: null }) },
  )

  const isVoting = ref(false)

  /**
   * Records a verdict and drops the card.
   *
   * The card is removed rather than the list refreshed: a vote is final, so the
   * only thing the server could tell us is what we already know — and a
   * round trip between "confirm" and the next photograph is a round trip
   * somebody spends looking at a card they have finished with.
   */
  async function vote(id: string, value: ProofVoteValue) {
    if (isVoting.value) return

    isVoting.value = true

    try {
      await api.proofs.vote(id, value)

      if (data.value) {
        data.value = {
          ...data.value,
          proofs: data.value.proofs.filter(card => card.proof.id !== id),
        }
      }

      toast.show(t.value.toast.voteCast)
    }
    catch (caught) {
      report(caught, { feature: 'proofs', action: 'vote' })

      // Something is out of step — the vote closed, or somebody got there
      // first. Reloading is the honest answer rather than leaving a card that
      // cannot be voted on.
      await refresh()
    }
    finally {
      isVoting.value = false
    }
  }

  return {
    proofs: computed(() => data.value?.proofs ?? []),
    error: computed(() => data.value?.failure ?? null),
    isLoading: computed(() => status.value === 'pending'),
    isVoting: computed(() => isVoting.value),
    refresh,
    vote,
  }
}

/**
 * Delivering a photograph: shrink it, upload it, hand the id to the goal.
 *
 * Two requests rather than one, and the split is deliberate — the bytes are the
 * slow part and the delivery is the part that can be refused, so a full window
 * never costs somebody their upload and a dropped connection never leaves half
 * a photograph attached to a goal.
 */
export function useProofDelivery() {
  const api = useQ2Api()
  const { report } = useErrorReporter()
  const toast = useToastMessage()
  const t = useMessages()

  const isDelivering = ref(false)

  /**
   * Sends an already-uploaded photograph. Returns true when the goal took it.
   *
   * `PhotoCapture` has done the shrinking and the upload by the time this runs,
   * which is why it takes an <see cref="Image"/> rather than a file.
   */
  async function deliver(goalId: string, image: Image) {
    if (isDelivering.value) return false

    isDelivering.value = true

    try {
      const proof = await api.proofs.submit(goalId, image.id, true)

      // A goal nobody shares is believed at once; a shared one is now waiting.
      // Saying which is the difference between "done" and "handed in".
      toast.show(proof.status === 'Confirmed' ? t.value.toast.proofConfirmed : t.value.toast.proofDelivered)
      return true
    }
    catch (caught) {
      report(caught, { feature: 'proofs', action: 'deliver' })
      return false
    }
    finally {
      isDelivering.value = false
    }
  }

  return {
    isDelivering: computed(() => isDelivering.value),
    deliver,
    /** The longest edge a proof photograph is uploaded at. */
    maxEdge: uploadSizes.proof,
    downscale: downscaleForUpload,
  }
}
