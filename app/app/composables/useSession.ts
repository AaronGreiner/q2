import { isApiError, type ApiFailure } from '~/api/errors'
import type { Messages } from '~/i18n/messages'
import type { LoginRequest, RegisterRequest, Session } from '~/api/types'

/**
 * Who is signed in, and the three actions that change that.
 *
 * The session is an http-only cookie, so the browser never holds a token and
 * this composable never stores one. What it holds is the *answer* to "who is
 * this?", shared under one `useState` key so every component asks once — the
 * layout needs it to decide whether to draw the tab bar, the middleware needs
 * it on every navigation, and the settings screen shows the address.
 *
 * `useState` rather than a module-level ref: on the server a module-level
 * value is shared by every request being rendered at once, which would hand
 * one person's name to another.
 */
export function useSession() {
  const api = useQ2Api()
  const nuxtApp = useNuxtApp()
  const session = useState<Session | null>('session', () => null)

  /** True once the server has actually been asked. */
  const isResolved = useState<boolean>('session:resolved', () => false)

  const person = computed(() => session.value?.person ?? null)
  const isSignedIn = computed(() => session.value !== null)

  // `resolve` also runs during SSR middleware. After awaiting the API request,
  // Nuxt's implicit composable context is no longer guaranteed to be active,
  // so cache cleanup must re-enter the context captured at setup time.
  function clearPersonalData() {
    nuxtApp.runWithContext(() => clearNuxtData())
  }

  /**
   * Asks the server who is signed in, at most once per navigation.
   *
   * A 401 is the ordinary answer for somebody who is signed out, so it sets
   * the state and is not an error. Anything else is left to the caller.
   */
  async function resolve(): Promise<Session | null> {
    if (isResolved.value) {
      return session.value
    }

    try {
      session.value = await api.accounts.session()
      isResolved.value = true
      return session.value
    }
    catch (caught) {
      // Only the API's explicit "no session" answer means signed out. A
      // timeout or a 500 is transient and must not be cached as an anonymous
      // session for the rest of the navigation.
      if (!isApiError(caught) || caught.kind !== 'unauthorized') throw caught

      session.value = null
      isResolved.value = true
      clearPersonalData()
      return null
    }
  }

  /** Replaces what is known about the session, after a sign-in or sign-up. */
  function adopt(value: Session | null) {
    session.value = value
    isResolved.value = true
  }

  async function login(request: LoginRequest): Promise<Session> {
    const result = await api.accounts.login(request)
    clearPersonalData()
    adopt(result)
    return result
  }

  async function register(request: RegisterRequest): Promise<Session> {
    const result = await api.accounts.register(request)
    clearPersonalData()
    adopt(result)
    return result
  }

  /**
   * Ends the session and clears everything that was read with it.
   *
   * The cached payloads matter: `useAsyncData` keys survive a navigation, so
   * without this the next person to sign in on the same device would see the
   * previous one's goals for as long as it took the first request to answer.
   */
  async function logout() {
    try {
      await api.accounts.logout()
    }
    finally {
      adopt(null)
      clearPersonalData()
    }
  }

  return { session, person, isSignedIn, isResolved, resolve, adopt, login, logout, register }
}

/**
 * The message to show for a failed sign-in.
 *
 * The API answers with a reason, never a sentence — the app speaks two
 * languages — so the mapping from reason to words lives here, next to the
 * catalogue it reads from.
 */
export function signInMessage(failure: ApiFailure | null, t: Messages): string | null {
  if (!failure) return null

  if (failure.kind === 'unauthorized') {
    return failure.reason === 'lockedOut' ? t.auth.lockedOut : t.auth.invalidCredentials
  }

  // A validation failure is rendered under the field it belongs to; anything
  // else falls back to the shared error wording.
  return failure.kind === 'validation' ? null : t.errors[failure.kind]
}
