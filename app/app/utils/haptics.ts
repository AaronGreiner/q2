/** A tactile acknowledgement of a tap, never a promise that a request succeeded.
 * Call directly from the interaction so browsers retain user activation.
 */
export function hapticTap(): void {
  try {
    if (typeof window === 'undefined'
      || typeof navigator.vibrate !== 'function'
      || window.matchMedia('(prefers-reduced-motion: reduce)').matches) return

    navigator.vibrate(15)
  }
  catch {
    // Unsupported or refused device feedback is expected. It must neither
    // interrupt the action nor produce logs or a Sentry event.
  }
}
