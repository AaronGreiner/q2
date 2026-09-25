import { isApiError } from '~/api/errors'
import type { AccessTokenSource } from '~/api/client'
import type { SessionTokens } from '~/api/types'

/**
 * Where the refresh token is kept between launches.
 *
 * The iOS Keychain in the app (`plugins/native.client.ts`); a plain object in
 * the tests. Only the refresh token is ever written down — the access token
 * lives a minute and stays in memory.
 */
export interface RefreshTokenStorage {
  read: () => Promise<string | null>
  write: (refreshToken: string) => Promise<void>
  clear: () => Promise<void>
}

export interface TokenKeeperOptions {
  storage: RefreshTokenStorage
  /** Trades a refresh token for a new pair — `api.accounts.refreshTokens`. */
  refresh: (refreshToken: string) => Promise<SessionTokens>
  now?: () => number
}

export interface TokenKeeper extends AccessTokenSource {
  /** Takes a pair the server just issued. */
  adopt: (tokens: SessionTokens) => Promise<void>
  /** Signs this device out: forgets both tokens. */
  clear: () => Promise<void>
}

/**
 * How long before its expiry an access token is treated as spent.
 *
 * A token picked with a second to go would expire on the way to the server;
 * five seconds is more than any request here takes to arrive.
 */
const expiryMargin = 5_000

/**
 * The iOS app's half of a session: an access token in memory and a refresh
 * token in the Keychain (docs/adr/0034-bearer-tokens-for-the-native-app.md).
 *
 * Three rules, each a bug it prevents:
 *
 * - **One refresh at a time.** A screen fires half a dozen reads at once, and
 *   each would otherwise trade the same refresh token for a pair of its own.
 * - **Only a refusal signs out.** A refresh that fails because the phone is
 *   offline keeps the refresh token and fails the call as a network error. A
 *   train through a tunnel must not end a session; a changed password must.
 * - **Signing out wins a race.** A refresh still on its way when somebody
 *   signs out is thrown away when it arrives, rather than signing them back in.
 */
export function createTokenKeeper({ storage, refresh, now = Date.now }: TokenKeeperOptions): TokenKeeper {
  let access: { token: string, expiresAt: number } | null = null
  let pending: Promise<string | null> | null = null

  // Bumped by `clear`, so a refresh that started before it knows it is stale.
  let generation = 0

  async function adopt(tokens: SessionTokens) {
    access = { token: tokens.accessToken, expiresAt: now() + tokens.expiresIn * 1000 - expiryMargin }
    await storage.write(tokens.refreshToken)
  }

  async function renew(): Promise<string | null> {
    const started = generation
    const refreshToken = await storage.read()

    if (!refreshToken) return null

    let tokens: SessionTokens

    try {
      tokens = await refresh(refreshToken)
    }
    catch (error) {
      if (!isApiError(error) || error.kind !== 'unauthorized') throw error

      if (started === generation) await clear()
      return null
    }

    if (started !== generation) return null

    await adopt(tokens)
    return tokens.accessToken
  }

  function accessToken(): Promise<string | null> {
    if (access && now() < access.expiresAt) return Promise.resolve(access.token)

    pending ??= renew().finally(() => {
      pending = null
    })

    return pending
  }

  function invalidate() {
    access = null
  }

  async function clear() {
    generation += 1
    access = null
    await storage.clear()
  }

  return { adopt, accessToken, invalidate, clear }
}
