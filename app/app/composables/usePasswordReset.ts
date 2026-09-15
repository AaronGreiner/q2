import type { ApiFailure } from '~/api/errors'
import type { PasswordReset } from '~/api/types'
import type { Messages } from '~/i18n/messages'

/**
 * Asking for a reset link, and setting a new password with one.
 *
 * Nothing is kept here. The token lives in the page that lifted it out of the
 * address bar (utils/resetLink.ts), and a reset ends the session, so all that
 * is left afterwards is the address — which the page hands to `useAuthEmail`
 * so the sign-in screen is already filled in.
 */
export function usePasswordReset() {
  const api = useQ2Api()
  const { logout } = useSession()

  /** Resolves the same whether or not the address has an account. */
  async function requestLink(email: string): Promise<void> {
    await api.accounts.requestPasswordReset({ email })
  }

  /**
   * Sets the new password, then lets go of whatever this device still held.
   *
   * The server has already ended the session — every session of the account
   * — so `logout` is only here to clear the payloads that were read with it.
   * It clears them even when its own request does not get through, which is
   * why a failure there does not turn a successful reset into an error.
   */
  async function reset(token: string, password: string): Promise<PasswordReset> {
    const result = await api.accounts.resetPassword({ token, password })
    await logout().catch(() => undefined)
    return result
  }

  return { requestLink, reset }
}

/**
 * Whether the server refused the link itself rather than the new password.
 *
 * Expired, used, altered or never one of ours all come back the same way — as
 * an error on the token, a field nobody typed — and a link that no longer works
 * replaces the form instead of sitting under it.
 */
export function isRefusedResetLink(failure: ApiFailure | null): boolean {
  return failure?.kind === 'validation'
    && Object.keys(failure.fieldErrors).some(field => field.toLowerCase() === 'token')
}

/**
 * The sentence for a failed request on the two reset screens.
 *
 * Null for what is shown elsewhere: a field error sits under its field, and a
 * refused link replaces the form (`isRefusedResetLink`).
 */
export function passwordResetMessage(failure: ApiFailure | null, t: Messages): string | null {
  if (!failure) return null

  if (failure.kind === 'unauthorized' && failure.reason === 'mailUnavailable') {
    return t.auth.recoveryUnavailable
  }

  return failure.kind === 'validation' ? null : t.errors[failure.kind]
}
