import type { ApiCaller } from './client'
import type { DeliveredProof, FeedProof, OwnProof, Proof, ProofVoteValue, KudosKind } from './types'

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
   */
  submit: (goalId: string, imageId: string, capturedInApp: boolean) => Promise<DeliveredProof>

  get: (id: string) => Promise<Proof>

  /** The photographs waiting for this person's vote. */
  pending: () => Promise<FeedProof[]>

  /** Every photograph this person has delivered, newest first, whatever became of it. */
  mine: () => Promise<OwnProof[]>

  /** One say per person, and it stands. */
  vote: (id: string, value: ProofVoteValue) => Promise<Proof>

  /** The same kind twice takes it back. */
  react: (id: string, kind: KudosKind) => Promise<Proof>
}

export function createProofsApi(call: ApiCaller): ProofsApi {
  const proof = (id: string) => `/api/proofs/${encodeURIComponent(id)}`

  return {
    submit: (goalId, imageId, capturedInApp) =>
      call<DeliveredProof>(`/api/goals/${encodeURIComponent(goalId)}/proof`, {
        method: 'POST',
        body: { imageId, capturedInApp },
      }),

    get: id => call<Proof>(proof(id), { method: 'GET' }),

    pending: () => call<FeedProof[]>('/api/proofs/pending', { method: 'GET' }),

    mine: () => call<OwnProof[]>('/api/proofs/mine', { method: 'GET' }),

    vote: (id, value) => call<Proof>(`${proof(id)}/vote`, { method: 'POST', body: { value } }),

    react: (id, kind) => call<Proof>(`${proof(id)}/reactions`, { method: 'POST', body: { kind } }),
  }
}
