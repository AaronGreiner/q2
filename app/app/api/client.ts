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
