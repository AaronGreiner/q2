import type { ApiFailure } from '~/api/errors'
import type { FeedProof, Image, ProofVoteValue } from '~/api/types'
import { isFirstLoad, placeholder } from '~/utils/firstLoad'
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
    { default: () => placeholder({ proofs: [], failure: null }) },
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
    isLoading: computed(() => isFirstLoad(status.value, data.value)),
    isVoting: computed(() => isVoting.value),
    refresh,
    vote,
  }
}

/**
 * What became of a delivery, for the screen that asked for it.
 *
 * - `moved`: the goal has a conversation and the owner has been taken there,
 *   because that is where the photograph is shown and voted on.
 * - `stayed`: it was taken, and the screen is still the one it was delivered
 *   from — the goal's own conversation, or a goal that has none.
 * - `refused`: it was not taken. `message` says why, in the reader's language,
 *   and the photograph is still uploaded, so trying again costs a tap.
 */
export type DeliveryResult
  = | { status: 'moved' }
    | { status: 'stayed' }
    | { status: 'refused', message: string }

/**
 * Delivering a photograph: shrink it, upload it, hand the id to the goal, and
 * show the owner where it went.
 *
 * Two requests rather than one, and the split is deliberate — the bytes are the
 * slow part and the delivery is the part that can be refused, so a full window
 * never costs somebody their upload and a dropped connection never leaves half
 * a photograph attached to a goal.
 *
 * **A delivery never ends in silence.** The row that offered the camera stops
 * offering it the moment a photograph is in, so without a next step the
 * photograph seems to vanish: taken, and then nowhere. So a goal with a
 * conversation takes its owner there, to the card their friends are about to
 * vote on (docs/adr/0027-goal-conversations.md); a goal without one says in a
 * toast what became of it; and a refusal comes back as a sentence the camera
 * screen shows under the photograph it is still holding.
 */
export function useProofDelivery() {
  const api = useQ2Api()
  const { report } = useErrorReporter()
  const toast = useToastMessage()
  const t = useMessages()
  const route = useRoute()

  const isDelivering = ref(false)

  /**
   * Sends an already-uploaded photograph and goes where it can be seen.
   *
   * `PhotoCapture` has done the shrinking and the upload by the time this runs,
   * which is why it takes an `Image` rather than a file.
   */
  async function deliver(goalId: string, image: Image): Promise<DeliveryResult> {
    if (isDelivering.value) return { status: 'refused', message: t.value.proof.refusedFailed }

    isDelivering.value = true

    try {
      const { proof, conversationId } = await api.proofs.submit(goalId, image.id, true)
      const conversation = conversationId ? `/chats/${conversationId}` : null

      if (conversation && route.path !== conversation) {
        await navigateTo(conversation)
        return { status: 'moved' }
      }

      // Already in the conversation, the card arriving in the thread says it.
      if (!conversation) {
        toast.show(proof.status === 'Voting' ? t.value.toast.proofWaiting : t.value.toast.proofCounted)
      }

      return { status: 'stayed' }
    }
    catch (caught) {
      const failure = report(caught, { feature: 'proofs', action: 'deliver' })
      return { status: 'refused', message: refusal(failure) }
    }
    finally {
      isDelivering.value = false
    }
  }

  /**
   * Why a delivery was not taken. A window that closed or filled while the
   * screen was open is the common case, and trying again will not change it;
   * a dropped connection is the other, and trying again is the whole answer.
   */
  function refusal(failure: ApiFailure): string {
    switch (failure.kind) {
      case 'validation':
      case 'conflict':
      case 'notFound':
        return t.value.proof.refusedClosed
      case 'network':
        return t.value.proof.refusedOffline
      default:
        return t.value.proof.refusedFailed
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
