import { isApiError, type ApiFailure } from '~/api/errors'
import type { Messages } from '~/i18n/messages'
import type { LoginRequest, RegisterRequest, Session } from '~/api/types'

/**
 * Who is signed in, and the actions that change that.
 *
 * In the browser the session is an http-only cookie, so the browser never
 * holds a token and this composable never stores one. The iOS app cannot keep
 * that cookie and signs in for bearer tokens instead; those are kept by
 * `$sessionTokens` (plugins/native.client.ts), and this composable only
 * decides which way to sign in. What it holds is the *answer* to "who is
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

  // Both present only inside the iOS app.
  const tokens = nuxtApp.$sessionTokens ?? null
  const pictures = nuxtApp.$imageCache ?? null
  const session = useState<Session | null>('session', () => null)

  /** True once the server has actually been asked. */
  const isResolved = useState<boolean>('session:resolved', () => false)

  const person = computed(() => session.value?.person ?? null)
  const isSignedIn = computed(() => session.value !== null)

  // `resolve` also runs during SSR middleware. After awaiting the API request,
  // Nuxt's implicit composable context is no longer guaranteed to be active,
  // so cache cleanup must re-enter the context captured at setup time.
  //
  // The pictures the iOS app fetched go with the payloads: they were read
  // with the same session, and the next person on this device must not be
  // shown one from memory that the server would refuse them.
  function clearPersonalData() {
    nuxtApp.runWithContext(() => clearNuxtData())
    pictures?.clear()
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

  /**
   * Signs in the way this client can keep a session: the cookie in the
   * browser, a pair of tokens in the iOS app. The token answer says nothing
   * about who signed in, so the app asks afterwards.
   */
  async function signIn(request: LoginRequest): Promise<Session> {
    if (!tokens) return await api.accounts.login(request)

    await tokens.adopt(await api.accounts.issueTokens(request))
    return await api.accounts.session()
  }

  async function login(request: LoginRequest): Promise<Session> {
    const result = await signIn(request)
    clearPersonalData()
    adopt(result)
    return result
  }

  /**
   * Creates the account and signs in with it.
   *
   * In the browser registering already signs in. The iOS app signs in a second
   * time for its tokens, with the credentials it was just given — registering
   * sets a cookie the app cannot keep.
   */
  async function register(request: RegisterRequest): Promise<Session> {
    const created = await api.accounts.register(request)
    const result = tokens ? await signIn({ email: request.email, password: request.password }) : created
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
      await forget()
    }
  }

  /**
   * Forgets the session on this device without asking the server — for when
   * the server has already ended it, as deleting the account does.
   *
   * In the iOS app that includes the tokens and every picture fetched with
   * them. A refresh token left in the Keychain would be refused at its next
   * use anyway, but "anyway" is not how a signed-out device should work.
   */
  async function forget() {
    adopt(null)
    clearPersonalData()
    await tokens?.clear()
  }

  return { session, person, isSignedIn, isResolved, resolve, adopt, login, logout, forget, register }
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
