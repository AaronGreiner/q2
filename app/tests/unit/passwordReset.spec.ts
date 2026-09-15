import { describe, expect, it } from 'vitest'
import type { ApiFailure } from '~/api/errors'
import { isRefusedResetLink, passwordResetMessage } from '~/composables/usePasswordReset'
import { de } from '~/i18n/messages'

/**
 * What the two reset screens say when something did not work.
 *
 * The server sends structure, never a sentence: a reason for "no mail here",
 * a field error on the token for a link that no longer works, a status for
 * "too many requests". These are the rules that turn that into words.
 */

function failure(overrides: Partial<ApiFailure>): ApiFailure {
  return {
    kind: 'validation',
    status: 400,
    fieldErrors: {},
    reason: null,
    traceId: null,
    errorId: null,
    isExpected: true,
    ...overrides,
  }
}

describe('the reset screens', () => {
  it('say plainly when this environment sends no mail', () => {
    const noMail = failure({ kind: 'unauthorized', status: 403, reason: 'mailUnavailable' })

    expect(passwordResetMessage(noMail, de)).toBe(de.auth.recoveryUnavailable)
  })

  it('treat a refused link as the link, not as a field', () => {
    // The server's key is "Token"; nobody typed a token, so nothing sits under a field.
    const refused = failure({ fieldErrors: { Token: ['This link is invalid, has expired, or has been used already.'] } })

    expect(isRefusedResetLink(refused)).toBe(true)
    expect(passwordResetMessage(refused, de)).toBeNull()
  })

  it('leave a password that breaks the policy under its field', () => {
    const tooShort = failure({ fieldErrors: { Password: ['A password must be at least 10 characters long.'] } })

    expect(isRefusedResetLink(tooShort)).toBe(false)
    expect(passwordResetMessage(tooShort, de)).toBeNull()
  })

  it('ask somebody who asked too often to wait', () => {
    expect(passwordResetMessage(failure({ kind: 'rateLimited', status: 429 }), de)).toBe(de.errors.rateLimited)
  })

  it('say nothing when nothing failed', () => {
    expect(passwordResetMessage(null, de)).toBeNull()
    expect(isRefusedResetLink(null)).toBe(false)
  })
})
