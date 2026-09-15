import type { ApiCaller } from './client'
import type {
  AccountDeletion,
  ForgotPasswordRequest,
  LoginRequest,
  PasswordReset,
  RegisterRequest,
  ResetPasswordRequest,
  Session,
} from './types'

/**
 * Registering, signing in, signing out, asking who is signed in — and getting
 * back into an account whose password is gone.
 *
 * Nothing here carries a token. The session is an http-only cookie the browser
 * stores and sends by itself, which is why every one of these returns only the
 * person it belongs to — there is nothing else for the client to keep.
 */
export interface AccountsApi {
  register: (request: RegisterRequest) => Promise<Session>
  login: (request: LoginRequest) => Promise<Session>
  logout: () => Promise<void>
  session: () => Promise<Session>

  /**
   * Deletes the account and everything personal behind it.
   *
   * Takes the password again, and the body is why this is worth a comment: a
   * session cookie authorises reading somebody's screens, not erasing their
   * year from a borrowed phone. The session is already gone by the time this
   * resolves — the server clears the cookie with the account.
   */
  remove: (password: string) => Promise<AccountDeletion>

  /**
   * Asks for a reset link.
   *
   * Resolves the same for every well-formed address, whether or not it has an
   * account: the server answers 202 either way and mails the link afterwards,
   * which is the whole point.
   */
  requestPasswordReset: (request: ForgotPasswordRequest) => Promise<void>

  /**
   * Sets a new password with the token from a reset link. Every session of the
   * account ends with it, this device's included.
   */
  resetPassword: (request: ResetPasswordRequest) => Promise<PasswordReset>
}

export function createAccountsApi(call: ApiCaller): AccountsApi {
  return {
    register: request => call<Session>('/api/auth/register', { method: 'POST', body: request }),

    login: request => call<Session>('/api/auth/login', { method: 'POST', body: request }),

    // 204 No Content, and it succeeds whether or not there was a session.
    logout: async () => {
      await call<unknown>('/api/auth/logout', { method: 'POST' })
    },

    session: () => call<Session>('/api/auth/session', { method: 'GET' }),

    remove: password => call<AccountDeletion>('/api/auth/account', {
      method: 'DELETE',
      body: { password },
    }),

    // 202 Accepted with no body, for an address with an account and without.
    requestPasswordReset: async (request) => {
      await call<unknown>('/api/auth/password/forgot', { method: 'POST', body: request })
    },

    resetPassword: request => call<PasswordReset>('/api/auth/password/reset', { method: 'POST', body: request }),
  }
}
