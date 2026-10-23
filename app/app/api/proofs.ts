import type { ApiCaller } from './client'
import type { DeliveredProof, DoubtReason, FeedProof, OwnProof, Proof, ProofSource, ProofVoteValue, KudosKind } from './types'
import type { ProofPartIds } from '~/utils/captured'

/**
 * Photographs and the votes on them.
 *
 * The half of the product that is not q2. Nothing here takes an image: the
 * bytes go up through `images.upload` first and only the id is delivered, so a
 * slow upload and a refused window are never the same request — and a failed
 * vote never costs somebody their photograph.
 */
export interface ProofsApi {
  /**
   * Delivers a photograph into a goal's open window.
   *
   * On a goal nobody shares it comes back already `Confirmed`: there is nobody
   * to ask. On a shared one it comes back `Voting`, and the window does not
   * move until friends have said so. Either way it names the goal's
   * conversation, where the photograph is shown from now on.
   *
   * `source` is the route the camera screen used — live or a picture from the
   * phone — and is shown to everybody who sees the photograph. A clip and the
   * front camera's picture travel as ids beside the main one, and are only
   * ever live (docs/adr/0060-video-and-both-cameras.md).
   */
  submit: (goalId: string, parts: ProofPartIds, source: ProofSource) => Promise<DeliveredProof>

  /**
   * Answers the spot check open on your own Don't with a live photograph,
   * uploaded as a proof image first. Stored as live whatever is sent, and voted
   * on like any proof — without a second attempt (docs/adr/0038-spot-checks.md).
   */
  answerSpotCheck: (goalId: string, checkId: string, parts: ProofPartIds) => Promise<DeliveredProof>

  get: (id: string) => Promise<Proof>

  /** The photographs waiting for this person's vote. */
  pending: () => Promise<FeedProof[]>

  /** Every photograph this person has delivered, newest first, whatever became of it. */
  mine: () => Promise<OwnProof[]>

  /** One say per person, and it stands. */
  /** With a doubt, optionally what was missing (#111). */
  vote: (id: string, value: ProofVoteValue, reason?: DoubtReason | null) => Promise<Proof>

  /** The same kind twice takes it back. */
  react: (id: string, kind: KudosKind) => Promise<Proof>
}

export function createProofsApi(call: ApiCaller): ProofsApi {
  const proof = (id: string) => `/api/proofs/${encodeURIComponent(id)}`

  return {
    submit: (goalId, parts, source) =>
      call<DeliveredProof>(`/api/goals/${encodeURIComponent(goalId)}/proof`, {
        method: 'POST',
        body: { ...parts, source },
      }),

    answerSpotCheck: (goalId, checkId, parts) =>
      call<DeliveredProof>(
        `/api/goals/${encodeURIComponent(goalId)}/spot-checks/${encodeURIComponent(checkId)}/proof`,
        { method: 'POST', body: parts },
      ),

    get: id => call<Proof>(proof(id), { method: 'GET' }),

    pending: () => call<FeedProof[]>('/api/proofs/pending', { method: 'GET' }),

    mine: () => call<OwnProof[]>('/api/proofs/mine', { method: 'GET' }),

    vote: (id, value, reason) => call<Proof>(`${proof(id)}/vote`, { method: 'POST', body: { value, ...(reason ? { reason } : {}) } }),

    react: (id, kind) => call<Proof>(`${proof(id)}/reactions`, { method: 'POST', body: { kind } }),
  }
}
