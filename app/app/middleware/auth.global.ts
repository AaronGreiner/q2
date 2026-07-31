/**
 * Nothing in q2 is reachable without a session, except the two screens that
 * create one.
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
const publicRoutes = new Set(['/login', '/register'])

export default defineNuxtRouteMiddleware(async (to) => {
  // The diagnostics page deliberately exists outside the app — it is how the
  // Sentry wiring is checked, and it must work when nothing else does.
  if (publicRoutes.has(to.path) || to.path === '/diagnostics') {
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
