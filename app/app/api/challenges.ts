import type { ApiCaller } from './client'
import type { ChallengeEntry, ChallengeArchiveEntry, ChallengeRoom, ChallengeToday, KudosKind } from './types'

/**
 * The daily challenge: today's prompt, the room, and your own archive.
 *
 * Everything about today is addressed as `today` rather than by an id. There is
 * exactly one challenge running at any moment, and a client that had to name it
 * could name yesterday's.
 *
 * Like proofs, nothing here takes an image: the bytes go up through
 * `images.upload` first and only the id is contributed, so a slow upload and a
 * refused contribution are never the same request.
 */
export interface ChallengesApi {
  /**
   * Today's room, or `{ room: null }` when nothing is running.
   *
   * Which contributions come back is the server's decision twice over: only
   * the viewer's own friends are in it at all, and their pictures arrive only
   * once the viewer has contributed one themselves.
   */
  today: () => Promise<ChallengeToday>

  /** Contributes a photograph. A second one replaces the first. */
  submit: (imageId: string, capturedInApp: boolean) => Promise<ChallengeRoom>

  /** Takes your contribution back. The room is covered again afterwards. */
  withdraw: () => Promise<ChallengeToday>

  /** The same kind twice takes it back. */
  react: (entryId: string, kind: KudosKind) => Promise<ChallengeEntry>

  /** Every challenge you have taken part in. Your own contributions only. */
  archive: () => Promise<ChallengeArchiveEntry[]>
}

export function createChallengesApi(call: ApiCaller): ChallengesApi {
  const entry = '/api/challenges/today/entry'

  return {
    today: () => call<ChallengeToday>('/api/challenges/today', { method: 'GET' }),

    submit: (imageId, capturedInApp) =>
      call<ChallengeRoom>(entry, { method: 'POST', body: { imageId, capturedInApp } }),

    withdraw: () => call<ChallengeToday>(entry, { method: 'DELETE' }),

    react: (entryId, kind) =>
      call<ChallengeEntry>(`/api/challenges/entries/${encodeURIComponent(entryId)}/reactions`, {
        method: 'POST',
        body: { kind },
      }),

    archive: () => call<ChallengeArchiveEntry[]>('/api/challenges/archive', { method: 'GET' }),
  }
}
