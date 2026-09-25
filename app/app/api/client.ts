import { normalizeApiError } from './errors'

/**
 * Minimal shape of the fetcher, so tests can pass a stub.
 */
export type ApiFetch = <T>(url: string, options?: {
  method?: 'GET' | 'POST' | 'PUT' | 'DELETE'
  query?: Record<string, string | undefined>
  body?: unknown
  /**
   * Per-call headers, merged over the client's own.
   *
   * Only one thing needs this: an image upload sends its file as the request
   * body and has to say what the bytes are. That media type is also the
   * endpoint's CSRF defence — a cross-site form can send multipart, never
   * `image/jpeg` — so it is not a detail the caller may leave to a default.
   */
  headers?: Record<string, string>
  /**
   * `blob` for the one read that is not JSON: a picture fetched with a token,
   * because an `<img>` in the iOS app cannot send one (`useImageSource`).
   */
  responseType?: 'blob'
}) => Promise<T>

/**
 * Wraps a fetcher so every call comes back as an
 * {@link import('./errors').ApiError}.
 *
 * Normalisation happens here, once, which is what lets every component
 * downstream branch on `kind` instead of poking at `error.response.data`.
 */
export function createCaller(apiFetch: ApiFetch) {
  return async function call<T>(...args: Parameters<ApiFetch>): Promise<T> {
    try {
      return await apiFetch<T>(...args)
    }
    catch (error) {
      throw normalizeApiError(error)
    }
  }
}

export type ApiCaller = ReturnType<typeof createCaller>

/**
 * What `withBearerToken` needs from whoever keeps the tokens.
 *
 * `useSessionTokens` in the iOS app; nothing at all in the browser, which
 * signs in with a cookie instead.
 */
export interface AccessTokenSource {
  /** A token that is valid now, refreshed first if needed, or null when signed out. */
  accessToken: () => Promise<string | null>
  /** Forgets the access token in hand, so the next call refreshes. */
  invalidate: () => void
}

/**
 * Wraps a fetcher so every call carries the bearer token.
 *
 * For the iOS app, whose WebView cannot keep the session cookie
 * (docs/adr/0034-bearer-tokens-for-the-native-app.md). An answer of 401 to a
 * call that did carry a token is tried once more with a fresh one: the token
 * lives a minute, and one that was valid when it was picked can have run out
 * by the time a slow request arrives. A second 401 is the real answer — the
 * refresh itself was refused, so the session is over.
 */
export function withBearerToken(apiFetch: ApiFetch, tokens: AccessTokenSource): ApiFetch {
  return async function authorised<T>(url: string, options?: Parameters<ApiFetch>[1]): Promise<T> {
    const token = await tokens.accessToken()

    try {
      return await apiFetch<T>(url, carrying(options, token))
    }
    catch (error) {
      if (!token || statusOf(error) !== 401) throw error

      tokens.invalidate()
      return await apiFetch<T>(url, carrying(options, await tokens.accessToken()))
    }
  }
}

function carrying(options: Parameters<ApiFetch>[1], token: string | null): Parameters<ApiFetch>[1] {
  return token ? { ...options, headers: { ...options?.headers, Authorization: `Bearer ${token}` } } : options
}

function statusOf(error: unknown): number | null {
  if (typeof error !== 'object' || error === null) return null

  const candidate = error as { status?: unknown, statusCode?: unknown }
  const status = candidate.status ?? candidate.statusCode

  return typeof status === 'number' ? status : null
}
