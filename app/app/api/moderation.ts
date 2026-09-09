import type { ApiCaller } from './client'
import type { Person, ReportReason, ReportReceipt, ReportTargetKind } from './types'

/**
 * Blocking somebody, and asking for something to be looked at.
 *
 * Two features in one client because they are one gesture on screen: the sheet
 * that offers "melden" also offers "blockieren", and somebody who has just been
 * shown something they did not want to see should not have to find the second
 * one in a different place.
 */
export interface ModerationApi {
  /**
   * Asks for a person, a photograph or a challenge contribution to be looked
   * at. There is deliberately no way to read a report back.
   */
  report: (
    targetKind: ReportTargetKind,
    targetId: string,
    reason: ReportReason,
    note?: string,
  ) => Promise<ReportReceipt>

  /** The people you have blocked. Never the ones who blocked you. */
  blocked: () => Promise<Person[]>

  /**
   * Blocks somebody and ends whatever connection there was. Answers with the
   * list afterwards, so the screen behind it never has to ask again.
   */
  block: (personId: string) => Promise<Person[]>

  /** Lifts a block you set. The friendship does not come back. */
  unblock: (personId: string) => Promise<Person[]>
}

export function createModerationApi(call: ApiCaller): ModerationApi {
  const block = (id: string) => `/api/blocks/${encodeURIComponent(id)}`

  return {
    report: (targetKind, targetId, reason, note) =>
      call<ReportReceipt>('/api/reports', {
        method: 'POST',
        body: { targetKind, targetId, reason, note: note || undefined },
      }),

    blocked: () => call<Person[]>('/api/blocks', { method: 'GET' }),

    block: personId => call<Person[]>(block(personId), { method: 'POST', body: {} }),

    unblock: personId => call<Person[]>(block(personId), { method: 'DELETE' }),
  }
}

/**
 * Your invite link, and replacing it.
 *
 * The server hands back the code alone — it does not know which host the app is
 * served from, and a link with the wrong origin in it is worse than none. The
 * link is built in the browser from `window.location.origin`.
 */
export interface InviteApi {
  get: () => Promise<{ code: string }>
  regenerate: () => Promise<{ code: string }>
}

export function createInviteApi(call: ApiCaller): InviteApi {
  return {
    get: () => call<{ code: string }>('/api/invite', { method: 'GET' }),
    regenerate: () => call<{ code: string }>('/api/invite/regenerate', { method: 'POST', body: {} }),
  }
}
