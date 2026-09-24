/**
 * Nothing in q2 is reachable without a session, except the screens that create
 * one and the ones that get somebody back into theirs.
 *
 * Global rather than per-page: a new page is written by adding a file, and a
 * guard somebody has to remember to add is a guard that will eventually be
 * missing from exactly the screen that needed it. Opting *out* is explicit and
 * visible, which is the right way round for this.
 *
 * This is convenience, not security. The API refuses every request without a
 * session on its own (`RequireAuthorization` on every feature group); all this
 * does is take somebody to the sign-in screen instead of showing them five
 * error states.
 */
const publicRoutes = new Set(['/login', '/register', '/forgot-password'])

/**
 * Reachable with or without a session, and never redirected away from.
 *
 * The diagnostics page has to work when nothing else does. A reset link is
 * opened on whatever device its mail was read on, and somebody who happens to
 * be signed in there must still be able to use it.
 */
const openRoutes = new Set(['/diagnostics', '/reset-password'])

/**
 * An invite link, which is usually followed by somebody with no session — and
 * sometimes by somebody who has one and wants to accept it.
 *
 * A prefix rather than a member of the sets above, because the code is part of
 * the path. Never redirected away from in either case: the page says whose link
 * it is and offers the step that fits whether there is a session.
 */
const publicPrefixes = ['/join/']

export default defineNuxtRouteMiddleware(async (to) => {
  if (publicPrefixes.some(prefix => to.path.startsWith(prefix))) {
    // Resolved first, because the page behind an invite link offers a
    // different step depending on whether there is a session.
    await useSession().resolve()
    return
  }

  if (publicRoutes.has(to.path) || openRoutes.has(to.path)) {
    const { isSignedIn, resolve } = useSession()
    await resolve()

    // Already signed in and heading for the sign-in screen: there is nothing
    // there to do.
    return isSignedIn.value && publicRoutes.has(to.path) ? navigateTo('/') : undefined
  }

  const { isSignedIn, resolve } = useSession()
  await resolve()

  if (isSignedIn.value) {
    return
  }

  // Where they were going, so signing in lands there rather than on the start
  // screen. `fullPath`, because a chat link carries an id.
  return navigateTo({
    path: '/login',
    query: to.fullPath === '/' ? undefined : { next: to.fullPath },
  })
})
