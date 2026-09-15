/**
 * The address somebody is signing in with, shared by the screens around it.
 *
 * Typed once: the sign-in screen, "forgot password" and the end of a reset all
 * read and write the same value, so going from one to the next never asks for
 * the address again. `useState` rather than a module-level ref, because on the
 * server a module-level value would be shared by every request being rendered
 * at once. A successful sign-in clears it (pages/login.vue).
 */
export function useAuthEmail() {
  return useState<string>('auth:email', () => '')
}
