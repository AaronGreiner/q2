import { normalizeApiError } from './errors'

/**
 * Minimal shape of the fetcher, so tests can pass a stub.
 */
export type ApiFetch = <T>(url: string, options?: {
  method?: 'GET' | 'POST' | 'PUT' | 'DELETE'
  query?: Record<string, string | undefined>
  body?: unknown
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
