import { normalizeApiError } from './errors'

/**
 * The backend's diagnostics endpoints.
 *
 * Hand-written rather than generated: these routes are deliberately excluded
 * from the OpenAPI document because they only exist outside Staging and
 * Production, and a committed contract that changes per environment would be
 * worse than none. The shapes are small and stable, so a hand-written type is
 * the honest trade.
 */
export interface SentryStatus {
  enabled: boolean
  environment: string
  release: string
  recordingTransport: boolean
}

export interface DiagnosticsApi {
  /** Reports whether the backend would send an event. Never returns the DSN. */
  sentryStatus: () => Promise<SentryStatus>
  /** Always fails with a 500. Used to verify the error path end to end. */
  triggerServerError: () => Promise<never>
}

export function createDiagnosticsApi(
  apiFetch: <T>(url: string, options?: { method?: 'GET' }) => Promise<T>,
): DiagnosticsApi {
  return {
    async sentryStatus() {
      try {
        return await apiFetch<SentryStatus>('/api/diagnostics/sentry', { method: 'GET' })
      }
      catch (error) {
        throw normalizeApiError(error)
      }
    },

    async triggerServerError() {
      try {
        return await apiFetch<never>('/api/diagnostics/boom', { method: 'GET' })
      }
      catch (error) {
        throw normalizeApiError(error)
      }
    },
  }
}
