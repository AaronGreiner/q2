/**
 * The invite code somebody arrived with, on its way to the sign-up form.
 *
 * `useState` rather than a module-level ref: on the server one process answers
 * every request, and a shared ref would hand one visitor's invite to the next
 * one's registration. It is also what carries the code across the redirect from
 * `/join/<code>` to `/register`.
 *
 * Deliberately not a cookie or local storage. The code is a credential in
 * everything but name, and one that outlived the visit would still be sitting
 * in the browser of somebody who decided not to sign up.
 */
export function usePendingInvite() {
  return useState<string | null>('q2:invite', () => null)
}
