import type { ApiCaller } from './client'
import type { HandleCheck, Session } from './types'

/**
 * The server's half of the first-run introduction
 * (docs/adr/0039-first-run-onboarding.md).
 *
 * Everything else the introduction changes — the photograph, the name, the
 * theme, the waiting Do — goes through the API that already changes it. Only
 * what exists because of the introduction is here: the handle a person may
 * choose while it is open, and saying it is over.
 */
export interface OnboardingApi {
  /** What a typed handle would be: available, taken, invalid, or the current one. */
  checkHandle: (handle: string) => Promise<HandleCheck>

  /** Takes the handle. Refused once the introduction is over, or if it is taken. */
  chooseHandle: (handle: string) => Promise<Session>

  /** Records that the introduction is behind this person, on every device. */
  complete: () => Promise<Session>
}

export function createOnboardingApi(call: ApiCaller): OnboardingApi {
  return {
    // In a body rather than the address, like every request here: what
    // somebody types while deciding is not a log line.
    checkHandle: handle => call<HandleCheck>('/api/onboarding/handle/check', { method: 'POST', body: { handle } }),

    chooseHandle: handle => call<Session>('/api/onboarding/handle', { method: 'PUT', body: { handle } }),

    complete: () => call<Session>('/api/onboarding/complete', { method: 'POST' }),
  }
}
