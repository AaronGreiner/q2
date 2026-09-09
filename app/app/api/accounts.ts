import type { ApiCaller } from './client'
import type { AccountDeletion, LoginRequest, RegisterRequest, Session } from './types'

/**
 * Registering, signing in, signing out, and asking who is signed in.
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
  }
}
